using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
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
using Microsoft.IdentityModel.Tokens;
using Nest;
using ServiceStack;

namespace HLS.Paygate.TopupGw.Components.Connectors.IRIS;

public class IrisConnector : GatewayConnectorBase
{
    private readonly ILogger<IrisConnector> _logger;

    public IrisConnector(ITopupGatewayService topupGatewayService, ILogger<IrisConnector> logger) : base(
        topupGatewayService)
    {
        _logger = logger;
    }

    public override async Task<MessageResponseBase> TopupAsync(TopupRequestLogDto topupRequestLog,
        ProviderInfoDto providerInfo)
    {
        _logger.LogInformation("{TransCode} IrisConnector topup request: {Log}", topupRequestLog.TransCode,
            topupRequestLog.ToJson());
        var responseMessage = new MessageResponseBase();
        try
        {
            if (!TopupGatewayService.ValidConnector(ProviderConst.IRIS, providerInfo.ProviderCode))
            {
                _logger.LogError(
                    "{TransCode}-{TransRef}-{ProviderCode}-IrisConnector ProviderConnector not valid",
                    topupRequestLog.TransCode, topupRequestLog.TransRef, providerInfo.ProviderCode);
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error,
                    ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
                };
            }

            var resCodeInfo = providerInfo.ExtraInfo;
            // var str = topupRequestLog.ProductCode.Split('_');
            // string keyCode = topupRequestLog.ProductCode.Contains("TOPUP")
            //     ? $"{str[0]}_{str[1]}"
            //     : topupRequestLog.ProductCode;
            // var providerService = providerInfo.ProviderServices.Find(p => p.ProductCode == keyCode);

            var telcoCode = topupRequestLog.Vendor switch
            {
                "VMS" => "Mobifone",
                "VNA" => "Vinaphone",
                "VTE" => "Viettel",
                "VNM" => "Vietnamobile",
                "GMOBILE" => "GTel",
                "WT" => "Reddi",
                _ => string.Empty
            };

            var data = new TopupRequestObject
            {
                UserId = providerInfo.ApiUser,
                Amount = topupRequestLog.TransAmount,
                TargetNumber = topupRequestLog.ReceiverInfo,
                TraceNumber = topupRequestLog.TransCode,
                Telco = telcoCode
            };

            var plainText = string.Join("", data.UserId, data.TraceNumber, data.TargetNumber, data.Amount, data.Telco);

            data.Signature = Sign(plainText, providerInfo.PrivateKeyFile);
            //data.Signature = SignBackUp(plainText, providerInfo.PrivateKeyFile);
            responseMessage.TransCodeProvider = topupRequestLog.TransCode;
            _logger.LogInformation("{TransCode} IrisConnector send: {Data}", topupRequestLog.TransCode, data.ToJson());
            var result = await CallApi(providerInfo.ApiUrl + "/topupv2", data.ToJson(), topupRequestLog.TransCode,
                providerInfo.Timeout);
            _logger.LogInformation("IrisConnector CallApi return: {TransCode}-{TransRef}-{Return}",
                topupRequestLog.TransCode,
                topupRequestLog.TransRef, result.ToJson());

            if (result != null)
            {
                topupRequestLog.ResponseInfo = result.ToJson();
                topupRequestLog.ModifiedDate = DateTime.Now;
                responseMessage.ProviderResponseCode = result.Data != null ? result.Data.Code : string.Empty;
                responseMessage.ProviderResponseMessage = result.Data != null ? result.Data.Message : string.Empty;
                _logger.LogInformation("{ProviderCode}{TransCode} IrisConnector return: {TransRef}-{Json}",
                    topupRequestLog.ProviderCode, topupRequestLog.TransCode, topupRequestLog.TransRef, result.ToJson());

                if (result.Code == "200")
                {
                    if (result.Data.Code == "00")
                    {
                        topupRequestLog.Status = TransRequestStatus.Success;
                        responseMessage.ProviderResponseTransCode = string.Empty;
                        responseMessage.ReceiverType = result.Data.MobileType;
                        responseMessage.ResponseCode = ResponseCodeConst.Success;
                        responseMessage.ResponseMessage = "Giao dịch thành công";
                    }
                    else if (result.Data != null && resCodeInfo.Split(";").Contains(result.Data.Code))
                    {
                        var reResult = await TopupGatewayService.GetResponseMassageCacheAsync(ProviderConst.IRIS,
                            result.Data.Code,
                            topupRequestLog.TransCode);
                        topupRequestLog.Status = TransRequestStatus.Fail;
                        responseMessage.ResponseCode =
                            reResult != null ? reResult.ReponseCode : ResponseCodeConst.Error;
                        responseMessage.ResponseMessage = reResult != null ? reResult.ReponseName : result.Data.Message;
                    }
                    else
                    {
                        // var reResult = await TopupGatewayService.GetResponseMassageCacheAsync(ProviderConst.IRIS,
                        //     result.Data.Code,
                        //     topupRequestLog.TransCode);
                        topupRequestLog.Status = TransRequestStatus.Timeout;
                        responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                        responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả xử lý.";
                    }
                }
                else
                {
                    var reResult = await TopupGatewayService.GetResponseMassageCacheAsync(ProviderConst.IRIS,
                        result.Code,
                        topupRequestLog.TransCode);
                    topupRequestLog.Status = TransRequestStatus.Timeout;
                    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                    responseMessage.ResponseMessage =
                        reResult != null ? reResult.ReponseName : "Giao dịch đang chờ kết quả xử lý.";
                }
            }
            else
            {
                _logger.LogInformation($"{topupRequestLog.TransCode} Error send request");
                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                topupRequestLog.Status = TransRequestStatus.Timeout;
            }

            await TopupGatewayService.TopupRequestLogUpdateAsync(topupRequestLog);
        }
        catch (Exception e)
        {
            _logger.LogError($"{topupRequestLog.TransCode} -{topupRequestLog.TransRef} Topup error {e}");
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

            var providerInfo = await TopupGatewayService.ProviderInfoCacheGetAsync(cardRequestLog.ProviderCode);

            if (providerInfo == null)
                return responseMessage;
            if (!TopupGatewayService.ValidConnector(ProviderConst.IRIS, providerInfo.ProviderCode))
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
                cardRequestLog.TransCode, providerInfo.Timeout);
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
                            //int keySize = string.IsNullOrEmpty(providerInfo.PublicKey) ? 2048 : Convert.ToInt32(providerInfo.PublicKey);
                            cardList = GenDecryptListCode(cardList, providerInfo, 2048);
                            responseMessage.Payload = cardList;
                        }
                        catch (Exception e)
                        {
                            _logger.LogError(
                                $"TransCode= {cardRequestLog.TransCode} IrisPinCodeConnector Error parsing cards: " +
                                e.Message);

                            cardRequestLog.Status = TransRequestStatus.Timeout;
                            responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                            responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả xử lý.";
                        }
                    }
                    else
                    {
                        var extraInfo = (providerInfo.ExtraInfo ?? string.Empty).Split(';', ';', '|');
                        var reResult = await TopupGatewayService.GetResponseMassageCacheAsync(
                            ProviderConst.IRIS,
                            result.Data.Code, cardRequestLog.TransCode);
                        if (result.Data != null && extraInfo.Contains(result.Data.Code))
                        {
                            cardRequestLog.Status = TransRequestStatus.Fail;
                            responseMessage.ResponseCode = ResponseCodeConst.Error;
                            responseMessage.ResponseMessage =
                                reResult != null ? reResult.ReponseName : result.Data.Message;
                        }
                        else
                        {
                            cardRequestLog.Status = TransRequestStatus.Timeout;
                            responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                            responseMessage.ResponseMessage = reResult != null
                                ? reResult.ReponseName
                                : "Giao dịch đang chờ kết quả xử lý.";
                        }
                    }
                }
                else
                {
                    var reResult = await TopupGatewayService.GetResponseMassageCacheAsync(
                        ProviderConst.IRIS,
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

            await TopupGatewayService.CardRequestLogUpdateAsync(cardRequestLog);
        }
        catch (Exception e)
        {
            _logger.LogError($"TransCode= {cardRequestLog.TransCode} IRIS_PINCODE Error: " + e.Message);
            cardRequestLog.Status = TransRequestStatus.Timeout;
            responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
        }

        return responseMessage;
    }

    public override async Task<MessageResponseBase> TransactionCheckAsync(string providerCode, string transCodeToCheck,
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
                !TopupGatewayService.ValidConnector(ProviderConst.IRIS, providerInfo.ProviderCode))
            {
                _logger.LogError("{TransCode}-{ProviderCode}- IrisConnector ProviderConnector not valid", transCode,
                    providerCode);
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult,
                    ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ"
                };
            }

            if (serviceCode.StartsWith("PIN"))
            {
                return await TransactionCheckPinCodeAsync(providerCode, transCodeToCheck, transCode, providerInfo);
            }

            _logger.LogInformation("Trans Check object send: {Trans}", transCodeToCheck);
            var checkTransUrl = providerInfo.ApiUrl + "/topup?userId=" + providerInfo.ApiUser + "&transactionId=" +
                                transCodeToCheck;

            ResponseObject result = null;
            var client = new JsonServiceClient();
            client.Timeout = TimeSpan.FromSeconds(providerInfo.Timeout);
            try
            {
                result = await client.GetAsync<ResponseObject>(checkTransUrl);
            }
            catch (Exception e)
            {
                _logger.LogError("Check trans fail: {Ex}", e.Message);
            }

            if (result != null)
            {
                _logger.LogInformation(
                    $"{providerCode}-{transCodeToCheck}  IrisConnector Check trans return: {transCodeToCheck}-{transCode}-{result.ToJson()}");

                //Chỗ này tài liệu iris k mô tả các mã lỗi code
                if (result.Code == "200")
                {
                    if (result.Data.TopupStatus == 9)
                    {
                        responseMessage.ResponseCode = ResponseCodeConst.Success;
                        responseMessage.ResponseMessage = "Giao dịch thành công";
                        responseMessage.ProviderResponseTransCode = string.Empty;
                        responseMessage.ReceiverType = result.Data.MobileType;
                    }
                    else if (result.Data.TopupStatus == 7)
                    {
                        responseMessage.ResponseCode = ResponseCodeConst.Error;
                        responseMessage.ResponseMessage = "Giao dịch thất bại";
                        responseMessage.ProviderResponseCode = result.Data.TopupStatus.ToString();
                        responseMessage.ProviderResponseMessage = "Giao dịch thất bại";
                    }
                    else
                    {
                        responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                        responseMessage.ResponseMessage = "Giao dịch Chưa có kết quá";
                        responseMessage.ProviderResponseCode = result.Status.ToString();
                        responseMessage.ProviderResponseMessage = "Giao dịch chưa có kết quả";
                    }
                }
                else if (result.Code == "404")
                {
                    responseMessage.ResponseCode = ResponseCodeConst.Error;
                    responseMessage.ResponseMessage = "Giao dịch không tồn tại bên NCC";
                    responseMessage.ProviderResponseCode = result?.Code;
                    responseMessage.ProviderResponseMessage = result?.Message;
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
                responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
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

    public override async Task<MessageResponseBase> CheckBalanceAsync(string providerCode, string transCode)
    {
        _logger.LogInformation("{TransCode} Get balance request", transCode);
        var responseMessage = new MessageResponseBase();
        var providerInfo = await TopupGatewayService.ProviderInfoCacheGetAsync(providerCode);

        if (providerInfo == null)
            return responseMessage;

        if (!TopupGatewayService.ValidConnector(ProviderConst.IRIS, providerInfo.ProviderCode))
        {
            _logger.LogError("{ProviderCode}-{TransCode}-IrisConnector ProviderConnector not valid", providerCode,
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
        var result = await CallApi(providerInfo.ApiUrl + "/topup/balance", data.ToJson(), transCode,
            providerInfo.Timeout);

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
                    await TopupGatewayService.GetResponseMassageCacheAsync(ProviderConst.IRIS, result.Code, transCode);
                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                responseMessage.ResponseMessage =
                    reResult != null
                        ? reResult.ReponseName
                        : "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
            }
            else
            {
                var reResult =
                    await TopupGatewayService.GetResponseMassageCacheAsync(ProviderConst.IRIS, result.Code, transCode);
                responseMessage.ResponseCode = reResult != null ? reResult.ReponseCode : ResponseCodeConst.Error;
                responseMessage.ResponseMessage = reResult != null ? reResult.ReponseName : result.Message;
            }
        }
        else
        {
            _logger.LogInformation("{TransCode} Error send request", transCode);
            responseMessage.ResponseMessage = "Lỗi kết nối nhà cung cấp";
        }

        return responseMessage;
    }

    private async Task<ResponseObject> CallApi(string url, string jsonRequest, string transCode, int timeout)
    {
        var responseString = string.Empty;
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
                _logger.LogInformation("IRIS callapi response {TransCode}-{ResponseString}", transCode,
                    responseString);
                retryCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError("Trans exception: {Ex}", ex.Message);
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

    private string DecryptString(string inputString, string privateKeyPem, int keySize)
    {
        using var rsa = new RSACryptoServiceProvider(keySize);
        var pemKey = File.ReadAllText("files/" + privateKeyPem);
        rsa.ImportFromPem(pemKey);
        int base64BlockSize = (keySize / 8 / 3) * 4 + 4;
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

    private List<CardRequestResponseDto> GenDecryptListCode(List<CardRequestResponseDto> cardList,
        ProviderInfoDto providerInfoDto, int keySize, bool isTripDes = false)
    {
        foreach (var item in cardList)
        {
            item.CardCode = DecryptString(item.CardCode, providerInfoDto.PrivateKeyFile, keySize);
            item.CardCode = isTripDes ? item.CardCode.EncryptTripDes() : item.CardCode;
        }

        return cardList;
    }


    private async Task<MessageResponseBase> TransactionCheckPinCodeAsync(string providerCode,
        string transCodeToCheck,
        string transCode,
        ProviderInfoDto providerInfo = null)
    {
        _logger.LogInformation("{TransCodeToCheck} IrisConnector check request: {TransCode}", transCodeToCheck,
            transCode);
        var responseMessage = new MessageResponseBase();
        try
        {
            providerInfo ??= await TopupGatewayService.ProviderInfoCacheGetAsync(providerCode);


            if (providerInfo == null ||
                !TopupGatewayService.ValidConnector(ProviderConst.IRIS, providerInfo.ProviderCode))
            {
                _logger.LogError("{TransCode}-{ProviderCode}- IrisPinCodeConnector ProviderConnector not valid",
                    transCode,
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

            ResponseObject result = null;
            var client = new JsonServiceClient();
            client.Timeout = TimeSpan.FromSeconds(providerInfo.Timeout);
            try
            {
                result = await client.GetAsync<ResponseObject>(checkTransUrl);
            }
            catch (Exception e)
            {
                _logger.LogError($"{transCodeToCheck} Check trans fail: {e.Message}");
                result = new ResponseObject
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

                            //int keySize = string.IsNullOrEmpty(providerInfo.PublicKey) ? 2048 : Convert.ToInt32(providerInfo.PublicKey);
                            cardList = GenDecryptListCode(cardList, providerInfo, 2048, isTripDes: true);
                            responseMessage.Payload = cardList;
                        }
                        catch (Exception e)
                        {
                            _logger.LogError($"transCodeToCheck= {transCodeToCheck} Error parsing cards: {e.Message}");
                            responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                            responseMessage.ResponseMessage = "Giao dịch Chưa có kết quá";
                            responseMessage.ProviderResponseCode = result.Data.SoftpinResult.Code;
                            responseMessage.ProviderResponseMessage = "Giao dịch chưa có kết quả";
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

    private class TopupRequestObject
    {
        public string UserId { get; set; }
        public string TargetNumber { get; set; }
        public int Amount { get; set; }
        public string TraceNumber { get; set; }
        public string Telco { get; set; }
        public string Signature { get; set; }
    }

    private class IrisResponseData
    {
        public string Code { get; set; }
        public string Message { get; set; }
        public string MobileType { get; set; }
        public decimal Balance { get; set; }
        public int TopupStatus { get; set; }
        public PinData SoftpinResult { get; set; }
        public List<IrisSoftpin> Softpins { get; set; }
    }

    private class Metadata
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int Total { get; set; }
        public int TotalPage { get; set; }
    }

    private class ResponseObject
    {
        public int Status { get; set; }
        public string Code { get; set; }
        public string Message { get; set; }
        public Metadata Metadata { get; set; }
        public IrisResponseData Data { get; set; }
    }

    private class PinData
    {
        public string Code { get; set; }
        public string Message { get; set; }
        public decimal Balance { get; set; }
        public List<IrisSoftpin> Softpins { get; set; }
    }

    private class PinCodeObject
    {
        public string UserId { get; set; }
        public string TransactionId { get; set; }
        public string ProductCode { get; set; }
        public int Quantity { get; set; }
        public string Signature { get; set; }
    }

    private class IrisSoftpin
    {
        public string Serial { get; set; }
        public string PinCode { get; set; }
        public string ExpiryDate { get; set; }
    }

    private class IRISBalanceRequestObject
    {
        [DataMember(Name = "UserId")] public string UserId { get; set; }
        [DataMember(Name = "UserName")] public string Username { get; set; }
        [DataMember(Name = "Password")] public string Password { get; set; }
    }
}