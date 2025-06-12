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
using Topup.TopupGw.Domains.Repositories;
using ThirdParty.Json.LitJson;

namespace Topup.TopupGw.Components.Connectors.Vinatti
{
    public class VinattiConnector(
    ITopupGatewayService TopupGatewayService,
    ILogger<VinattiConnector> logger) : GatewayConnectorBase(TopupGatewayService)
    {
        public override async Task<MessageResponseBase> TopupAsync(TopupRequestLogDto topupRequestLog, ProviderInfoDto providerInfo)
        {
            logger.LogInformation("VinattiConnector request: " + topupRequestLog.ToJson());
            var responseMessage = new MessageResponseBase();
            if (!TopupGatewayService.ValidConnector(ProviderConst.VINATTI, providerInfo.ProviderCode))
            {
                logger.LogError($"{topupRequestLog.TransCode} - {topupRequestLog.TransRef} - {providerInfo.ProviderCode} - VinattiConnector ProviderConnector not valid");
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
           
            try
            {
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

                string jsonData = request.ToJson();
                logger.LogInformation($"{topupRequestLog.TransCode} VinattiConnector Param_Json: {jsonData}");
                var result = await CallApi(providerInfo, request.Code, topupRequestLog.TransCode, jsonData);
                var jsonResponse = result.ToJson();
                logger.LogInformation($"{topupRequestLog.TransCode} - {topupRequestLog.TransRef} VinattiConnector Topup Reponse: {jsonResponse}");

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
                    topupRequestLog.ResponseInfo = jsonResponse;
                    topupRequestLog.Status = TransRequestStatus.Fail;
                    var reResult = await TopupGatewayService.GetResponseMassageCacheAsync(ProviderConst.VINATTI, result.Code, topupRequestLog.TransCode);
                    responseMessage.ResponseCode = reResult != null ? reResult.ResponseCode : ResponseCodeConst.ResponseCode_WaitForResult;
                    responseMessage.ResponseMessage = reResult != null ? reResult.ResponseName : "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                }
            }
            catch (Exception ex)
            {
                logger.LogInformation($"VinattiConnector Error: {topupRequestLog.ProviderCode} - {topupRequestLog.TransCode} - {topupRequestLog.TransRef} - exception: {ex}");
                responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_TimeOut;
                topupRequestLog.Status = TransRequestStatus.Timeout;
            }
            finally
            {
                await TopupGatewayService.TopupRequestLogUpdateAsync(topupRequestLog);
            }
            return responseMessage;
        }

        public override async Task<MessageResponseBase> TransactionCheckAsync(string providerCode, string transCodeToCheck,
            string transCode, string serviceCode = null, ProviderInfoDto providerInfo = null)
        {
            try
            {
                logger.LogInformation($"{transCodeToCheck} VinattiConnector CheckTrans request: " + transCode);
                var responseMessage = new MessageResponseBase();

                if (providerInfo == null)
                    providerInfo = await TopupGatewayService.ProviderInfoCacheGetAsync(providerCode);

                if (providerInfo == null ||
                    !TopupGatewayService.ValidConnector(ProviderConst.VINATTI, providerInfo.ProviderCode))
                {
                    logger.LogError($"{transCodeToCheck} - {providerCode} - VinattiConnector ProviderConnector not valid");
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
                string jsonData = request.ToJson();
                logger.LogInformation($"{transCode} VinattiConnector Param_Json: {jsonData}");
                var result = await CallApi(providerInfo, request.Code, transCode, jsonData);
                logger.LogInformation($"{transCode} - {providerCode} VinattiConnector Topup Reponse: {result.ToJson()}");

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
                    var reResult = await TopupGatewayService.GetResponseMassageCacheAsync(ProviderConst.VINATTI,result.Code, transCodeToCheck);
                    responseMessage.ResponseCode = reResult != null ? reResult.ResponseCode : ResponseCodeConst.ResponseCode_WaitForResult;
                    responseMessage.ResponseMessage = reResult != null ? reResult.ResponseName : "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                }

                responseMessage.ProviderResponseCode = result?.Code;
                responseMessage.ProviderResponseMessage = result?.Message;
                return responseMessage;
            }
            catch (Exception e)
            {
                logger.LogInformation($"{transCode} VinattiConnector TransactionCheckAsync exception : {e}");
                return new MessageResponseBase
                {
                    ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ",
                    ResponseCode = ResponseCodeConst.ResponseCode_TimeOut,
                    Exception = e.Message
                };
            }
        }

        public override async Task<MessageResponseBase> CheckBalanceAsync(string providerCode, string transCode)
        {
            logger.LogInformation($"QueryBalanceAsync request: {providerCode}");
            var responseMessage = new MessageResponseBase();
            var providerInfo = await TopupGatewayService.ProviderInfoCacheGetAsync(providerCode);

            if (providerInfo == null)
            {
                logger.LogInformation($"providerCode = {providerCode} - providerInfo is null");
                return responseMessage;
            }

            if (!TopupGatewayService.ValidConnector(ProviderConst.VINATTI, providerInfo.ProviderCode))
            {
                logger.LogError($"{transCode} - {providerInfo.ProviderCode} - VinattiConnector ProviderConnector not valid");
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error,
                    ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
                };
            }

            try
            {
                var dto = new VinattiTransDto
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

                var jsonData = request.ToJson();
                logger.LogInformation($"{transCode} VinattiConnector Param_Json: {jsonData}");
                var result = await CallApi(providerInfo, request.Code, transCode, jsonData);
                logger.LogInformation($"{transCode} - {providerCode} VinattiConnector Topup Reponse: {result.ToJson()}");

                if (result.Code == "00")
                {
                    string dataGen = DecryptJson(result.Data, key256);
                    responseMessage.ResponseMessage = "Thành công";
                    responseMessage.ResponseCode = ResponseCodeConst.Success;
                }
                else
                {
                    responseMessage.ResponseMessage = "Không thành công";
                    responseMessage.ResponseCode = ResponseCodeConst.Error;
                }                     
            }
            catch(Exception ex)
            {
                logger.LogError($"{transCode} VinattiConnector CheckBalanceAsync exception : {ex}");
            }
            return responseMessage;
        }

        private async Task<VinattiResponse> CallApi(ProviderInfoDto providerInfo, string function, string transCode, string JsonData)
        {
            try
            {
                ServicePointManager.Expect100Continue = true;
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                var client = new JsonServiceClient(providerInfo.ApiUrl) { Timeout = TimeSpan.FromMinutes(providerInfo.Timeout) };
                var result = await client.PostAsync<object>("/Gateway/Execute", JsonData);
                logger.LogInformation($"{transCode} Func = {function} - CallApi_VinattiConnector - result: {result.ToJson()}");
                return result.ConvertTo<VinattiResponse>();
            }
            catch (Exception ex)
            {
                logger.LogError($"{transCode} CallApi_VinattiConnector - exception : {ex.Message}");
                return new VinattiResponse()
                {
                    Code = "501102",
                    Message="Chưa có kết quả"
                };
            }
        }

        private string Sign(string dataToSign, string privateFile)
        {           
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
