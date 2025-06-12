using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using Topup.Shared;
using Topup.Shared.CacheManager;
using Topup.Shared.Dtos;
using Topup.Shared.Utils;
using Topup.TopupGw.Contacts.ApiRequests;
using Topup.TopupGw.Contacts.Dtos;
using Topup.TopupGw.Contacts.Enums;
using Topup.TopupGw.Domains.BusinessServices;
using Microsoft.Extensions.Logging;
using ServiceStack;
using MongoDB.Bson;

namespace Topup.TopupGw.Components.Connectors.Vinatti
{
    public class VinattiConnector : IGatewayConnector
    {
        private readonly ILogger<VinattiConnector> _logger;
        private readonly ITopupGatewayService _topupGatewayService;
        private readonly ICacheManager _cacheManager;

        public VinattiConnector(ITopupGatewayService topupGatewayService, ILogger<VinattiConnector> logger,
            ICacheManager cacheManager)
        {
            _topupGatewayService = topupGatewayService;
            _logger = logger;
            _cacheManager = cacheManager;
        }

        public async Task<MessageResponseBase> TopupAsync(TopupRequestLogDto topupRequestLog, ProviderInfoDto providerInfo)
        {
            _logger.LogInformation("VinattiConnector request: " + topupRequestLog.ToJson());
            var responseMessage = new MessageResponseBase();
            if (!_topupGatewayService.ValidConnector(ProviderConst.VINATTI, providerInfo.ProviderCode))
            {
                _logger.LogError($"{topupRequestLog.TransCode} - {topupRequestLog.TransRef}-{providerInfo.ProviderCode} - VinattiConnector ProviderConnector not valid");
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error,
                    ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
                };
            }

            var dto = new VinattiTopupDto
            {
                MerchantCode = providerInfo.ApiUser,
                RefNumber = providerInfo.Username,
                Amount = topupRequestLog.TransAmount,
                MobileNumber = topupRequestLog.ReceiverInfo,
                Telco = topupRequestLog.Vendor,
                TransRefNumber = topupRequestLog.TransCode,
            };

            if (dto.Telco == "VTE")
                dto.Telco = "Viettel";
            else if (dto.Telco == "VNA")
                dto.Telco = "Vinaphone";
            else if (dto.Telco == "GMOBILE")
                dto.Telco = "Gmobile";
            else if (dto.Telco == "VNM")
                dto.Telco = "Vietnamobile";
            else if (dto.Telco == "VMS")
                dto.Telco = "Mobifone";
            else if (dto.Telco is "WT" or "Wintel")
                dto.Telco = "Wintel";


            // Tạo secret key (32 byte)
            byte[] key256 = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(key256);
            }

            // Mã hóa
            string encrypted = EncryptJson(dto.ToJson(), key256);
            string sign = Sign(encrypted, "./" + providerInfo.PrivateKeyFile);
            var request = new VinattiRequest
            {
                Code = "TOPUP",
                Time = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"),
                Data = encrypted,
                Signature = sign,
            };

            _logger.LogInformation($"{topupRequestLog.TransCode} VinattiConnector Param_Json: " + request.ToJson());

            var result = await CallApi(providerInfo, request.Code, topupRequestLog.TransCode, request.ToJson());
            _logger.LogInformation($"{topupRequestLog.TransCode} - {topupRequestLog.TransRef} VinattiConnector Topup Reponse: {result.ToJson()}");

            try
            {
                responseMessage.ProviderResponseCode = result?.Code;
                responseMessage.ProviderResponseMessage = result?.Message;
                if (result.Code == "00")
                {
                    topupRequestLog.ModifiedDate = DateTime.Now;
                    topupRequestLog.ResponseInfo = result.ToJson();
                    topupRequestLog.Status = TransRequestStatus.Success;
                    responseMessage.ResponseCode = ResponseCodeConst.Success;
                    responseMessage.ResponseMessage = "Thành công";
                }
                else
                {

                    topupRequestLog.ModifiedDate = DateTime.Now;
                    topupRequestLog.ResponseInfo = result.ToJson();
                    topupRequestLog.Status = TransRequestStatus.Fail;
                    var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.VINATTI,
                        result.Code, topupRequestLog.TransCode);
                    responseMessage.ResponseCode = reResult != null ? reResult.ResponseCode : ResponseCodeConst.ResponseCode_WaitForResult;
                    responseMessage.ResponseMessage = reResult != null
                        ? reResult.ResponseName : "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                }
            }
            catch (Exception ex)
            {
                _logger.LogInformation(
                    $"VinattiConnector Error: {topupRequestLog.ProviderCode}-{topupRequestLog.TransCode}-{topupRequestLog.TransRef}-{result.ToJson()} Exception: {ex}");
                responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_TimeOut;
                topupRequestLog.Status = TransRequestStatus.Timeout;
            }
            finally
            {
                await _topupGatewayService.TopupRequestLogUpdateAsync(topupRequestLog);
            }
            return responseMessage;
        }

        public async Task<MessageResponseBase> TransactionCheckAsync(string providerCode, string transCodeToCheck,
            string transCode, string serviceCode = null, ProviderInfoDto providerInfo = null)
        {
            try
            {
                _logger.LogInformation($"{transCodeToCheck} VinattiConnector CheckTrans request: " + transCode);
                var responseMessage = new MessageResponseBase();

                if (providerInfo == null)
                    providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(providerCode);


                if (providerInfo == null ||
                    !_topupGatewayService.ValidConnector(ProviderConst.VINATTI, providerInfo.ProviderCode))
                {
                    _logger.LogError(
                        $"{transCodeToCheck} - {providerCode} - VinattiConnector ProviderConnector not valid");
                    return new MessageResponseBase
                    {
                        ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult,
                        ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ"
                    };
                }
                var dto = new VinattiBalanceDto
                {
                    MerchantCode = providerInfo.ApiUser,
                    TransRefNumber = transCodeToCheck,
                };

                byte[] key256 = new byte[32];
                using (var rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(key256);
                }

                string encrypted = EncryptJson(dto.ToJson(), key256);
                string sign = Sign(encrypted, "./" + providerInfo.PrivateKeyFile);
                var request = new VinattiRequest
                {
                    Code = "CHECK_TRANSACTION",
                    Time = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"),
                    Data = encrypted,
                    Signature = sign,
                };

                _logger.LogInformation($"{transCode} VinattiConnector Param_Json: " + request.ToJson());

                var result = await CallApi(providerInfo, request.Code, transCode, request.ToJson());
                _logger.LogInformation($"{transCode} - {providerCode} VinattiConnector Topup Reponse: " +
                                       result.ToJson());

                if (result.Code == "00")
                {
                    string dataGen = DecryptJson(result.Data, key256);
                    var objData = dataGen.FromJson<VinattiViewTransDto>();
                    if (objData.Status == "A")
                    {
                        responseMessage.ResponseCode = ResponseCodeConst.Success;
                        responseMessage.ResponseMessage = "Thành công";
                    }
                    else if (objData.Status is "C" or "E")
                    {
                        responseMessage.ResponseCode = ResponseCodeConst.Error;
                        responseMessage.ResponseMessage = "Giao dịch thất bại";
                    }
                    else
                    {
                        responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                        responseMessage.ResponseMessage = "Giao dịch chưa có kết quả";
                    }
                }
                else
                {
                    var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.VINATTI,
                         result.Code, transCodeToCheck);
                    responseMessage.ResponseCode = reResult != null ? reResult.ResponseCode : ResponseCodeConst.ResponseCode_WaitForResult;
                    responseMessage.ResponseMessage = reResult != null
                        ? reResult.ResponseName : "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                }

                responseMessage.ProviderResponseCode = result?.Code;
                responseMessage.ProviderResponseMessage = result?.Message;
                return responseMessage;
            }
            catch (Exception e)
            {
                return new MessageResponseBase
                {
                    ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ",
                    ResponseCode = ResponseCodeConst.ResponseCode_TimeOut,
                    Exception = e.Message
                };
            }
        }

        public async Task<NewMessageResponseBase<InvoiceResultDto>> QueryAsync(PayBillRequestLogDto payBillRequestLog)
        {
            throw new NotImplementedException();
        }

        public async Task<MessageResponseBase> CardGetByBatchAsync(CardRequestLogDto cardRequestLog)
        {
            throw new NotImplementedException();
        }

        public async Task<MessageResponseBase> CheckBalanceAsync(string providerCode, string transCode)
        {
            _logger.LogInformation("QueryBalanceAsync request: " + providerCode);
            var responseMessage = new MessageResponseBase();
            var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(providerCode);

            if (providerInfo == null)
            {
                _logger.LogInformation($"providerCode= {providerCode}|providerInfo is null");
                return responseMessage;
            }

            if (!_topupGatewayService.ValidConnector(ProviderConst.VINATTI, providerInfo.ProviderCode))
            {
                _logger.LogError(
                    $"{providerCode}-{transCode}-{providerInfo.ProviderCode} - VinattiConnector ProviderConnector not valid");
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error,
                    ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
                };
            }

            var dto = new VinattiBalanceDto
            {
                MerchantCode = providerInfo.ApiUser,
                TransRefNumber = transCode,
            };

            byte[] key256 = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(key256);
            }

            string encrypted = EncryptJson(dto.ToJson(), key256);
            string sign = Sign(encrypted, "./" + providerInfo.PrivateKeyFile);
            var request = new VinattiRequest
            {
                Code = "CHECK_BALANCE",
                Time = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"),
                Data = encrypted,
                Signature = sign,
            };

            _logger.LogInformation($"{transCode} VinattiConnector Param_Json: " + request.ToJson());

            var result = await CallApi(providerInfo, request.Code, transCode, request.ToJson());
            _logger.LogInformation($"{transCode} - {providerCode} VinattiConnector Topup Reponse: " +
                                   result.ToJson());


            if (result.Code == "00")
            {
                string dataGen = DecryptJson(result.Data, key256);
                responseMessage.ResponseMessage = "Thành công";
                responseMessage.ResponseCode = ResponseCodeConst.Success;
            }

            return responseMessage;
        }

        public Task<MessageResponseBase> DepositAsync(DepositRequestDto request)
        {
            throw new NotImplementedException();
        }

        public async Task<MessageResponseBase> PayBillAsync(PayBillRequestLogDto payBillRequestLog)
        {
            throw new NotImplementedException();
        }

        private async Task<VinattiResponse> CallApi(ProviderInfoDto providerInfo, string function, string transCode, string JsonData)
        {
            try
            {
                ServicePointManager.Expect100Continue = true;
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                var client = new JsonServiceClient(providerInfo.ApiUrl) { Timeout = TimeSpan.FromMinutes(providerInfo.Timeout) };
                var result = await client.PostAsync<object>("/Gateway/Execute", JsonData);
                _logger.LogInformation($"{transCode} Func = {function} - CallApi_VinattiConnector - Reponse result: {result.ToJson()}");
                return result.ConvertTo<VinattiResponse>();
            }
            catch (Exception ex)
            {
                _logger.LogError($"{transCode} CallApi_VinattiConnector - Reponse exception : {ex.Message}");

                return new VinattiResponse()
                {
                    Code = "501102",
                };
            }
        }

        public Task<ResponseMessageApi<object>> GetProductInfo(GetProviderProductInfo info)
        {
            throw new NotImplementedException();
        }

        public Task<MessageResponseBase> CheckPrePostPaid(string msisdn, string transCode, string providerCode)
        {
            throw new NotImplementedException();
        }

        private string Sign(string dataToSign, string privateFile)
        {
            //todo Cache privateKey;

            var privateKey = File.ReadAllText("files/" + privateFile);
            var privateKeyBlocks = privateKey.Split("-", StringSplitOptions.RemoveEmptyEntries);
            var privateKeyBytes = Convert.FromBase64String(privateKeyBlocks[1]);

            using var rsa = RSA.Create();

            if (privateKeyBlocks[0] == "BEGIN PRIVATE KEY")
                rsa.ImportPkcs8PrivateKey(privateKeyBytes, out _);
            else if (privateKeyBlocks[0] == "BEGIN RSA PRIVATE KEY") rsa.ImportRSAPrivateKey(privateKeyBytes, out _);

            var sig = rsa.SignData(
                Encoding.ASCII.GetBytes(dataToSign),
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            var signature = Convert.ToBase64String(sig);

            return signature;
        }

        private static string EncryptJson(string jsonString, byte[] key256bit)
        {
            byte[] plainBytes = Encoding.UTF8.GetBytes(jsonString);

            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = key256bit;
                aesAlg.Mode = CipherMode.CBC;
                aesAlg.Padding = PaddingMode.PKCS7;

                // Tạo IV ngẫu nhiên (16 byte)
                aesAlg.GenerateIV();
                byte[] iv = aesAlg.IV;

                // Tạo encryptor
                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, iv);

                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    // Ghi IV đầu tiên vào stream
                    msEncrypt.Write(iv, 0, iv.Length);

                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        csEncrypt.Write(plainBytes, 0, plainBytes.Length);
                        csEncrypt.FlushFinalBlock();
                    }

                    // Kết quả là IV + Ciphertext, encode bằng Base64
                    return Convert.ToBase64String(msEncrypt.ToArray());
                }
            }
        }

        private static string DecryptJson(string encryptedBase64, byte[] key256bit)
        {
            byte[] fullCipher = Convert.FromBase64String(encryptedBase64);

            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = key256bit;
                aesAlg.Mode = CipherMode.CBC;
                aesAlg.Padding = PaddingMode.PKCS7;

                // Tách IV (16 byte đầu)
                byte[] iv = new byte[16];
                Array.Copy(fullCipher, 0, iv, 0, iv.Length);

                // Tách Ciphertext
                byte[] cipherBytes = new byte[fullCipher.Length - iv.Length];
                Array.Copy(fullCipher, iv.Length, cipherBytes, 0, cipherBytes.Length);

                aesAlg.IV = iv;
                ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                using (MemoryStream msDecrypt = new MemoryStream(cipherBytes))
                using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                {
                    return srDecrypt.ReadToEnd(); // chuỗi JSON gốc
                }
            }
        }

    }

    internal class VinattiRequest
    {
        public string Code { get; set; }

        public string Data { get; set; }

        public string Time { get; set; }

        public string Signature { get; set; }
    }

    internal class VinattiResponse
    {
        public string Code { get; set; }

        public string Message { get; set; }

        public string Data { get; set; }

        public string Time { get; set; }
        public string Signature { get; set; }
    }

    internal class VinattiTopupDto
    {
        public string MerchantCode { get; set; }

        public string RefNumber { get; set; }

        public string Telco { get; set; }

        public decimal Amount { get; set; }

        public string TransRefNumber { get; set; }

        public string MobileNumber { get; set; }
    }

    internal class VinattiTransDto
    {
        public string MerchantCode { get; set; }

        public string TransRefNumber { get; set; }
    }
    internal class VinattiBalanceDto
    {
        public string MerchantCode { get; set; }

        public string TransRefNumber { get; set; }
    }

    internal class VinattiViewTransDto
    {
        public string TransRefNumber { get; set; }

        public string RetRefNumber { get; set; }

        public string Status { get; set; }
    }
}
