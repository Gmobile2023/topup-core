using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Topup.Shared;
using Topup.Shared.Dtos;
using Topup.Shared.Utils;
using Topup.TopupGw.Contacts.Dtos;
using Topup.TopupGw.Contacts.Enums;

using Topup.TopupGw.Domains.BusinessServices;
using Topup.TopupGw.Domains.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using ServiceStack;

namespace Topup.TopupGw.Components.Connectors.WhyPay;

public class WhyPayConnector : GatewayConnectorBase
{
    private readonly ILogger<WhyPayConnector> _logger;

    public WhyPayConnector(ITopupGatewayService topupGatewayService, ILogger<WhyPayConnector> logger) : base(
        topupGatewayService)
    {
        _logger = logger;
    }

    public override async Task<MessageResponseBase> TopupAsync(TopupRequestLogDto topupRequestLog,
        ProviderInfoDto providerInfo)
    {
        _logger.LogInformation("{TransCode} WhyPayConnector topup request: {Input}", topupRequestLog.TransCode,
            topupRequestLog.ToJson());
        var responseMessage = new MessageResponseBase();
        try
        {
            if (!TopupGatewayService.ValidConnector(ProviderConst.WHYPAY, providerInfo.ProviderCode))
            {
                _logger.LogError(
                    "{TransCode}-{TransRef}-{ProviderInfoProviderCode}-WhyPayConnector ProviderConnector not valid",
                    topupRequestLog.TransCode, topupRequestLog.TransRef, providerInfo.ProviderCode);
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error,
                    ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
                };
            }

            var data = new WhyPayRequest
            {
                Function = FunctionWhyPay.Topup,
                Msisdn = topupRequestLog.ReceiverInfo,
                MobileType = 1,
                Amount = topupRequestLog.TransAmount,
                PartnerRid = topupRequestLog.TransCode,
                Telco = topupRequestLog.Vendor
            };


            if (topupRequestLog.ServiceCode != "TOPUP_DATA")
            {
                if (data.Telco == "VTE")
                    data.Telco = "VTT";
                else if (data.Telco == "VNA")
                    data.Telco = "VNP";
                else if (data.Telco == "GMOBILE")
                    data.Telco = "GMOBILE";
                else if (data.Telco == "VNM")
                    data.Telco = "VNM";
                else if (data.Telco == "VMS")
                    data.Telco = "VNM";
                else if (data.Telco is "WT" or "Wintel")
                    data.Telco = "Reddi";
            }
            else
            {
                if (data.Telco == "VTE")
                    data.Telco = "VTTDATA";
                else if (data.Telco == "VNA")
                    data.Telco = "VNPDATA";
                else if (data.Telco == "VMS")
                    data.Telco = "VMSDATA";
            }

            responseMessage.TransCodeProvider = topupRequestLog.TransCode;
            var json = data.ToJson();

            var encryptedRequest = Encrypted(json, providerInfo.Password);

            var result = await CallApi(providerInfo, FunctionWhyPay.Topup, topupRequestLog.TransCode, encryptedRequest);
            if (result != null)
            {
                topupRequestLog.ResponseInfo = result.ToJson();
                topupRequestLog.ModifiedDate = DateTime.Now;
                responseMessage.ProviderResponseCode = result.Status;
                responseMessage.ProviderResponseMessage = result.Desc;

                if (result.Status == "1")
                {
                    if (!string.IsNullOrEmpty(result.ResponseStatus))
                    {
                        topupRequestLog.Status = TransRequestStatus.Success;
                        responseMessage.ResponseCode = ResponseCodeConst.Success;
                        responseMessage.ResponseMessage = "Giao dịch thành công";
                        responseMessage.ProviderResponseTransCode = result.TransId;
                        responseMessage.ReceiverType =
                            result.MobileType == "0" ? "TS" : result.MobileType == "1" ? "TT" : null;
                    }
                    else
                    {
                        topupRequestLog.Status = TransRequestStatus.Timeout;
                        responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                        responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả xử lý.";
                    }
                }
                else
                {
                    if (result.Status == "501102")
                    {
                        topupRequestLog.Status = TransRequestStatus.Timeout;
                        responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                        responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả xử lý.";
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(result.ResponseStatus))
                        {
                            topupRequestLog.Status = TransRequestStatus.Timeout;
                            responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                            responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả xử lý.";
                        }
                        else if (!string.IsNullOrEmpty(result.Status) && result.Status != "1")
                        {
                            if (result.Status == "0")
                            {
                                //casse looix
                                var reResult = await TopupGatewayService.GetResponseMassageCacheAsync(ProviderConst.WHYPAY, result.ResponseStatus, topupRequestLog.TransCode);
                                responseMessage.ResponseCode = reResult != null ? reResult.ResponseCode : ResponseCodeConst.ResponseCode_ErrorProvider;
                                responseMessage.ResponseMessage = reResult != null ? reResult.ResponseName : "Giao dịch lỗi từ phía nhà cung cấp";
                            }
                            else
                            {
                                var reResult = await TopupGatewayService.GetResponseMassageCacheAsync(ProviderConst.WHYPAY, result.ResponseStatus, topupRequestLog.TransCode);
                                responseMessage.ResponseCode = reResult != null ? reResult.ResponseCode : ResponseCodeConst.ResponseCode_WaitForResult;
                                responseMessage.ResponseMessage = reResult != null ? reResult.ResponseName : "Giao dịch chưa có kết quả";
                            }

                            //var errorCode = result.ResponseStatus;
                            //if (topupRequestLog.Vendor == "VTE")
                            //{
                            //    var mapError = (result.Desc ?? string.Empty).Split(':')[0];
                            //    if (!string.IsNullOrEmpty(mapError) && !string.IsNullOrEmpty(providerInfo.IgnoreCode) &&
                            //        providerInfo.IgnoreCode.Contains(mapError))
                            //        errorCode = result.ResponseStatus + "_" + mapError;
                            //    if (new[] { "K85", "1085", "K82", "1082" }.Contains(errorCode))
                            //    {
                            //        responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_PhoneLocked;
                            //        responseMessage.ResponseMessage = mapError;
                            //        topupRequestLog.Status = TransRequestStatus.Fail;
                            //    }
                            //    else if (new[]
                            //             {
                            //                 "0", ResponseCodeConst.Success, "10", "71", "75", "97", "KH5", "1005", "P02", "1002",
                            //                 "K03", "1003", "230", "231", "232", "233"
                            //             }.Contains(errorCode))
                            //    {
                            //        responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_ErrorProvider;
                            //        responseMessage.ResponseMessage = "Giao dịch lỗi từ phía Nhà cung cấp.";
                            //        topupRequestLog.Status = TransRequestStatus.Fail;
                            //    }
                            //}
                            //else if (new[]
                            //         {
                            //             "95", "98", "90"
                            //         }.Contains(errorCode))
                            //{
                            //    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_ErrorProvider;
                            //    responseMessage.ResponseMessage = "Giao dịch lỗi từ phía Nhà cung cấp.";
                            //    topupRequestLog.Status = TransRequestStatus.Fail;
                            //}
                            //else
                            //{
                            //    topupRequestLog.Status = TransRequestStatus.Fail;
                            //    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                            //    responseMessage.ResponseMessage = "Giao dịch chưa có kết quả";
                            //}
                        }
                        else
                        {
                            topupRequestLog.Status = TransRequestStatus.Timeout;
                            responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                            responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả xử lý.";
                        } 
                            
                    }
                }
            }
            else
            {
                _logger.LogInformation("{TransCode} Error send request", topupRequestLog.TransCode);
                responseMessage.ResponseMessage = "Lỗi kết nối nhà cung cấp";
                topupRequestLog.Status = TransRequestStatus.Fail;
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
        _logger.LogInformation($"{cardRequestLog.TransCode} WhyPayConnector Get card request: " + cardRequestLog.ToJson());
        var responseMessage = new MessageResponseBase();

        var providerInfo = await TopupGatewayService.ProviderInfoCacheGetAsync(cardRequestLog.ProviderCode);

        if (providerInfo == null)
            return responseMessage;

        if (!TopupGatewayService.ValidConnector(ProviderConst.WHYPAY, providerInfo.ProviderCode))
        {
            _logger.LogInformation($"{cardRequestLog.ProviderCode}-{cardRequestLog.TransCode}-{providerInfo.ProviderCode}-WhyPayConnector ProviderConnector not valid");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
            };
        }
        var data = new WhyPayCardRequest
        {
            Function = FunctionWhyPay.BuyCardCode,
            Telco = cardRequestLog.Vendor,
            Amount = Convert.ToInt32(cardRequestLog.TransAmount),
            Quantity = cardRequestLog.Quantity,
            PartnerRid = cardRequestLog.TransCode,

        };

        if (data.Telco == "VTE")
            data.Telco = "VTT";
        else if (data.Telco == "VNA")
            data.Telco = "VNP";
        else if (data.Telco == "GMOBILE")
            data.Telco = "GMOBILE";
        else if (data.Telco == "VNM")
            data.Telco = "VNM";
        else if (data.Telco == "VMS")
            data.Telco = "VNM";
        else if (data.Telco is "WT" or "Wintel")
            data.Telco = "Reddi";

        responseMessage.TransCodeProvider = cardRequestLog.TransCode;
        var json = data.ToJson();

        var encryptedRequest = Encrypted(json, providerInfo.Password);

        var result = await CallApi(providerInfo, FunctionWhyPay.BuyCardCode, cardRequestLog.TransCode, encryptedRequest);

        if (result != null)
        {
            cardRequestLog.ModifiedDate = DateTime.Now;
            responseMessage.ProviderResponseCode = result.Status;
            responseMessage.ProviderResponseMessage = result.Desc;

            if (result.Status == "1")
            {
                if (!string.IsNullOrEmpty(result.ResponseStatus))
                {
                    cardRequestLog.Status = TransRequestStatus.Success;
                    responseMessage.ResponseCode = ResponseCodeConst.Success;
                    responseMessage.ResponseMessage = "Giao dịch thành công";
                    responseMessage.ProviderResponseTransCode = result.TransId;
                    try
                    {
                        var cardList = new List<CardRequestResponseDto>();
                        foreach (var card in result.cards)
                        {
                            cardList.Add(new CardRequestResponseDto
                            {
                                CardCode = card.Code,
                                Serial = card.Serial,
                                ExpiredDate = UnixTimeStampToDateTime(Convert.ToDouble(card.ExpiredDate)),
                                ExpireDate = UnixTimeStampToDateTime(Convert.ToDouble(card.ExpiredDate)).ToString("dd/MM/yyyy"),
                                CardValue = cardRequestLog.TransAmount.ToString(CultureInfo.InvariantCulture),
                            });
                        }

                        responseMessage.Payload = cardList;
                    }
                    catch (Exception e)
                    {
                        _logger.LogError($"{cardRequestLog.TransCode} Vmg2Connector Error parsing cards: " + e.Message);
                    }
                }
                else
                {
                    cardRequestLog.Status = TransRequestStatus.Timeout;
                    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                    responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả xử lý.";
                }
            }
            else
            {
                if (string.IsNullOrEmpty(result.ResponseStatus))
                {
                    cardRequestLog.Status = TransRequestStatus.Timeout;
                    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                    responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả xử lý.";
                }
                else if (!string.IsNullOrEmpty(result.Status) && result.Status != "1")
                {
                    if (result.Status == "0")
                    {
                        cardRequestLog.Status = TransRequestStatus.Fail;
                        responseMessage.ResponseCode = ResponseCodeConst.Error;
                        responseMessage.ResponseMessage = "Giao dịch không thành công.";
                    }
                    else
                    {
                        var reResult = await TopupGatewayService.GetResponseMassageCacheAsync(ProviderConst.WHYPAY, result.ResponseStatus, cardRequestLog.TransCode);
                        responseMessage.ResponseCode = reResult != null ? reResult.ResponseCode : ResponseCodeConst.ResponseCode_WaitForResult;
                        responseMessage.ResponseMessage = reResult != null ? reResult.ResponseName : "Giao dịch chưa có kết quả";
                    }
                }
                else
                {
                    cardRequestLog.Status = TransRequestStatus.Timeout;
                    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                    responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả xử lý.";
                }
            }
        }
        else
        {
            _logger.LogInformation("{TransCode} Error send request", cardRequestLog.TransCode);
            responseMessage.ResponseMessage = "Lỗi kết nối nhà cung cấp";
            cardRequestLog.Status = TransRequestStatus.Fail;
        }

        await TopupGatewayService.CardRequestLogUpdateAsync(cardRequestLog);
        return responseMessage;
    }

    public override async Task<MessageResponseBase> TransactionCheckAsync(string providerCode, string transCodeToCheck,
        string transCode, string serviceCode = null,
        ProviderInfoDto providerInfo = null)
    {
        _logger.LogInformation("{Ref} WhyPayConnector check request: {TransCode}", transCodeToCheck,
            transCode);
        if (string.IsNullOrEmpty(serviceCode))
        {
            _logger.LogError("{TransCode}-{ProviderCode}-CheckOnly topup", transCode,
                providerCode);
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult,
                ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ"
            };
        }

        var responseMessage = new MessageResponseBase();
        try
        {
            if (providerInfo == null)
                providerInfo = await TopupGatewayService.ProviderInfoCacheGetAsync(providerCode);


            if (providerInfo == null ||
                !TopupGatewayService.ValidConnector(ProviderConst.WHYPAY, providerInfo.ProviderCode))
            {
                _logger.LogError("{TransCode}-{ProviderCode}-WhyPayConnector ProviderConnector not valid", transCode,
                    providerCode);
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult,
                    ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ"
                };
            }

            string function = FunctionWhyPay.CheckTopup;
            if ((serviceCode ?? string.Empty).Contains("PIN"))
                function = FunctionWhyPay.CheckBuyCard;
            var data = new WhyPayRequest
            {
                Function = function,
                PartnerRid = transCodeToCheck
            };
            var json = data.ToJson();
            var encryptedRequest = Encrypted(json, providerInfo.Password);
            var result = await CallApi(providerInfo, function, transCodeToCheck, encryptedRequest);
            if (result != null)
            {
                responseMessage.ProviderResponseCode = result.Status;
                responseMessage.ProviderResponseMessage = result.Desc;
                if (result.Status == "1")
                {
                    if (!string.IsNullOrEmpty(result.ResponseStatus))
                    {
                        responseMessage.ResponseCode = ResponseCodeConst.Success;
                        responseMessage.ResponseMessage = "Giao dịch thành công";
                        responseMessage.ProviderResponseTransCode = result.TransId;
                        if (function == FunctionWhyPay.CheckBuyCard)
                        {
                            try
                            {
                                var cardList = new List<CardRequestResponseDto>();
                                foreach (var card in result.cards)
                                {
                                    cardList.Add(new CardRequestResponseDto
                                    {
                                        CardCode = card.Code.EncryptTripDes(),
                                        Serial = card.Serial,
                                        ExpiredDate = UnixTimeStampToDateTime(Convert.ToDouble(card.ExpiredDate)),
                                        ExpireDate = UnixTimeStampToDateTime(Convert.ToDouble(card.ExpiredDate)).ToString("dd/MM/yyyy"),
                                        CardValue = card.Price,
                                    });
                                }
                                responseMessage.Payload = cardList;
                            }
                            catch (Exception e)
                            {
                                _logger.LogError($"{transCodeToCheck} WhyPayConnector Error parsing cards: " + e.Message);
                            }
                        }
                        else
                        {
                            responseMessage.ReceiverType =
                                result.MobileType == "0" ? "TS" : result.MobileType == "1" ? "TT" : null;
                        }
                    }
                    else
                    {
                        responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                        responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả xử lý.";
                    }
                }
                else
                {
                    if (string.IsNullOrEmpty(result.ResponseStatus))
                    {
                        responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                        responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả xử lý.";
                    }
                    else if (!string.IsNullOrEmpty(result.Status) && result.Status != "1")
                    {
                        if (result.Status == "0")
                        {
                            responseMessage.ResponseCode = ResponseCodeConst.Error;
                            responseMessage.ResponseMessage = "Giao dịch không thành công.";
                        }
                        else
                        {
                            var reResult = await TopupGatewayService.GetResponseMassageCacheAsync(ProviderConst.WHYPAY, result.ResponseStatus, string.Empty);
                            responseMessage.ResponseCode = reResult != null ? reResult.ResponseCode : ResponseCodeConst.ResponseCode_WaitForResult;
                            responseMessage.ResponseMessage = reResult != null ? reResult.ResponseName : "Giao dịch chưa có kết quả";
                        }
                    }
                    else
                    {                        
                        responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                        responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả xử lý.";
                    }
                }
            }
            else
            {
                _logger.LogInformation("{TransCode} Error send request", transCodeToCheck);
                responseMessage.ResponseMessage = "Lỗi kết nối nhà cung cấp";
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
        var providerInfo = await TopupGatewayService.ProviderInfoCacheGetAsync(providerCode);
        if (providerInfo == null)
        {
            _logger.LogInformation($"providerCode= {providerCode}|providerInfo is null");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error
            };
        }

        var data = new WhyPayRequest
        {
            Function = FunctionWhyPay.GetBalance
        };

        var json = data.ToJson();
        var encryptedRequest = Encrypted(json, providerInfo.Password);
        var result = await CallApiBalance(providerInfo, transCode, encryptedRequest);
        _logger.LogInformation($"{providerCode} CheckBalance: {result.ToJson()}");
        return new MessageResponseBase
        {
            ResponseCode = ResponseCodeConst.Success,
            ResponseMessage = "Success",
            Payload = result
        };
    }

    private async Task<WhyPayResponse> CallApi(ProviderInfoDto providerInfo, string function, string transCode, string encryptedRequest)
    {
        WhyPayResponse result = null;
        try
        {
            _logger.LogInformation($"WhyPay request {transCode}-{encryptedRequest}");
            var client = new JsonServiceClient(providerInfo.ApiUrl);
            client.Timeout = TimeSpan.FromSeconds(providerInfo.Timeout);
            if (function is FunctionWhyPay.BuyCardCode or FunctionWhyPay.CheckBuyCard)
            {
                var resultData = await client.GetAsync<object>($"/api/user/sync?partner={providerInfo.Username}&info=" +
                                                            Uri.EscapeDataString(encryptedRequest));
                _logger.LogInformation($"function= {function} - WhyPay response {transCode}-{resultData}");
                if (resultData != null)
                {
                    var data = CardDecrypt(resultData.ToString(), providerInfo.Password);
                    result = data.FromJson<WhyPayResponse>();
                }
                else result = new WhyPayResponse();
            }
            else
            {
                result = await client.GetAsync<WhyPayResponse>($"/api/user/sync?partner={providerInfo.Username}&info=" +
                                                               Uri.EscapeDataString(encryptedRequest));
                _logger.LogInformation($"function= {function} - WhyPay response {transCode}-{result.ToJson()}");
            }
        }
        catch (Exception e)
        {
            _logger.LogError("{TransCode} WhyPayError {E}", transCode, e);
            result = new WhyPayResponse
            {
                Status = "501102",
                Desc = "Lỗi kết nối nhà cung cấp"
            };
        }

        return result;
    }

    private async Task<decimal> CallApiBalance(ProviderInfoDto providerInfo, string transCode, string encryptedRequest)
    {
        decimal result = 0;
        try
        {
            var client = new JsonServiceClient(providerInfo.ApiUrl);
            client.Timeout = TimeSpan.FromSeconds(providerInfo.Timeout);
            result = await client.GetAsync<decimal>($"/api/user/sync?partner={providerInfo.Username}&info=" +
                                                    Uri.EscapeDataString(encryptedRequest));
        }
        catch (Exception e)
        {
            _logger.LogError("{TransCode} Error {E}", transCode, e);
            return 0;
        }

        return result;
    }

    private static string Encrypted(string data, string stringKey)
    {
        var md5 = MD5.Create();
        var key = md5.ComputeHash(Encoding.UTF8.GetBytes(stringKey));

        // Destroy objects that aren't needed.
        md5.Clear();
        md5 = null;
        var toEncryptArray = Encoding.UTF8.GetBytes(data);
        var first8byte = new byte[8];
        for (var i = 0; i < 8; i++) first8byte[i] = key[i];
        //Take first 8 bytes of $key and append them to the end of $key.
        key = key.Concat(first8byte).ToArray();

        var tdes = TripleDES.Create();
        //set the secret key for the tripleDES algorithm
        tdes.Key = key;
        //mode of operation. there are other 4 modes. We choose ECB(Electronic code Book)
        tdes.Mode = CipherMode.ECB;
        //padding mode(if any extra byte added)
        tdes.Padding = PaddingMode.PKCS7;

        var cTransform = tdes.CreateEncryptor();
        //transform the specified region of bytes array to resultArray
        var resultArray = cTransform.TransformFinalBlock
            (toEncryptArray, 0, toEncryptArray.Length);
        //Release resources held by TripleDes Encryptor
        tdes.Clear();
        //Return the encrypted data into unreadable string format
        return Convert.ToBase64String(resultArray, 0, resultArray.Length);
    }

    public static DateTime UnixTimeStampToDateTime(double unixTimeStamp)
    {
        // Unix timestamp is seconds past epoch
        DateTime dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Local);
        dateTime = dateTime.AddSeconds(unixTimeStamp).ToLocalTime();
        return dateTime;
    }

    public static string CardDecrypt(string TextToDecrypt, string stringKey)
    {
        byte[] MyDecryptArray = Convert.FromBase64String
           (TextToDecrypt);

        MD5CryptoServiceProvider MyMD5CryptoService = new
           MD5CryptoServiceProvider();

        byte[] MysecurityKeyArray = MyMD5CryptoService.ComputeHash
           (UTF8Encoding.UTF8.GetBytes(stringKey));

        MyMD5CryptoService.Clear();

        var MyTripleDESCryptoService = new
           TripleDESCryptoServiceProvider();

        MyTripleDESCryptoService.Key = MysecurityKeyArray;

        MyTripleDESCryptoService.Mode = CipherMode.ECB;

        MyTripleDESCryptoService.Padding = PaddingMode.PKCS7;

        var MyCrytpoTransform = MyTripleDESCryptoService
           .CreateDecryptor();

        byte[] MyresultArray = MyCrytpoTransform
           .TransformFinalBlock(MyDecryptArray, 0,
           MyDecryptArray.Length);

        MyTripleDESCryptoService.Clear();

        return UTF8Encoding.UTF8.GetString(MyresultArray);
    }


    [DataContract]
    internal class WhyPayRequest
    {
        [DataMember(Name = "function")] public string Function { get; set; }

        [DataMember(Name = "msisdn")] public string Msisdn { get; set; }

        [DataMember(Name = "mobile_type")] public int MobileType { get; set; }

        [DataMember(Name = "telco")] public string Telco { get; set; }

        [DataMember(Name = "amount")] public int Amount { get; set; }

        [DataMember(Name = "partner_rid")] public string PartnerRid { get; set; }
    }

    [DataContract]
    internal class WhyPayCardRequest
    {
        [DataMember(Name = "function")] public string Function { get; set; }
        [DataMember(Name = "telco")] public string Telco { get; set; }
        [DataMember(Name = "amount")] public int Amount { get; set; }
        [DataMember(Name = "quantity")] public int Quantity { get; set; }
        [DataMember(Name = "partner_rid")] public string PartnerRid { get; set; }
    }

    [DataContract]
    internal class WhyPayResponse
    {
        [DataMember(Name = "status")] public string Status { get; set; }

        [DataMember(Name = "desc")] public string Desc { get; set; }

        [DataMember(Name = "response_status")] public string ResponseStatus { get; set; }

        [DataMember(Name = "trans_id")] public string TransId { get; set; }

        [DataMember(Name = "mobile_type")] public string MobileType { get; set; }

        [DataMember(Name = "cards")] public List<CardItem> cards { get; set; }
    }

    [DataContract]
    internal class CardItem
    {
        [DataMember(Name = "code")] public string Code { get; set; }

        [DataMember(Name = "serial")] public string Serial { get; set; }

        [DataMember(Name = "expired_date")] public string ExpiredDate { get; set; }

        [DataMember(Name = "price")] public string Price { get; set; }

        [DataMember(Name = "type")] public string Type { get; set; }
    }

    internal class FunctionWhyPay
    {
        public const string BuyCardCode = "BuyCardCode";
        public const string Topup = "Topup";
        public const string GetBalance = "GetBalance";
        public const string CheckTopup = "CheckTopup";
        public const string CheckBuyCard = "CheckBuyCard";
    }
}