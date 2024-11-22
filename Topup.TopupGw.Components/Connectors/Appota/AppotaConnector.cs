using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using HLS.Paygate.Shared;
using HLS.Paygate.Shared.Dtos;
using HLS.Paygate.TopupGw.Contacts.ApiRequests;
using HLS.Paygate.TopupGw.Contacts.Dtos;
using HLS.Paygate.TopupGw.Contacts.Enums;
using HLS.Paygate.TopupGw.Domains.BusinessServices;
using Microsoft.Extensions.Logging;
using ServiceStack;

namespace HLS.Paygate.TopupGw.Components.Connectors.Appota;

public class AppotaConnector : IGatewayConnector
{
    private readonly ILogger<AppotaConnector> _logger;
    private readonly ITopupGatewayService _topupGatewayService;


    public AppotaConnector(ITopupGatewayService topupGatewayService,
        ILogger<AppotaConnector> logger)
    {
        _topupGatewayService = topupGatewayService;
        _logger = logger;
    }

    public async Task<MessageResponseBase> TopupAsync(TopupRequestLogDto topupRequestLog, ProviderInfoDto providerInfo)
    {
        var accessToken =
            "eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiIsImN0eSI6ImFwcG90YXBheS1hcGk7dj0xIn0.eyJpc3MiOiJURVNUIiwiYXBpX2tleSI6ImNSNmZzdW96N0sxU3BNaEo2SEQyV01xVHprQkprMGE1IiwianRpIjoiY1I2ZnN1b3o3SzFTcE1oSjZIRDJXTXFUemtCSmswYTUtMTYyMzM2OTU5OSIsImV4cCI6MTYyMzM2OTU5OX0.cfwp8q6tb7x4nfyY67Y6OzzQQmQup8BUhDgPVpQvN5g";

        _logger.Log(LogLevel.Information, "AppotaConnector request: " + topupRequestLog.ToJson());
        var responseMessage = new MessageResponseBase();
        if (!_topupGatewayService.ValidConnector(ProviderConst.APPOTA, providerInfo.ProviderCode))
        {
            _logger.LogError($"{topupRequestLog.TransCode}-{topupRequestLog.TransRef} ProviderConnector not valid");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
            };
        }

        var request = new AppotaRequest
        {
            PartnerRefId = topupRequestLog.TransCode,
            PhoneNumber = topupRequestLog.ReceiverInfo,
            Amount = topupRequestLog.TransAmount,
            Telco = topupRequestLog.Vendor,
            TelcoServiceType = topupRequestLog.CategoryCode == "MOBILE_BILL" ? "POSTPAID" : "PREPAID"
        };

        switch (request.Telco)
        {
            case "VTE":
                request.Telco = "viettel";
                break;
            case "VMS":
                request.Telco = "mobifone";
                break;
            case "VNA":
                request.Telco = "vinaphone";
                break;
            case "VNM":
                request.Telco = "vnmobile";
                break;
            case "BEE":
                request.Telco = "beeline";
                break;
        }

        var str =
            $"{request.Amount}{request.PartnerRefId}{request.PhoneNumber}{request.Telco}{request.TelcoServiceType}";
        //var str = $"amount={request.Amount}&partnerRefId={request.PartnerRefId}&phoneNumber={request.PhoneNumber}&telco={request.Telco}&ServiceType={request.TelcoServiceType}";
        var sign = HmacSHA256(str, providerInfo.PublicKey);
        request.Siganture = sign;
        var result = await CallAppotaApi(providerInfo, request, "TOPUP", accessToken);
        _logger.LogInformation(request.PartnerRefId + $" AppotaConnector Return: {result.ToJson()}");
        try
        {
            responseMessage.ProviderResponseCode = result?.ErrorCode.ToString();
            responseMessage.ProviderResponseMessage = result?.Message;
            if (result != null && result.ErrorCode == 0)
            {
                topupRequestLog.ModifiedDate = DateTime.Now;
                topupRequestLog.ResponseInfo = result.ToJson();
                _logger.LogInformation($"AppotaConnector return: {topupRequestLog.ProviderCode}-{topupRequestLog.TransCode}-{topupRequestLog.TransRef}-{result.ToJson()}");
                topupRequestLog.Status = TransRequestStatus.Success;
                responseMessage.ResponseCode = ResponseCodeConst.Success;
                responseMessage.ResponseMessage = "Thành công";
            }
            else
            {
                var arrayCode = _topupGatewayService.ConvertArrayCode(providerInfo.ExtraInfo ?? string.Empty);
                if (result != null && arrayCode.Contains(result.ErrorCode))
                {
                    topupRequestLog.ModifiedDate = DateTime.Now;
                    topupRequestLog.ResponseInfo = result.ToJson();
                    _logger.LogInformation($"AppotaConnector return:{topupRequestLog.ProviderCode}-{topupRequestLog.TransCode}-{topupRequestLog.TransRef}-{result.ToJson()}");
                    topupRequestLog.Status = TransRequestStatus.Fail;
                    responseMessage.ResponseCode = ResponseCodeConst.Error;
                    responseMessage.ResponseMessage = "Giao dịch không thành công từ nhà cung cấp";
                    var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.APPOTA, result.ErrorCode.ToString(), topupRequestLog.TransCode);
                    responseMessage.ResponseCode = reResult != null ? reResult.ReponseCode : ResponseCodeConst.Error;
                    responseMessage.ResponseMessage = reResult != null ? reResult.ReponseName : "Giao dịch không thành công từ nhà cung cấp";
                }
                else
                {
                    _logger.LogInformation($"AppotaConnector return:{topupRequestLog.ProviderCode}-{topupRequestLog.TransCode}-{topupRequestLog.TransRef}-{result.ToJson()}");
                    responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_TimeOut;
                    topupRequestLog.Status = TransRequestStatus.Timeout;
                    topupRequestLog.ModifiedDate = DateTime.Now;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogInformation($"AppotaConnector Error: {topupRequestLog.ProviderCode}-{topupRequestLog.TransCode}-{topupRequestLog.TransRef}-{result.ToJson()} Exception: {ex}");
            responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
            responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_TimeOut;
            topupRequestLog.Status = TransRequestStatus.Timeout;
        }

        await _topupGatewayService.TopupRequestLogUpdateAsync(topupRequestLog);
        return responseMessage;
    }

    public async Task<MessageResponseBase> TransactionCheckAsync(string providerCode, string transCodeToCheck,
        string transCode, string serviceCode = null, ProviderInfoDto providerInfo = null)
    {
        _logger.LogInformation($"{transCodeToCheck} CheckTrans request: " + transCode);
        var responseMessage = new MessageResponseBase();

        if (providerInfo == null)
            providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(providerCode);


        if (providerInfo == null || !_topupGatewayService.ValidConnector(ProviderConst.APPOTA, providerInfo.ProviderCode))
        {
            _logger.LogError($"{transCode}-{providerCode} ProviderConnector not valid");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult,
                ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ"
            };
        }

        //var resultCheckTrans = await CallApiCheckTrans(providerInfo, transCodeToCheck);
        //_logger.LogInformation($"{transCode} checkTranTopup return: {resultCheckTrans.ToJson()}");

        //if (resultCheckTrans != null && resultCheckTrans.responseStatus.errorCode == "00")
        //{
        //    responseMessage.ResponseCode = ResponseCodeConst.Success;
        //    responseMessage.ResponseMessage = "Thành công";
        //}
        //else if (resultCheckTrans != null && resultCheckTrans.responseStatus.errorCode == "01")
        //{
        //    responseMessage.ResponseCode = ResponseCodeConst.Error;
        //    responseMessage.ResponseMessage = "Giao dịch không thành công từ nhà cung cấp";
        //}
        //else
        //{
        //    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
        //    responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
        //}


        return responseMessage;
    }

    public async Task<NewMessageReponseBase<InvoiceResultDto>> QueryAsync(PayBillRequestLogDto payBillRequestLog)
    {
        return await Task.FromResult(new NewMessageReponseBase<InvoiceResultDto>
        {
            ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error, "Nhà cung cấp không hỗ trợ truy vấn")
        });
    }

    public async Task<MessageResponseBase> CardGetByBatchAsync(CardRequestLogDto cardRequestLog)
    {
        _logger.Log(LogLevel.Information, "AppotaConnector request: " + cardRequestLog.ToJson());
        var responseMessage = new MessageResponseBase();
        var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(cardRequestLog.ProviderCode);

        if (providerInfo == null)
        {
            _logger.LogInformation("providerInfo is null");
            return responseMessage;
        }

        if (!_topupGatewayService.ValidConnector(ProviderConst.APPOTA, providerInfo.ProviderCode))
        {
            _logger.LogError($"{cardRequestLog.TransCode}-{cardRequestLog.TransRef} ProviderConnector not valid");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
            };
        }

        var request = new AppotaRequest
        {
            PartnerRefId = cardRequestLog.TransCode,
            PhoneNumber = cardRequestLog.ReceiverInfo,
            Quantity = cardRequestLog.Quantity
        };

        //partnerRefId + ProductCode + Quantity
        responseMessage.ExtraInfo = request.PartnerRefId;
        var providerService =
            providerInfo.ProviderServices.Find(p => p.ProductCode == cardRequestLog.ProductCode);
        request.ProductCode = providerService.ServiceCode;

        var sign = HmacSignature(string.Join("", request.PartnerRefId, request.ProductCode, request.Quantity),
            providerInfo.PrivateKeyFile);
        request.Siganture = sign;
        var accessToken = HmacSignatureHeader(providerInfo);
        var result = await CallAppotaApi(providerInfo, request, "PINCODE", accessToken);
        _logger.LogInformation(request.PartnerRefId + $" AppotaConnector Return: {result.ToJson()}");
        try
        {
            if (result != null && result.ErrorCode == 0)
            {
                cardRequestLog.ModifiedDate = DateTime.Now;
                cardRequestLog.ResponseInfo = result.ToJson();
                _logger.LogInformation(
                    $"AppotaConnector return: {cardRequestLog.ProviderCode}-{cardRequestLog.TransCode}-{cardRequestLog.TransRef}-{result.ToJson()}");
                cardRequestLog.Status = TransRequestStatus.Success;
                responseMessage.ResponseCode = ResponseCodeConst.Success;
                responseMessage.ResponseMessage = "Thành công";
            }
            else
            {
                var arrayCode = _topupGatewayService.ConvertArrayCode(providerInfo.ExtraInfo ?? string.Empty);
                if (result != null && arrayCode.Contains(result.ErrorCode))
                {

                    cardRequestLog.ModifiedDate = DateTime.Now;
                    cardRequestLog.ResponseInfo = result.ToJson();
                    _logger.LogInformation($"AppotaConnector return:{cardRequestLog.ProviderCode}-{cardRequestLog.TransCode}-{cardRequestLog.TransRef}-{result.ToJson()}");
                    cardRequestLog.Status = TransRequestStatus.Fail;
                    responseMessage.ResponseCode = ResponseCodeConst.Error;
                    responseMessage.ResponseMessage = "Giao dịch không thành công từ nhà cung cấp";
                    var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.APPOTA, result.ErrorCode.ToString(), cardRequestLog.TransCode);
                    responseMessage.ResponseCode = reResult != null ? reResult.ReponseCode : ResponseCodeConst.Error;
                    responseMessage.ResponseMessage = reResult != null ? reResult.ReponseName : "Giao dịch không thành công từ nhà cung cấp";
                }
                else
                {
                    _logger.LogInformation($"AppotaConnector return:{cardRequestLog.ProviderCode}-{cardRequestLog.TransCode}-{cardRequestLog.TransRef}-{result.ToJson()}");
                    responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_TimeOut;
                    cardRequestLog.Status = TransRequestStatus.Timeout;
                    cardRequestLog.ModifiedDate = DateTime.Now;
                }
            }           
        }
        catch (Exception ex)
        {
            _logger.LogInformation($"AppotaConnector Error: {cardRequestLog.ProviderCode}-{cardRequestLog.TransCode}-{cardRequestLog.TransRef}-{result.ToJson()} Exception: {ex}");
            responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
            responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_TimeOut;
            cardRequestLog.Status = TransRequestStatus.Timeout;
        }

        await _topupGatewayService.CardRequestLogUpdateAsync(cardRequestLog);
        return responseMessage;
    }

    public async Task<MessageResponseBase> CheckBalanceAsync(string providerCode, string transCode)
    {
        _logger.Log(LogLevel.Information, "QueryBalanceAsync request: " + providerCode);
        var responseMessage = new MessageResponseBase();
        var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(providerCode);

        if (providerInfo == null)
        {
            _logger.LogInformation("providerInfo is null");
            return responseMessage;
        }

        if (!_topupGatewayService.ValidConnector(ProviderConst.APPOTA, providerInfo.ProviderCode))
        {
            _logger.LogError($"{providerCode}-{transCode} ProviderConnector not valid");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
            };
        }

        var accessToken = HmacSignatureHeader(providerInfo);

        try
        {
            accessToken =
                "eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiIsImN0eSI6ImFwcG90YXBheS1hcGk7dj0xIn0.eyJpc3MiOiJURVNUIiwiYXBpX2tleSI6ImNSNmZzdW96N0sxU3BNaEo2SEQyV01xVHprQkprMGE1IiwianRpIjoiY1I2ZnN1b3o3SzFTcE1oSjZIRDJXTXFUemtCSmswYTUtMTYyMzM2OTU5OSIsImV4cCI6MTYyMzM2OTU5OX0.cfwp8q6tb7x4nfyY67Y6OzzQQmQup8BUhDgPVpQvN5g";
            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(providerInfo.Timeout),
                BaseAddress = new Uri(providerInfo.ApiUrl)
            };
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("X-APPOTAPAY-AUTH", $"Bearer {accessToken}");
            var result = await client.GetAsync("/api/v1/service/accounts/balance");
            _logger.LogInformation($"{providerInfo.ProviderCode} AppotaConnector Reponse:{result.StatusCode}");
            if (result.IsSuccessStatusCode)
            {
                var rs = await result.Content.ReadAsStringAsync();
                var getRs = rs.FromJson<AppotaReponse>();
                _logger.LogInformation($"{providerInfo.ProviderCode} CheckBalance Reponse : {getRs.ToJson()}");
                if (getRs != null && getRs.ErrorCode == 0)
                {
                    responseMessage.ResponseCode = ResponseCodeConst.Success;
                    responseMessage.ResponseMessage = "Giao dịch thành công";
                    responseMessage.Payload = getRs.Account?.balance;
                }
                else
                {
                    responseMessage.ResponseCode = ResponseCodeConst.Error;
                    responseMessage.ResponseMessage = "Truy vấn thất bại";
                    responseMessage.Payload = "0";
                }
            }
            else
            {
                responseMessage.ResponseCode = ResponseCodeConst.Error;
                responseMessage.ResponseMessage = "Truy vấn thất bại";
                responseMessage.Payload = "0";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"{providerCode} QueryBalanceAsync .Exception: " + ex.Message);

            responseMessage.ResponseCode = ResponseCodeConst.Error;
            responseMessage.ResponseMessage = "Truy vấn thất bại.";
            responseMessage.Payload = "0";
        }

        return responseMessage;
    }

    public Task<MessageResponseBase> DepositAsync(DepositRequestDto request)
    {
        throw new NotImplementedException();
    }

    public async Task<MessageResponseBase> PayBillAsync(PayBillRequestLogDto payBillRequestLog)
    {
        return await Task.FromResult(new MessageResponseBase());
    }

    private async Task<AppotaReponse> CallAppotaApi(ProviderInfoDto providerInfo, AppotaRequest request,
        string serviceCode, string accessToken)
    {
        try
        {
            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(providerInfo.Timeout),
                BaseAddress = new Uri(providerInfo.ApiUrl)
            };
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("X-APPOTAPAY-AUTH", $"Bearer {accessToken}");
            var data = new StringContent(request.ToJson(), Encoding.UTF8, "application/json");
            _logger.LogInformation($"{request.PartnerRefId}|{serviceCode} AppotaConnector send: " + request.ToJson());
            var urlInput = serviceCode.StartsWith("TOPUP")
                ? "/api/v1/service/topup/charging"
                : "/api/v1/service/shopcard/buy";

            var result = await client.PostAsync(urlInput, data);
            _logger.LogInformation($"{serviceCode} AppotaConnector Reponse:{result.StatusCode}");
            if (result.IsSuccessStatusCode)
            {
                var rs = await result.Content.ReadAsStringAsync();
                var getRs = rs.FromJson<AppotaReponse>();
                _logger.LogInformation(
                    $"{request.PartnerRefId}|{serviceCode} AppotaConnector Reponse: {getRs.ToJson()}");
                return getRs;
            }

            return new AppotaReponse
            {
                ErrorCode = 501102,
                Message = "Chưa xác định trạng thái"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"{request.PartnerRefId} AppotaConnector Error: " + ex);
            return new AppotaReponse
            {
                ErrorCode = 501102,
                Message = ex.Message
            };
        }
    }

    private string HmacSignature(string sig, string token)
    {
        var signature = string.Empty;
        using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(token)))
        {
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(sig));
            signature = Convert.ToBase64String(hash);
        }

        return signature;
    }

    private string HmacSignatureHeader(ProviderInfoDto providerInfo)
    {
        //var time = DateTimeOffset.Now.ToUnixTimeSeconds();
        // var exp = DateTimeOffset.Now.AddDays(1).ToUnixTimeSeconds();
        // var epoch = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds.ToString().Split;

        var exp = "1623369599";
        var header = new Header
        {
            typ = "JWT",
            alg = "HS256",
            cty = "appotapay-api;v=1"
        };

        var payload = new Payload
        {
            iss = providerInfo.ApiUser,
            api_key = providerInfo.ApiPassword,
            jti = providerInfo.ApiPassword + "-" + exp,
            exp = Convert.ToInt32(exp)
        };

        var inputBytes = Encoding.UTF8.GetBytes(header.ToJson());
        var inputHeader = Convert.ToBase64String(inputBytes);
        var inputBytesPayLoad = Encoding.UTF8.GetBytes(payload.ToJson());
        var inputPayLoad = Convert.ToBase64String(inputBytesPayLoad);
        var inputKey = Encoding.UTF8.GetBytes(providerInfo.PublicKey);
        var inputPayKey = Convert.ToBase64String(inputKey);
        var sign = string.Join(".", inputHeader, inputPayLoad);
        Test(sign, providerInfo.PublicKey);
        sign = Sign(string.Join(".", inputHeader, inputPayLoad), providerInfo.PublicKey);
        return sign;
    }

    private static string Sign(string stringToSign, string Key)
    {
        var signature = string.Empty;
        var unicodeKey = Encoding.UTF8.GetBytes(Key);
        using (var hmacSha256 = new HMACSHA256(unicodeKey))
        {
            var dataToHmac = Encoding.UTF8.GetBytes(stringToSign);
            signature = Convert.ToBase64String(hmacSha256.ComputeHash(dataToHmac));
        }

        //var pp = GetHMAC(stringToSign, Key);
        //Test(stringToSign, Key);
        return signature;
    }

    private static void Test(string jwt, string client_secret)
    {
        //string jwt = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiYWRtaW4iOnRydWV9.TJVA95OrM7E2cBab30RMHrHDcEfxjoYZgeFONFh7HgQ";
        var parts = jwt.Split(".".ToCharArray());
        var headerDotPayload = string.Format("{0}.{1}", parts[0], parts[1]);

        var signature = parts[2];
        var secret = Encoding.UTF8.GetBytes(client_secret);
        var input = Encoding.UTF8.GetBytes(headerDotPayload);

        var alg = new HMACSHA256(secret);
        var hash = alg.ComputeHash(input);

        //Attempting to verify
        var result = new StringBuilder();

        for (var i = 0; i < hash.Length; i++) result.Append(hash[i].ToString("x2"));

        var verify1 = result.ToString(); //Does not match signature

        var verify2 = Encoding.UTF8.GetString(hash); //Does not match signature

        var verify3 = Encoding.UTF8.GetBytes(signature); //Does not match value in the hash byte[]
    }

    public static string GetHMAC(string text, string key)
    {
        var enc = Encoding.UTF8;
        var hmac = new HMACSHA256(enc.GetBytes(key));
        hmac.Initialize();

        var buffer = enc.GetBytes(text);
        return BitConverter.ToString(hmac.ComputeHash(buffer)).Replace("-", "").ToLower();
    }

    public static string Base64Encode(string plainText)
    {
        var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
        return Convert.ToBase64String(plainTextBytes);
    }

    private string HmacSHA256(string data, string key)
    {
        string hash;
        var encoder = new ASCIIEncoding();
        var code = encoder.GetBytes(key);
        using (var hmac = new HMACSHA256(code))
        {
            var hmBytes = hmac.ComputeHash(encoder.GetBytes(data));
            hash = ToHexString(hmBytes);
        }

        return hash;
    }

    public static string ToHexString(byte[] array)
    {
        var hex = new StringBuilder(array.Length * 2);
        foreach (var b in array) hex.AppendFormat("{0:x2}", b);
        return hex.ToString();
    }

    public Task<ResponseMessageApi<object>> GetProductInfo(GetProviderProductInfo info)
    {
        throw new NotImplementedException();
    }

    public Task<MessageResponseBase> CheckPrePostPaid(string msisdn, string transCode, string providerCode)
    {
        throw new NotImplementedException();
    }
}

public class AppotaRequest
{
    public string PartnerRefId { get; set; }
    public string Telco { get; set; }
    public string TelcoServiceType { get; set; }
    public string PhoneNumber { get; set; }
    public decimal Amount { get; set; }

    public int Quantity { get; set; }
    public string ProductCode { get; set; }
    public string Siganture { get; set; }
}

public class Account
{
    public decimal balance { get; set; }
}

public class AppotaReponse
{
    public int ErrorCode { get; set; }

    public string Message { get; set; }

    public Transaction Transaction { get; set; }

    public List<Card> Cards { get; set; }

    public Account Account { get; set; }
}

public class Transaction
{
    public int Amount { get; set; }

    public int TopupAmount { get; set; }

    public string AppotapayTransId { get; set; }

    public string Phonenumber { get; set; }

    public string Time { get; set; }
}

public class Card
{
    public string Code { get; set; }

    public string Serial { get; set; }

    public string Expiry { get; set; }

    public decimal Value { get; set; }
}

public class Header
{
    public string typ { get; set; }

    public string alg { get; set; }

    public string cty { get; set; }
}

public class Payload
{
    public string iss { get; set; }

    public string api_key { get; set; }

    public string jti { get; set; }

    public int exp { get; set; }
}