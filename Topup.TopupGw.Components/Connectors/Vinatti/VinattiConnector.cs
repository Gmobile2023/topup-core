using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Topup.Shared;
using Topup.TopupGw.Contacts.Dtos;
using Topup.TopupGw.Contacts.Enums;
using Topup.TopupGw.Domains.BusinessServices;
using Microsoft.Extensions.Logging;
using ServiceStack;
using MongoDB.Bson;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Crypto.Encodings;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;
using System.Security.Cryptography.X509Certificates;
using System.Runtime.Serialization;
using Org.BouncyCastle.Pkcs;
using System.Linq;
using Org.BouncyCastle.OpenSsl;
using ServiceStack.Text.Controller;

namespace Topup.TopupGw.Components.Connectors.Vinatti
{

    public class VinattiConnector(
    ITopupGatewayService TopupGatewayService,
    ILogger<VinattiConnector> logger) : GatewayConnectorBase(TopupGatewayService)
    {
        private const int IV_SIZE = 16;
        public override async Task<MessageResponseBase> TopupAsync(TopupRequestLogDto topupRequestLog, ProviderInfoDto providerInfo)
        {
            logger.LogInformation($"VinattiConnector request: {topupRequestLog.ToJson()}");
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
                PartnerCode = providerInfo.ApiUser,
                UserName = providerInfo.Username,
                Password = providerInfo.Password,
                Amount = topupRequestLog.TransAmount,
                TransAmount = topupRequestLog.TransAmount,
                ReceiverMobileNumber = topupRequestLog.ReceiverInfo,
                RefNumber = topupRequestLog.ReceiverInfo,
                TransRefNumber = topupRequestLog.TransCode,
                OrderID = topupRequestLog.TransCode,
                Details = "Nạp tiền"
            };

            if (topupRequestLog.Vendor == "VTE")
                dto.NetworkHomeID = 1;
            else if (topupRequestLog.Vendor == "VMS")
                dto.NetworkHomeID = 2;
            else if (topupRequestLog.Vendor == "VNA")
                dto.NetworkHomeID = 3;
            else if (topupRequestLog.Vendor == "VNM")
                dto.NetworkHomeID = 4;
            else if (topupRequestLog.Vendor == "GMOBILE")
                dto.NetworkHomeID = 5;

            try
            {
                var dataItem = dto.ToJson();
                string encrypted = Encrypt(dataItem, providerInfo.PublicKey);
                string sign = Sign(encrypted, "./" + providerInfo.PrivateKeyFile);
                var request = new VinattiRequest
                {
                    Code = "TOPUP",
                    Time = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"),
                    Data = encrypted,
                    Signature = sign,
                };

                string jsonData = request.ToJson();
                logger.LogInformation($"{topupRequestLog.TransCode} VinattiConnector topup_Json: {dataItem}");
                var result = await CallApi(providerInfo, request.Code, topupRequestLog.TransCode, jsonData);
                var jsonResponse = result.ToJson();
                logger.LogInformation($"{topupRequestLog.TransCode} - {topupRequestLog.TransRef} VinattiConnector topup reponse: {jsonResponse}");

                responseMessage.ProviderResponseCode = result?.Code;
                responseMessage.ProviderResponseMessage = result?.Message;
                if (result.Code == "00")
                {
                    var topupInfo = Decrypt(result.Data, providerInfo.PublicKeyFile, providerInfo.ApiPassword);
                    logger.LogInformation($"{topupRequestLog.ProviderCode} - {topupRequestLog.TransCode} - {topupRequestLog.TransRef} - VinattiConnector - data-reponse : {topupInfo}");
                    var objData = topupInfo.FromJson<VinattiViewTransDto>();
                    topupRequestLog.ModifiedDate = DateTime.Now;
                    topupRequestLog.ResponseInfo = jsonResponse;
                    topupRequestLog.Status = TransRequestStatus.Success;
                    responseMessage.ResponseCode = ResponseCodeConst.Success;
                    responseMessage.ResponseMessage = "Thành công";
                    responseMessage.ReceiverType = objData.PrepaidPostpaid;
                    responseMessage.ProviderResponseTransCode = objData.RetRefNumber;
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
                logger.LogInformation($"VinattiConnector CheckTrans request: {transCodeToCheck}");
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
                var dto = new VinattiTransDto
                {
                    PartnerCode = providerInfo.ApiUser,
                    UserName = providerInfo.Username,
                    Password = providerInfo.Password,
                    TransRefNumber = transCodeToCheck,
                };

                var dataItem = dto.ToJson();
                string encrypted = Encrypt(dataItem, providerInfo.PublicKey);
                string sign = Sign(encrypted, "./" + providerInfo.PrivateKeyFile);
                var request = new VinattiRequest
                {
                    Code = "CHECK_TRANSACTION",
                    Time = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"),
                    Data = encrypted,
                    Signature = sign,
                };
                string jsonData = request.ToJson();
                logger.LogInformation($"{transCodeToCheck} VinattiConnector checktran_json: {dataItem}");
                var result = await CallApi(providerInfo, request.Code, transCode, jsonData);
                logger.LogInformation($"{transCode} - {providerCode} VinattiConnector checktrans reponse: {result.ToJson()}");

                if (result.Code == "00")
                {
                    var dataInfo = Decrypt(result.Data, providerInfo.PublicKeyFile, providerInfo.ApiPassword);
                    logger.LogInformation($"{transCodeToCheck} - Checktran - VinattiConnector - data-reponse : {dataInfo}");
                    var objData = dataInfo.FromJson<VinattiViewTransDto>();
                    responseMessage.ReceiverType = objData.PrepaidPostpaid;
                    responseMessage.ProviderResponseTransCode = objData.RetRefNumber;
                    if (objData.Status == "A")
                    {
                        responseMessage.ResponseCode = ResponseCodeConst.Success;
                        responseMessage.ResponseMessage = "Thành công";
                        responseMessage.ProviderResponseMessage = "Thành công";
                    }
                    else if (objData.Status is "C" or "E")
                    {
                        responseMessage.ResponseCode = ResponseCodeConst.Error;
                        responseMessage.ResponseMessage = "Giao dịch thất bại";
                        responseMessage.ProviderResponseMessage = "Thất bại";
                    }
                    else
                    {
                        responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                        responseMessage.ResponseMessage = "Giao dịch chưa có kết quả";
                        responseMessage.ProviderResponseMessage = "Chưa có kết quả";
                    }

                    responseMessage.ProviderResponseCode = objData.Status;

                }
                else if (result.Code is "96" or "30" or "100")
                {
                    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                    responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                }
                else
                {
                    var reResult = await TopupGatewayService.GetResponseMassageCacheAsync(ProviderConst.VINATTI, result.Code, transCodeToCheck);
                    responseMessage.ResponseCode = reResult != null ? reResult.ResponseCode : ResponseCodeConst.ResponseCode_WaitForResult;
                    responseMessage.ResponseMessage = reResult != null ? reResult.ResponseName : "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";

                    responseMessage.ProviderResponseCode = result?.Code;
                    responseMessage.ProviderResponseMessage = result?.Message;
                }


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
                var dto = new VinattiBalanceDto
                {
                    PartnerCode = providerInfo.ApiUser,
                    UserName = providerInfo.Username,
                    Password = providerInfo.Password,
                    TransRefNumber = transCode,
                };
                var dataItem = dto.ToJson();
                string encrypted = Encrypt(dataItem, providerInfo.PublicKey);
                string sign = Sign(encrypted, "./" + providerInfo.PrivateKeyFile);
                var request = new VinattiRequest
                {
                    Code = "CHECK_BALANCE",
                    Time = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"),
                    Data = encrypted,
                    Signature = sign,
                };

                var jsonData = request.ToJson();
                logger.LogInformation($"{providerCode} VinattiConnector balancer_Json: {dataItem}");
                var result = await CallApi(providerInfo, request.Code, transCode, jsonData);
                logger.LogInformation($"{providerCode} VinattiConnector balance reponse: {result.ToJson()}");

                if (result.Code == "00")
                {
                    var dataInfo = Decrypt(result.Data, providerInfo.PublicKeyFile, providerInfo.ApiPassword);
                    logger.LogInformation($"{providerCode} - CheckBalance - VinattiConnector - data-reponse : {dataInfo}");
                    var balance = dataInfo.FromJson<VinattiViewBalanceDto>();
                    responseMessage.ResponseMessage = "Thành công";
                    responseMessage.ResponseCode = ResponseCodeConst.Success;
                    responseMessage.Payload = balance.Balance;
                }
                else
                {
                    responseMessage.ResponseMessage = "Không thành công";
                    responseMessage.ResponseCode = ResponseCodeConst.Error;
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"{providerCode} VinattiConnector CheckBalanceAsync exception : {ex}");
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
                logger.LogInformation($"{transCode} - Func = {function} - CallApi_VinattiConnector - result: {result.ToJson()}");
                return result.ConvertTo<VinattiResponse>();
            }
            catch (Exception ex)
            {
                logger.LogError($"{transCode} - Func = {function} - CallApi_VinattiConnector - exception : {ex.Message}");
                return new VinattiResponse()
                {
                    Code = "501102",
                    Message = "Chưa có kết quả"
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
            else if (privateKeyBlocks[0] == "BEGIN RSA PRIVATE KEY")
                rsa.ImportRSAPrivateKey(privateKeyBytes, out _);

            var sig = rsa.SignData(
                Encoding.ASCII.GetBytes(dataToSign),
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            var signature = Convert.ToBase64String(sig);

            return signature;
        }

        private static string Encrypt_AES_RSA(string plainText, byte[] keyBytes, AsymmetricKeyParameter public_key, string aes_algorithm)
        {
            byte[] plainTextData = null;
            byte[] iv = null;
            byte[] enSecretKey = null;
            byte[] iv_encrypt_data = null;
            byte[] encrypt_data_result = null;
            try
            {
                iv = new byte[IV_SIZE];
                plainTextData = Encoding.UTF8.GetBytes(plainText);
                var random = new SecureRandom();
                random.NextBytes(iv);

                // Tạo secretkey AES để mã hóa
                var secretKeySpec = ParameterUtilities.CreateKeyParameter("AES", keyBytes, 0, keyBytes.Length);
                ICipherParameters aesKeyParam = null;
                if (aes_algorithm.Equals(AES_Algorithm.ALGORITHM_AES_GCM_NoPadding))
                    aesKeyParam = CreateAeadParameters(secretKeySpec, iv);
                else
                    aesKeyParam = CreateParametersWithIV(secretKeySpec, iv);

                // Mã hóa plainText với AES
                var aes_cipher = CipherUtilities.GetCipher(aes_algorithm);
                aes_cipher.Init(true, aesKeyParam);
                var encrypt_data = aes_cipher.DoFinal(plainTextData);

                // Kết hợp IV với dữ liệu được mã hóa (IV_Data)
                iv_encrypt_data = new byte[IV_SIZE + encrypt_data.Length];
                Array.Copy(iv, 0, iv_encrypt_data, 0, IV_SIZE);
                Array.Copy(encrypt_data, 0, iv_encrypt_data, IV_SIZE, encrypt_data.Length);

                // Mã hóa secretkey với thuật toán RSA 
                enSecretKey = EncryptRSA(secretKeySpec.GetKey(), public_key);

                // Kết hợp secretkey với IV_Data
                encrypt_data_result = new byte[enSecretKey.Length + iv_encrypt_data.Length];
                Array.Copy(enSecretKey, 0, encrypt_data_result, 0, enSecretKey.Length);
                Array.Copy(iv_encrypt_data, 0, encrypt_data_result, enSecretKey.Length, iv_encrypt_data.Length);

                return Convert.ToBase64String(encrypt_data_result);
            }
            finally
            {
                if (plainTextData != null) Array.Clear(plainTextData, 0, plainTextData.Length);
                if (iv != null) Array.Clear(iv, 0, iv.Length);
                if (enSecretKey != null) Array.Clear(enSecretKey, 0, enSecretKey.Length);
                if (iv_encrypt_data != null) Array.Clear(iv_encrypt_data, 0, iv_encrypt_data.Length);
                if (encrypt_data_result != null) Array.Clear(encrypt_data_result, 0, encrypt_data_result.Length);

                plainTextData = null;
                iv = null;
                enSecretKey = null;
                iv_encrypt_data = null;
                encrypt_data_result = null;
            }
        }

        private static string Encrypt(string plainText, string certPath, string aes_algorithm = AES_Algorithm.ALGORITHM_AES_GCM_NoPadding)
        {
            SecureRandom sec_rand;
            AsymmetricKeyParameter public_key = null;
            CipherKeyGenerator key_general = null;
            try
            {
                public_key = GetPublicKey("files/" + certPath);
                sec_rand = new SecureRandom();
                key_general = GeneratorUtilities.GetKeyGenerator("AES");
                key_general.Init(new KeyGenerationParameters(sec_rand, 256));
                return Encrypt_AES_RSA(plainText, key_general.GenerateKey(), public_key, aes_algorithm);
            }
            catch (Exception ex)
            {
                throw new Exception("Encrypt have error:" + ex.Message);
            }
            finally
            {
                sec_rand = null;
                public_key = null;
                key_general = null;
            }
        }

        private static byte[] EncryptRSA(byte[] data, AsymmetricKeyParameter pub_key)
        {
            IAsymmetricBlockCipher eng = new Pkcs1Encoding(new RsaEngine());
            eng.Init(true, pub_key);
            return eng.ProcessBlock(data, 0, data.Length);
        }
        private static ICipherParameters CreateParametersWithIV(KeyParameter keySpec, byte[] iv)
        {
            return new ParametersWithIV(keySpec, iv);
        }

        private static ICipherParameters CreateAeadParameters(KeyParameter keySpec, byte[] iv)
        {
            return new AeadParameters(keySpec, iv.Length * 8, iv);
        }

        private static AsymmetricKeyParameter GetPublicKey(string certPath)
        {
            return DotNetUtilities.FromX509Certificate(
                X509Certificate.CreateFromCertFile(certPath)
            ).GetPublicKey();
        }

        private static string Decrypt(string encrypt_data, string cert_private_key, string password, string aes_algorithm = AES_Algorithm.ALGORITHM_AES_GCM_NoPadding)
        {
            AsymmetricKeyParameter private_key = null;
            try
            {
                X509Certificate2 cert = new X509Certificate2("files/" + cert_private_key, password);
                private_key = GetPrivateKey("files/" + cert_private_key, password);
                return Decrypt_AES_RSA(encrypt_data, private_key, aes_algorithm);
            }
            catch (Exception ex)
            {
                throw new Exception("Encrypt have error:" + ex.Message);
            }
            finally
            {
                private_key = null;
            }
        }

        private static AsymmetricKeyParameter GetPrivateKey(string keyStorePath, string password)
        {
            Pkcs12Store pkcs12 = new Pkcs12Store(new FileStream(keyStorePath, FileMode.Open, FileAccess.Read), password.ToArray());
            string keyAlias = null;

            foreach (string name in pkcs12.Aliases)
            {
                if (pkcs12.IsKeyEntry(name))
                {
                    keyAlias = name;
                    break;
                }
            }
            return pkcs12.GetKey(keyAlias).Key;
        }

        private static string Decrypt_AES_RSA(string encryptText, AsymmetricKeyParameter pri_key, string aes_algorithm)
        {
            var keyLength = 0;
            byte[] encrypted_source = null;
            byte[] encrypted_secretKey = null;
            byte[] iv = null;
            byte[] encrypted_data = null;
            byte[] decrypted_data = null;
            IBufferedCipher aes_cipher = null;
            ICipherParameters aesKeyParam = null;
            try
            {
                keyLength = GetKeyLength(pri_key) / 8;
                encrypted_source = Convert.FromBase64String(encryptText);

                // Tách secretkey đã mã hóa RSA
                encrypted_secretKey = new byte[keyLength];
                Array.Copy(encrypted_source, 0, encrypted_secretKey, 0, keyLength);

                // Tách IV
                iv = new byte[IV_SIZE];
                Array.Copy(encrypted_source, keyLength, iv, 0, iv.Length);

                // Tách plainText đã mã hóa AES               
                encrypted_data = new byte[encrypted_source.Length - (keyLength + IV_SIZE)];
                Array.Copy(encrypted_source, keyLength + IV_SIZE, encrypted_data, 0, encrypted_data.Length);

                // giải mã secretkey
                byte[] descryptedSecretKey = DecryptRSA(encrypted_secretKey, pri_key);
                var secretKeySpec = ParameterUtilities.CreateKeyParameter("AES", descryptedSecretKey, 0, descryptedSecretKey.Length);

                if (aes_algorithm.Equals(AES_Algorithm.ALGORITHM_AES_GCM_NoPadding))
                    aesKeyParam = CreateAeadParameters(secretKeySpec, iv);
                else
                    aesKeyParam = CreateParametersWithIV(secretKeySpec, iv);

                // Giải mã plainText
                //var aesKeyParam = new ParametersWithIV(secretKeySpec, iv);
                aes_cipher = CipherUtilities.GetCipher(aes_algorithm);
                aes_cipher.Init(false, aesKeyParam);
                decrypted_data = aes_cipher.DoFinal(encrypted_data);

                return Encoding.UTF8.GetString(decrypted_data);
            }
            catch (Exception ex)
            {
                return "";
            }
            finally
            {
                if (encrypted_source != null) Array.Clear(encrypted_source, 0, encrypted_source.Length);
                if (encrypted_secretKey != null) Array.Clear(encrypted_secretKey, 0, encrypted_secretKey.Length);
                if (iv != null) Array.Clear(iv, 0, iv.Length);
                if (encrypted_data != null) Array.Clear(encrypted_data, 0, encrypted_data.Length);
                if (decrypted_data != null) Array.Clear(decrypted_data, 0, decrypted_data.Length);

                aes_cipher = null;
                aesKeyParam = null;

                encrypted_source = null;
                encrypted_secretKey = null;
                iv = null;
                encrypted_data = null;
                decrypted_data = null;
            }
        }

        private static byte[] DecryptRSA(byte[] data, AsymmetricKeyParameter pri_key)
        {
            IAsymmetricBlockCipher eng = new Pkcs1Encoding(new RsaEngine());
            eng.Init(false, pri_key);
            return eng.ProcessBlock(data, 0, data.Length);
        }

        private static int GetKeyLength(AsymmetricKeyParameter pub_key)
        {
            return ((RsaKeyParameters)pub_key).Modulus.BitLength;
        }

    }

    internal class VinattiRequest
    {
        [DataMember(Name = "Code")]
        public string Code { get; set; }

        [DataMember(Name = "Data")]
        public string Data { get; set; }

        [DataMember(Name = "Time")]
        public string Time { get; set; }

        [DataMember(Name = "Signature")]
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
        [DataMember(Name = "PartnerCode")]
        public string PartnerCode { get; set; }

        [DataMember(Name = "UserName")]
        public string UserName { get; set; }

        [DataMember(Name = "Password")]
        public string Password { get; set; }

        [DataMember(Name = "RefNumber")]
        public string RefNumber { get; set; }

        [DataMember(Name = "Amount")]
        public int Amount { get; set; }

        [DataMember(Name = "TransAmount")]
        public int TransAmount { get; set; }

        [DataMember(Name = "TransRefNumber")]
        public string TransRefNumber { get; set; }

        [DataMember(Name = "Details")]
        public string Details { get; set; }

        [DataMember(Name = "ReceiverMobileNumber")]
        public string ReceiverMobileNumber { get; set; }

        [DataMember(Name = "OrderID")]
        public string OrderID { get; set; }

        [DataMember(Name = "NetworkHomeID")]
        public int NetworkHomeID { get; set; }
    }

    internal class VinattiTransDto
    {
        [DataMember(Name = "PartnerCode")]
        public string PartnerCode { get; set; }

        [DataMember(Name = "UserName")]
        public string UserName { get; set; }

        [DataMember(Name = "Password")]
        public string Password { get; set; }

        [DataMember(Name = "TransRefNumber")]
        public string TransRefNumber { get; set; }
    }

    internal class VinattiBalanceDto
    {
        [DataMember(Name = "PartnerCode")]
        public string PartnerCode { get; set; }

        [DataMember(Name = "UserName")]
        public string UserName { get; set; }

        [DataMember(Name = "Password")]
        public string Password { get; set; }

        [DataMember(Name = "TransRefNumber")]
        public string TransRefNumber { get; set; }
    }

    internal class VinattiViewTransDto
    {
        public string TransRefNumber { get; set; }

        public string RetRefNumber { get; set; }

        public string PrepaidPostpaid { get; set; }

        public string Status { get; set; }
    }

    internal class VinattiViewBalanceDto
    {
        public decimal Balance { get; set; }
    }

    public class AES_Algorithm
    {
        public const string ALGORITHM_AES_CBC_PKCS5Padding = "AES/CBC/PKCS5Padding";
        public const string ALGORITHM_AES_GCM_NoPadding = "AES/GCM/NoPadding";
    }
}
