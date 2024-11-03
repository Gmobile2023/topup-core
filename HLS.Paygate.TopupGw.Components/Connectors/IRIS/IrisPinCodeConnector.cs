using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using HLS.Paygate.Shared;
using HLS.Paygate.Shared.Dtos;
using HLS.Paygate.Shared.Utils;
using HLS.Paygate.TopupGw.Contacts.Dtos;
using HLS.Paygate.TopupGw.Contacts.Enums;
using HLS.Paygate.TopupGw.Domains.BusinessServices;
using Microsoft.Extensions.Logging;
using ServiceStack;


namespace HLS.Paygate.TopupGw.Components.Connectors.IRIS
{
    public class IrisPinCodeConnector : GatewayConnectorBase
    {
        private readonly ILogger<IrisPinCodeConnector> _logger;
        private readonly ITopupGatewayService _topupGatewayService;

        public IrisPinCodeConnector(ITopupGatewayService topupGatewayService, ILogger<IrisPinCodeConnector> logger) :
            base(
                topupGatewayService)
        {
            _logger = logger;
            _topupGatewayService = topupGatewayService;
        }

        public override Task<MessageResponseBase> TopupAsync(TopupRequestLogDto topupRequestLog,
            ProviderInfoDto providerInfo)
        {
            throw new NotImplementedException();
        }

        public override async Task<MessageResponseBase> TransactionCheckAsync(string providerCode,
            string transCodeToCheck,
            string transCode, string serviceCode = null,
            ProviderInfoDto providerInfo = null)
        {
            _logger.LogInformation("{TransCodeToCheck} IrisConnector check request: {TransCode}", transCodeToCheck,
                transCode);
            var responseMessage = new MessageResponseBase();
            try
            {
                providerInfo ??= await TopupGatewayService.ProviderInfoCacheGetAsync(providerCode);


                if (providerInfo == null ||
                    !TopupGatewayService.ValidConnector(ProviderConst.IRIS_PINCODE, providerInfo.ProviderCode))
                {
                    _logger.LogError("{TransCode}-{ProviderCode}- IrisPinCodeConnector ProviderConnector not valid", transCode,
                        providerCode);
                    return new MessageResponseBase
                    {
                        ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult,
                        ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ"
                    };
                }

                _logger.LogInformation("Trans Check object send: {TransCode}", transCodeToCheck);
                var plainText = string.Join("", providerInfo.ApiUser, transCodeToCheck);
                string signature = Sign(plainText, providerInfo.PrivateKeyFile);
                var checkTransUrl = providerInfo.ApiUrl +
                                    $"/softpin?userId={providerInfo.ApiUser}&transactionId={transCodeToCheck}&signature={signature}";

                GetResponseObject result = null;
                var client = new JsonServiceClient();
                client.Timeout = TimeSpan.FromSeconds(providerInfo.Timeout);
                try
                {
                    result = await client.GetAsync<GetResponseObject>(checkTransUrl);
                }
                catch (Exception e)
                {
                    _logger.LogError($"{transCodeToCheck} Check trans fail: {e.Message}");
                    result = new GetResponseObject
                    {
                        Code = "501102",
                        Message = e.Message,
                    };
                }

                if (result != null)
                {
                    _logger.LogInformation(
                        $"{providerCode}-{transCodeToCheck}  IrisPinCodeConnector Check trans return: {transCodeToCheck}-{transCode}-{result.ToJson()}");

                    if (result.Code == "200")
                    {
                        var ignoreCode = (providerInfo.IgnoreCode ?? string.Empty).Split(';', ',', '|');
                        if (result.Data.SoftpinResult.Code == "00")
                        {
                            responseMessage.ResponseCode = ResponseCodeConst.Success;
                            responseMessage.ResponseMessage = "Giao dịch thành công";
                            responseMessage.ProviderResponseTransCode = string.Empty;
                            try
                            {
                                var cardList = result.Data.SoftpinResult.Softpins.Select(card =>
                                    new CardRequestResponseDto
                                    {
                                        CardValue = string.Empty,
                                        CardCode = card.PinCode,
                                        Serial = card.Serial,
                                        ExpireDate = DateTime.ParseExact(card.ExpiryDate, "yyyy/MM/dd HH:mm:ss",
                                                CultureInfo.InvariantCulture).ToString("dd/MM/yyyy"),
                                        ExpiredDate = DateTime.ParseExact(card.ExpiryDate, "yyyy/MM/dd HH:mm:ss",
                                                CultureInfo.InvariantCulture)
                                    }).ToList();

                                cardList = GenDecryptListCode(cardList, providerInfo, transCode, isTripDes: true);
                                responseMessage.Payload = cardList;
                            }
                            catch (Exception e)
                            {
                                _logger.LogError(
                                    $"transCodeToCheck= {transCodeToCheck} Error parsing cards: {e.Message}");
                            }
                        }
                        else if (ignoreCode.Contains(result.Data.SoftpinResult.Code))
                        {
                            responseMessage.ResponseCode = ResponseCodeConst.Error;
                            responseMessage.ResponseMessage = "Giao dịch thất bại";
                            responseMessage.ProviderResponseCode = result.Data.SoftpinResult.Code;
                            responseMessage.ProviderResponseMessage = "Giao dịch thất bại";
                        }
                        else
                        {
                            responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                            responseMessage.ResponseMessage = "Giao dịch Chưa có kết quá";
                            responseMessage.ProviderResponseCode = result.Data.SoftpinResult.Code;
                            responseMessage.ProviderResponseMessage = "Giao dịch chưa có kết quả";
                        }
                    }
                    else
                    {
                        responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                        responseMessage.ResponseMessage = "Giao dịch chưa có kết quả";
                        responseMessage.ProviderResponseCode = result?.Code;
                        responseMessage.ProviderResponseMessage = result?.Message;
                    }
                }
                else
                {
                    _logger.LogInformation("{TransCodeToCheck} Error send request", transCodeToCheck);
                    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                    responseMessage.ResponseMessage =
                        "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                }
            }
            catch (Exception e)
            {
                _logger.LogError($"{transCodeToCheck} CheckTrans error {e}");
                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                responseMessage.ResponseMessage =
                    "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
            }

            return responseMessage;
        }


        public override async Task<MessageResponseBase> CardGetByBatchAsync(CardRequestLogDto cardRequestLog)
        {
            var responseMessage = new MessageResponseBase();
            try
            {
                _logger.LogInformation("{TransCode} IrisPinCodeConnector topup request: {Log}",
                    cardRequestLog.TransCode,
                    cardRequestLog.ToJson());

                var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(cardRequestLog.ProviderCode);

                if (providerInfo == null)
                    return responseMessage;
                if (!_topupGatewayService.ValidConnector(ProviderConst.IRIS_PINCODE, providerInfo.ProviderCode))
                {
                    _logger.LogError(
                        $"{cardRequestLog.TransCode}-{cardRequestLog.TransRef}-{cardRequestLog.ProviderCode} - IrisPinCodeConnector ProviderConnector not valid");
                    return new MessageResponseBase
                    {
                        ResponseCode = ResponseCodeConst.Error,
                        ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
                    };
                }

                var product = providerInfo.ProviderServices.Find(p => p.ProductCode == cardRequestLog.ProductCode);
                if (product == null)
                {
                    _logger.LogError(
                        $"{cardRequestLog.TransCode}-{cardRequestLog.TransRef}-{cardRequestLog.ProviderCode} - IrisPinCodeConnector ProviderConnector not config productCode= {cardRequestLog.ProductCode}");
                    return new MessageResponseBase
                    {
                        ResponseCode = ResponseCodeConst.Error,
                        ResponseMessage = "Giao dịch lỗi. Chưa cấu hình sản phẩm"
                    };
                }

                var request = new PinCodeObject
                {
                    ProductCode = product.ServiceCode,
                    Quantity = cardRequestLog.Quantity,
                    TransactionId = cardRequestLog.TransCode,
                    UserId = providerInfo.ApiUser,
                };
                var plainText = string.Join("", request.UserId, request.TransactionId, request.ProductCode,
                    request.Quantity);
                request.Signature = Sign(plainText, providerInfo.PrivateKeyFile);
                responseMessage.TransCodeProvider = cardRequestLog.TransCode;
                _logger.LogInformation("{TransCode} IrisPinCodeConnector send: {Data}", cardRequestLog.TransCode,
                    request.ToJson());
                var result = await CallApi(providerInfo.ApiUrl + "/softpin/getsoftpin", request.ToJson(),
                    cardRequestLog.TransCode);
                _logger.LogInformation("IrisPinCodeConnector CallApi return: {TransCode} - {TransRef} - {Return}",
                    cardRequestLog.TransCode,
                    cardRequestLog.TransRef, result.ToJson());


                if (result != null)
                {
                    cardRequestLog.ModifiedDate = DateTime.Now;
                    responseMessage.ProviderResponseCode = result.Data != null ? result.Data.Code : string.Empty;
                    responseMessage.ProviderResponseMessage = result.Data != null ? result.Data.Message : string.Empty;
                    if (result.Code == "200")
                    {
                        if (result.Data.Code == "00")
                        {
                            cardRequestLog.Status = TransRequestStatus.Success;
                            responseMessage.ProviderResponseTransCode = string.Empty;
                            responseMessage.ResponseCode = ResponseCodeConst.Success;
                            responseMessage.ResponseMessage = "Giao dịch thành công";
                            try
                            {
                                var cardList = result.Data.Softpins.Select(card => new CardRequestResponseDto
                                {
                                    CardType = cardRequestLog.Vendor,
                                    CardValue = (int.Parse(cardRequestLog.ProductCode.Split('_')[2]) * 1000).ToString(),
                                    CardCode = card.PinCode,
                                    Serial = card.Serial,
                                    ExpireDate = DateTime.ParseExact(card.ExpiryDate, "yyyy/MM/dd",
                                        CultureInfo.InvariantCulture).ToString("dd/MM/yyyy"),
                                    ExpiredDate = DateTime.ParseExact(card.ExpiryDate, "yyyy/MM/dd",
                                        CultureInfo.InvariantCulture)
                                }).ToList();

                                cardList = GenDecryptListCode(cardList, providerInfo, cardRequestLog.TransCode);
                                responseMessage.Payload = cardList;
                            }
                            catch (Exception e)
                            {
                                _logger.LogError(
                                    $"TransCode= {cardRequestLog.TransCode} IrisPinCodeConnector Error parsing cards: " +
                                    e.Message);
                            }
                        }
                        else
                        {
                            var extraInfo = (providerInfo.ExtraInfo ?? string.Empty).Split(';', ';', '|');
                            var reResult = await TopupGatewayService.GetResponseMassageCacheAsync(
                              ProviderConst.IRIS_PINCODE,
                              result.Data.Code, cardRequestLog.TransCode);
                            if (result.Data != null && extraInfo.Contains(result.Data.Code))
                            {
                                cardRequestLog.Status = TransRequestStatus.Fail;
                                responseMessage.ResponseCode = ResponseCodeConst.Error;
                                responseMessage.ResponseMessage = reResult != null ? reResult.ReponseName : result.Data.Message;
                            }
                            else
                            {
                                cardRequestLog.Status = TransRequestStatus.Timeout;
                                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                                responseMessage.ResponseMessage = reResult != null ? reResult.ReponseName : "Giao dịch đang chờ kết quả xử lý.";
                            }
                        }
                    }
                    else
                    {
                        var reResult = await TopupGatewayService.GetResponseMassageCacheAsync(
                            ProviderConst.IRIS_PINCODE,
                            result.Code, cardRequestLog.TransCode);
                        cardRequestLog.Status = TransRequestStatus.Timeout;
                        responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                        responseMessage.ResponseMessage =
                            reResult != null ? reResult.ReponseName : "Giao dịch đang chờ kết quả xử lý.";
                    }
                }
                else
                {
                    _logger.LogInformation($"{cardRequestLog.TransCode} Error send request");
                    responseMessage.ResponseMessage = "Lỗi kết nối nhà cung cấp";
                    cardRequestLog.Status = TransRequestStatus.Fail;
                }

                await _topupGatewayService.CardRequestLogUpdateAsync(cardRequestLog);
            }
            catch (Exception e)
            {
                _logger.LogError($"TransCode= {cardRequestLog.TransCode} IRIS_PINCODE Error: " + e.Message);
                cardRequestLog.Status = TransRequestStatus.Timeout;
                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
            }

            return responseMessage;
        }

        public override async Task<MessageResponseBase> CheckBalanceAsync(string providerCode, string transCode)
        {
            _logger.LogInformation("{TransCode} Get balance request", transCode);
            var responseMessage = new MessageResponseBase();
            var providerInfo = await TopupGatewayService.ProviderInfoCacheGetAsync(providerCode);

            if (providerInfo == null)
                return responseMessage;

            if (!TopupGatewayService.ValidConnector(ProviderConst.IRIS_PINCODE, providerInfo.ProviderCode))
            {
                _logger.LogError("{ProviderCode}-{TransCode}-IrisPinCodeConnector ProviderConnector not valid", providerCode,
                    transCode);
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error,
                    ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
                };
            }

            var data = new IRISBalanceRequestObject()
            {
                UserId = providerInfo.ApiUser,
                Username = providerInfo.Username,
                Password = providerInfo.Password
            };

            var resCodeInfo = providerInfo.ExtraInfo;
            _logger.LogInformation("{TransCode} Balance object send: {Data}", transCode, data.ToJson());
            var result = await CallApi(providerInfo.ApiUrl + "/topup/balance", data.ToJson(), transCode);

            if (result != null)
            {
                _logger.LogInformation($"{transCode} Balance return: {transCode}-{result.ToJson()}");
                if (result.Code == "00" || result.Code == "0")
                {
                    responseMessage.ResponseCode = ResponseCodeConst.Success;
                    responseMessage.ResponseMessage = "Giao dịch thành công";
                    responseMessage.Payload = result.Data.Balance.ToString(CultureInfo.InvariantCulture);
                }
                else if (resCodeInfo.Contains(result.Code))
                {
                    var reResult =
                        await TopupGatewayService.GetResponseMassageCacheAsync(ProviderConst.IRIS_PINCODE, result.Code,
                            transCode);
                    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                    responseMessage.ResponseMessage =
                        reResult != null
                            ? reResult.ReponseName
                            : "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                }
                else
                {
                    var reResult =
                        await TopupGatewayService.GetResponseMassageCacheAsync(ProviderConst.IRIS_PINCODE, result.Code,
                            transCode);
                    responseMessage.ResponseCode = reResult != null ? reResult.ReponseCode : ResponseCodeConst.Error;
                    responseMessage.ResponseMessage = reResult != null ? reResult.ReponseName : result.Message;
                }
            }
            else
            {
                _logger.LogInformation("{TransCode} IrisPinCodeConnector Error send request", transCode);
                responseMessage.ResponseMessage = "Lỗi kết nối nhà cung cấp";
            }

            return responseMessage;
        }

        private async Task<ResponseObject> CallApi(string url, string jsonRequest, string transCode)
        {
            try
            {
                string responseString;
                var exception = string.Empty;
                var retryCount = 0;
                do
                {
                    try
                    {
                        var clientHandler = new HttpClientHandler();
                        clientHandler.ServerCertificateCustomValidationCallback =
                            (sender, cert, chain, sslPolicyErrors) => true;

                        responseString = await url.PostStringToUrlAsync(jsonRequest, "application/json");
                        _logger.LogInformation("IRIS IrisPinCodeConnector response {TransCode} - {ResponseString}", transCode,
                            responseString);
                        retryCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"{transCode} IrisPinCodeConnector exception: {ex.Message}");
                        exception = ex.Message;
                        responseString = "TIMEOUT";
                    }
                } while (string.IsNullOrEmpty(responseString) && retryCount < 3);

                if (!string.IsNullOrEmpty(responseString))
                {
                    if (responseString == "TIMEOUT")
                        return new ResponseObject
                        {
                            Code = "501102",
                            Message = exception
                        };

                    var responseMessage = responseString.FromJson<ResponseObject>();
                    return responseMessage;
                }

                return new ResponseObject
                {
                    Code = "501102",
                    Message = "Send request timeout!"
                };
            }
            catch (Exception e)
            {
                _logger.LogError($"{transCode} IRIS PINCODE callapi error {e}");
                return new ResponseObject
                {
                    Code = "501102",
                    Message = e.Message
                };
            }
        }

        private string Sign(string dataToSign, string privateFile)
        {
            var privateKeyText = File.ReadAllText("files/" + privateFile);
            var privateKeyBlocks = privateKeyText.Split("-", StringSplitOptions.RemoveEmptyEntries);
            var str = privateKeyBlocks[1].Replace("\r\n", "");
            var privateKeyBytes = Convert.FromBase64String(str);
            using var rsa = RSA.Create();

            if (privateKeyBlocks[0] == "BEGIN PRIVATE KEY")
                rsa.ImportPkcs8PrivateKey(privateKeyBytes, out _);

            else if (privateKeyBlocks[0] == "BEGIN RSA PRIVATE KEY") rsa.ImportRSAPrivateKey(privateKeyBytes, out _);
            var sig = rsa.SignData(
                Encoding.UTF8.GetBytes(dataToSign),
                HashAlgorithmName.SHA1,
                RSASignaturePadding.Pkcs1);
            var signature = Convert.ToBase64String(sig);
            return signature;
        }

        private string DecryptString(string transCode, string inputString, string privateKeyPem)
        {
            try
            {
                const int keySize = 1024;
                using var rsa = new RSACryptoServiceProvider(keySize);
                var pemKey = File.ReadAllText("files/" + privateKeyPem);
                rsa.ImportFromPem(pemKey);
                const int base64BlockSize = (keySize / 8 / 3) * 4 + 4;
                var iterations = inputString.Length / base64BlockSize;
                var arrayList = new ArrayList();
                for (var i = 0; i < iterations; i++)
                {
                    var encryptedBytes =
                        Convert.FromBase64String(inputString.Substring(base64BlockSize * i, base64BlockSize));
                    Array.Reverse(encryptedBytes);
                    arrayList.AddRange(rsa.Decrypt(encryptedBytes, false));
                }

                var result = Encoding.UTF8.GetString(arrayList.ToArray(Type.GetType("System.Byte")) as byte[]);
                return result;
            }
            catch (Exception e)
            {
                _logger.LogError($"{transCode} IrisPinCodeConnector DecryptString error {e}");
                return inputString;
            }
        }
        private List<CardRequestResponseDto> GenDecryptListCode(List<CardRequestResponseDto> cardList,
            ProviderInfoDto providerInfoDto, string transCode, bool isTripDes = false)
        {
            try
            {
                foreach (var item in cardList)
                {
                    item.CardCode = DecryptString(transCode, item.CardCode, providerInfoDto.PrivateKeyFile);
                    item.CardCode = isTripDes ? item.CardCode.EncryptTripDes() : item.CardCode;
                }

                return cardList;
            }
            catch (Exception ex)
            {
                _logger.LogError($"{transCode} GenDecryptListCode exception: {ex.Message}");
                return cardList;
            }
        }

        private class PinCodeObject
        {
            public string UserId { get; set; }
            public string TransactionId { get; set; }
            public string ProductCode { get; set; }
            public int Quantity { get; set; }
            public string Signature { get; set; }
        }

        private class ResponseObject
        {
            public string Code { get; set; }
            public string Message { get; set; }
            public PinData Data { get; set; }
        }

        private class GetResponseObject
        {
            public string Code { get; set; }
            public string Message { get; set; }
            public IrisGetPinData Data { get; set; }
        }

        private class PinData
        {
            public string Code { get; set; }
            public string Message { get; set; }
            public decimal Balance { get; set; }
            public List<IrisSoftpin> Softpins { get; set; }
        }

        private class IrisGetPinData
        {
            public string Code { get; set; }
            public string Message { get; set; }
            public string TransactionId { get; set; }
            public PinData SoftpinResult { get; set; }
        }

        private class IrisSoftpin
        {
            public string Serial { get; set; }
            public string PinCode { get; set; }
            public string ExpiryDate { get; set; }
        }
    }
}