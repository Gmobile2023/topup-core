using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using GMB.Topup.Shared;
using GMB.Topup.Shared.CacheManager;
using GMB.Topup.Shared.Dtos;
using GMB.Topup.TopupGw.Contacts.ApiRequests;
using GMB.Topup.TopupGw.Contacts.Dtos;
using GMB.Topup.TopupGw.Contacts.Enums;
using GMB.Topup.TopupGw.Domains.BusinessServices;
using Microsoft.Extensions.Logging;
using MongoDB.Driver.Core.WireProtocol.Messages;
using ServiceStack;

namespace GMB.Topup.TopupGw.Components.Connectors.VNPTPay;

public class VnptPayConnector : GatewayConnectorBase
{
    private readonly ILogger<VnptPayConnector> _logger;
    private readonly ICacheManager _cacheManager;

    public VnptPayConnector(ITopupGatewayService topupGatewayService, ILogger<VnptPayConnector> logger,
        ICacheManager cacheManager) : base(
        topupGatewayService)
    {
        _logger = logger;
        _cacheManager = cacheManager;
    }

    public override async Task<MessageResponseBase> TopupAsync(TopupRequestLogDto topupRequestLog,
        ProviderInfoDto providerInfo)
    {
        var responseMessage = new MessageResponseBase();
        _logger.LogInformation("Get topup request: " + topupRequestLog.ToJson());
        if (!TopupGatewayService.ValidConnector(ProviderConst.VNPTPAY, topupRequestLog.ProviderCode))
        {
            _logger.LogInformation("{TransCode}-{TransRef}-{ProviderCode}-VnptPayConnector ProviderConnector not valid",
                topupRequestLog.TransCode, topupRequestLog.TransRef, providerInfo.ProviderCode);
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
            };
        }

        string productCode = topupRequestLog.ProductCode;

        decimal transAmount = Convert.ToDecimal(topupRequestLog.TransAmount);
        if (providerInfo.ProviderServices == null)
        {
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Giao dịch lỗi. Chưa cấu hình sản phẩm"
            };
        }
        var sp = topupRequestLog.ProductCode.Split('_');
        string keyCode = string.Join('_', sp.Skip(0).Take(2));
        var providerService = providerInfo.ProviderServices.Find(p => p.ProductCode == keyCode);
        string data;
        if (providerService != null)
            data = providerService.ServiceCode;
        else
        {
            _logger.LogWarning(
                $"{topupRequestLog.TransCode} VnptPayConnector-ProviderService with ProductCode [{topupRequestLog.ProductCode}] is null");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Giao dịch lỗi. Nhà cung cấp không có thông tin sản phẩm tương ứng"
            };
        }
        var provider = new ProviderVnptItem()
        {
            Provider = data.Split('|')[2]
        };
        string function = "v1.0.6/relief_debt_partner_prepaid";
        var request = new VnptPayRequest()
        {
            Action = "RELIEF",
            Version = "1.0.6",
            PartnerId = providerInfo.ApiUser,
            ServiceId = data.Split('|')[0],
            ServiceProviderId = data.Split('|')[1],
            PaymentCode = topupRequestLog.ReceiverInfo,
            Options = string.Empty,
            BillMonth = string.Empty,
            BillAmount = topupRequestLog.TransAmount.ToString(),
            ChannelId = "1",
            TransDateTime = DateTime.Now.ToString("yyyyMMddHHmmss"),
            TransRequestId = topupRequestLog.TransCode,
            Additional = providerInfo.Username,
        };

        try
        {
            string keySignature = string.Join("|", request.Action, request.Version, request.PartnerId, request.ServiceId,
                request.ServiceProviderId, request.PaymentCode, request.Options, request.BillMonth, request.BillAmount,
                request.TransRequestId, request.ChannelId, request.Additional, request.TransDateTime, providerInfo.PrivateKeyFile);

            var dataSign = HashSHA256(keySignature);
            request.SecureCode = dataSign;

            var response = await CallApiRequest(providerInfo.ApiUrl, function, providerInfo.ApiPassword, request);
            if (response.ResponseCode == "00")
            {
                responseMessage.TransCode = response.TransRequestId;
                responseMessage.PaymentAmount = topupRequestLog.TransAmount;
                responseMessage.ResponseCode = ResponseCodeConst.Success;
                responseMessage.ProviderResponseTransCode = response.TransResponseId;
                responseMessage.ResponseMessage = "Giao dịch thành công";
                topupRequestLog.Status = TransRequestStatus.Success;
                _logger.LogInformation(topupRequestLog.TransCode + " VnptPayConnector-TopupReturnValue: " +
                                       response.ResponseCode + "|" + response.Description);
            }
            else
            {
                var reResult = await TopupGatewayService.GetResponseMassageCacheAsync(ProviderConst.VNPTPAY, response.ResponseCode, topupRequestLog.TransCode);
                if (reResult.ResponseCode is ResponseCodeConst.ResponseCode_Failed
                    or ResponseCodeConst.ResponseCode_Cancel
                    or ResponseCodeConst.ResponseCode_00)
                {
                    responseMessage.ResponseCode = ResponseCodeConst.Error;
                    responseMessage.ResponseMessage = reResult != null
                        ? reResult.ResponseName
                        : "Giao dịch không thành công từ nhà cung cấp";
                    topupRequestLog.Status = TransRequestStatus.Fail;
                }
                else
                {
                    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                    responseMessage.ResponseMessage =
                        reResult != null ? reResult.ResponseName : "Giao dịch chưa có kết quả";
                    topupRequestLog.Status = TransRequestStatus.Timeout;
                }
            }

            _logger.LogInformation(
                $"TransCode= {topupRequestLog.TransCode} - VnptPayConnector-TopupReturn: {responseMessage.ResponseCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"TransCode= {topupRequestLog.TransCode} - VnptPayConnector - Exception : {ex.Message}");
            responseMessage.ResponseCode = ResponseCodeConst.Error;
            topupRequestLog.Status = TransRequestStatus.Fail;
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
        var responseMessage = new MessageResponseBase();
        responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
        responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả xử lý.";
        return responseMessage;
    }

    public override async Task<NewMessageResponseBase<InvoiceResultDto>> QueryAsync(
        PayBillRequestLogDto payBillRequestLog)
    {
        if (!TopupGatewayService.ValidConnector(ProviderConst.VNPTPAY, payBillRequestLog.ProviderCode))
            return new NewMessageResponseBase<InvoiceResultDto>()
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error,
                    "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ")
            };

        var responseMessage = new NewMessageResponseBase<InvoiceResultDto>();
        var providerInfo = await TopupGatewayService.ProviderInfoCacheGetAsync(payBillRequestLog.ProviderCode);
        if (providerInfo == null)
        {
            responseMessage.ResponseStatus.ErrorCode = ResponseCodeConst.Error;
            responseMessage.ResponseStatus.Message = "Không tồn tại cấu hình của nhà cung cấp";
            return responseMessage;
        }

        string productCode = payBillRequestLog.ProductCode;

        if (providerInfo.ProviderServices == null)
        {
            responseMessage.ResponseStatus.ErrorCode = ResponseCodeConst.Error;
            responseMessage.ResponseStatus.Message = "Giao dịch không thành công";
            return responseMessage;
        }

        var providerService = providerInfo.ProviderServices.Find(p => p.ProductCode == productCode);
        var data = string.Empty;
        if (providerService != null)
        {
            data = providerService.ServiceCode;
        }
        else
        {
            _logger.LogWarning(
                $"{payBillRequestLog.TransCode} VnptPayConnector-ProviderService with ProductCode [{payBillRequestLog.ProductCode}] is null");
            responseMessage.ResponseStatus.ErrorCode = ResponseCodeConst.Error;
            responseMessage.ResponseStatus.Message = "Giao dịch không thành công";
            return responseMessage;
        }

        try
        {
            //if (string.IsNullOrEmpty(payBillRequestLog.TransCode))
            //    payBillRequestLog.TransCode = DateTime.Now.ToString("yyyyMMddHHmmssfff");

            //var queryInfo = await QueryBill(providerInfo, data.Split('|')[1], payBillRequestLog.ReceiverInfo,
            //    payBillRequestLog.TransCode);
            //if (queryInfo != null)
            //{
            //    string billNumber = queryInfo.PeriodDetails != null
            //        ? queryInfo.PeriodDetails.FirstNonDefault()?.BillNumber
            //        : "";
            //    if (!string.IsNullOrEmpty(billNumber))
            //    {
            //        var key =
            //            $"PayGate_BillQuery:Items:{payBillRequestLog.ProviderCode}_{data.Split('|')[1]}_{payBillRequestLog.ReceiverInfo}";
            //        await _cacheManager.AddEntity(key, billNumber, TimeSpan.FromMinutes(15));
            //    }

            //    responseMessage.ResponseStatus.ErrorCode = ResponseCodeConst.Success;
            //    responseMessage.Results = queryInfo;
            //}
            //else
            //{
            //    responseMessage.ResponseStatus.ErrorCode = ResponseCodeConst.Error;
            //}

            _logger.LogInformation(
                $"TransCode= {payBillRequestLog.TransCode}|VnptPayConnector_QueryAsyncReturn: {responseMessage.ResponseStatus.ErrorCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                $"TransCode= {payBillRequestLog.TransCode}|VnptPayConnector_QueryAsync_Exception : {ex.Message}");
            responseMessage.ResponseStatus.ErrorCode = ResponseCodeConst.ResponseCode_WaitForResult;
        }

        return responseMessage;
    }

    public override async Task<MessageResponseBase> CardGetByBatchAsync(CardRequestLogDto cardRequestLog)
    {
        _logger.LogInformation($"{cardRequestLog.TransCode} Get card request: " + cardRequestLog.ToJson());
        var responseMessage = new MessageResponseBase();

        if (!TopupGatewayService.ValidConnector(ProviderConst.VNPTPAY, cardRequestLog.ProviderCode))
        {
            _logger.LogError(
                $"{cardRequestLog.ProviderCode}-{cardRequestLog.TransCode}-VnptPayConnector ProviderConnector not valid");
            responseMessage.ResponseCode = ResponseCodeConst.Error;
            responseMessage.ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ";
            return responseMessage;
        }

        var providerInfo = await TopupGatewayService.ProviderInfoCacheGetAsync(cardRequestLog.ProviderCode);
        if (providerInfo == null)
        {
            _logger.LogError($"{cardRequestLog.ProviderCode}-{cardRequestLog.TransCode}-VnptPayConnector ProviderConnector not valid");
            responseMessage.ResponseCode = ResponseCodeConst.Error;
            responseMessage.ResponseMessage = "Không tồn tại cấu hình của nhà cung cấp";
            return responseMessage;
        }

        if (providerInfo.ProviderServices == null)
        {
            _logger.LogWarning(
                $"{cardRequestLog.TransCode} VnptPayConnector - ProviderService with ProductCode [{cardRequestLog.ProductCode}] is null");
            responseMessage.ResponseCode = ResponseCodeConst.Error;
            responseMessage.ResponseMessage = "Giao dịch không thành công sản phẩm chưa được cấu hình";
            return responseMessage;
        }

        var providerService = providerInfo.ProviderServices.Find(p => p.ProductCode == cardRequestLog.ProductCode);
        if (providerService == null)
        {
            _logger.LogWarning(
                $"{cardRequestLog.TransCode} VnptPayConnector - ProviderService with ProductCode [{cardRequestLog.ProductCode}] is null");
            responseMessage.ResponseCode = ResponseCodeConst.Error;
            responseMessage.ResponseMessage = "Giao dịch lỗi. Nhà cung cấp không có thông tin sản phẩm tương ứng.";
            return responseMessage;
        }

        var data = providerService.ServiceCode;
        string function = "v1.0.6/relief_debt_partner_prepaid";
        var provider = new ProviderVnptItem()
        {
            Provider = data.Split('|')[2]
        };
        var request = new VnptPayRequest()
        {
            Action = "RELIEF",
            Version = "1.0.6",
            PartnerId = providerInfo.ApiUser,
            ServiceId = data.Split('|')[0],
            ServiceProviderId = data.Split('|')[1],
            PaymentCode = cardRequestLog.ReceiverInfo,
            Options = provider.ToJson(),
            BillMonth = string.Empty,
            BillAmount = cardRequestLog.TransAmount.ToString(),
            ChannelId = "1",
            TransDateTime = DateTime.Now.ToString("yyyyMMddHHmmss"),
            TransRequestId = cardRequestLog.TransCode,
            Additional = "TEST",
        };

        _logger.LogInformation(
            $"TransCode= {request.TransRequestId} - VnptPayConnector.Card object send: {request.ToJson()}");

        string inputData =
                  $"{request.Action}|{request.Version}|{request.PartnerId}|{request.ServiceId}|{request.ServiceProviderId}|{request.PaymentCode}|{request.Options}|" +
                  $"{request.BillMonth}|{request.BillAmount}|{request.ChannelId}|{request.TransDateTime}|{request.TransRequestId}|{request.Additional}";
        var dataSign = Sign(inputData, "./" + providerInfo.PrivateKeyFile);
        request.SecureCode = dataSign;

        var reporse = await CallApiRequest(providerInfo.ApiUrl, function, providerInfo.ApiPassword, request);
        if (reporse.ResponseCode == "00")
        {
            responseMessage.ResponseCode = ResponseCodeConst.Success;
            responseMessage.ResponseMessage = "Giao dịch thành công";
            cardRequestLog.Status = TransRequestStatus.Success;
            try
            {
                //var dataInfo = Cryptography.TripleDesDecrypt(reporse.dataInfo, providerInfo.PublicKey);
                //var cardData = dataInfo.FromJson<CardData>();
                //var cardList = new List<CardRequestResponseDto>();
                //foreach (var card in cardData.ListCard)
                //{
                //    cardList.Add(new CardRequestResponseDto
                //    {
                //        CardCode = card.Code,
                //        Serial = card.Serial,
                //        ExpireDate = card.ExpriredDate.Split('T')[0],
                //        ExpiredDate = DateTime.ParseExact(card.ExpriredDate, "yyyy-MM-ddTHH:mm:ss",
                //            CultureInfo.InvariantCulture),
                //        CardValue = card.Value.ToString(),
                //    });
                //}

                //responseMessage.Payload = cardList;
            }
            catch (Exception e)
            {
                _logger.LogError(
                    $"TransCode= {cardRequestLog.TransCode} Vtc365Connector.Error parsing cards: {e.Message}");
            }
        }
        else
        {
            var reResult = await TopupGatewayService.GetResponseMassageCacheAsync(ProviderConst.VNPTPAY,
                reporse.ResponseCode, cardRequestLog.TransCode);

            if (reResult.ResponseCode is ResponseCodeConst.ResponseCode_Failed
                    or ResponseCodeConst.ResponseCode_Cancel
                    or ResponseCodeConst.ResponseCode_00)
            {
                responseMessage.ResponseCode = ResponseCodeConst.Error;
                responseMessage.ResponseMessage =
                    reResult != null ? reResult.ResponseName : "Giao dịch không thành công từ nhà cung cấp";
                cardRequestLog.Status = TransRequestStatus.Fail;
            }
            else
            {
                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                responseMessage.ResponseMessage = reResult != null ? reResult.ResponseName : "Giao dịch chưa có kết quả";
                cardRequestLog.Status = TransRequestStatus.Timeout;
            }
        }

        await TopupGatewayService.CardRequestLogUpdateAsync(cardRequestLog);
        return responseMessage;
    }

    public override async Task<MessageResponseBase> CheckBalanceAsync(string providerCode, string transCode)
    {
        return new MessageResponseBase(ResponseCodeConst.Error, "Kênh này ko sẵn sang dịch vụ này");
    }

    public override async Task<MessageResponseBase> PayBillAsync(PayBillRequestLogDto payBillRequestLog)
    {
        return new MessageResponseBase(ResponseCodeConst.Error, "Kênh này ko sẵn sang dịch vụ này");
    }

    public override async Task<ResponseMessageApi<object>> GetProductInfo(GetProviderProductInfo info)
    {
        throw new NotImplementedException();
    }

    private async Task<VnptResponse> CallApiRequest(string url, string fuction, string key, VnptPayRequest request)
    {
        try
        {
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            var client = new JsonServiceClient(url) { Timeout = TimeSpan.FromMinutes(10) };
            client.AddHeader("Authorization", $"Bearer {key}");
            string JsonData = request.ToJson();
            var result = await client.PostAsync<object>(fuction, JsonData);
            _logger.LogInformation($"{request.TransRequestId} CallApiRequest_VnptPayConnector - Reponse {fuction} result: " +
                result.ToJson());
            return result.ConvertTo<VnptResponse>();
        }
        catch (Exception ex)
        {
            _logger.LogError($"{request.TransRequestId} CallApiRequest_VnptPayConnector - Reponse {fuction} exception : " +
                             ex.Message);
            return new VnptResponse()
            {
                ResponseCode = "501102",
            };
        }
    }

    private string Sign(string dataToSign, string privateFile)
    {
        try
        {
            var privateKey = System.IO.File.ReadAllText("files/" + privateFile);
            var privateKeyBlocks = privateKey.Split("-", StringSplitOptions.RemoveEmptyEntries);
            var key = privateKeyBlocks[1].Replace("\r\n", "");
            var privateKeyBytes = Convert.FromBase64String(privateKeyBlocks[1]);
            using var rsa = RSA.Create();

            if (privateKeyBlocks[0] == "BEGIN PRIVATE KEY")
                rsa.ImportPkcs8PrivateKey(privateKeyBytes, out _);
            else if (privateKeyBlocks[0] == "BEGIN RSA PRIVATE KEY") rsa.ImportRSAPrivateKey(privateKeyBytes, out _);

            var sig = rsa.SignData(
                Encoding.UTF8.GetBytes(dataToSign),
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            var signature = Convert.ToBase64String(sig);

            return signature;
        }
        catch (Exception ex)
        {
            _logger.LogError($"PrivateKey= {privateFile} Sign VNPTPay - Exception: {ex}");
            return string.Empty;
        }
    }

    private string HashSHA256(string value)
    {
        StringBuilder Sb = new StringBuilder();
        using (SHA256 hash = SHA256.Create())
        {
            Encoding enc = Encoding.UTF8;
            Byte[] result = hash.ComputeHash(enc.GetBytes(value));
            foreach (Byte b in result)
                Sb.Append(b.ToString("x2"));
        }
        return Sb.ToString();
    }

    [DataContract]
    internal class VnptPayRequest
    {


        [DataMember(Name = "VERSION", Order = 1)]
        public string Version { get; set; }

        [DataMember(Name = "PARTNER_ID", Order = 2)]
        public string PartnerId { get; set; }

        [DataMember(Name = "SERVICE_ID", Order = 3)]
        public string ServiceId { get; set; }

        [DataMember(Name = "BILL_AMOUNT", Order = 4)]
        public string BillAmount { get; set; }

        [DataMember(Name = "SERVICE_PROVIDER_ID", Order = 5)]
        public string ServiceProviderId { get; set; }

        [DataMember(Name = "CHANNEL_ID", Order = 6)]
        public string ChannelId { get; set; }

        [DataMember(Name = "BILL_MONTH", Order = 7)]
        public string BillMonth { get; set; }

        [DataMember(Name = "PAYMENT_CODE", Order = 8)]
        public string PaymentCode { get; set; }

        [DataMember(Name = "ADDITIONAL_INFO", Order = 9)]
        public string Additional { get; set; }

        [DataMember(Name = "TRANS_DATE_TIME", Order = 10)]
        public string TransDateTime { get; set; }


        [DataMember(Name = "TRANS_REQUEST_ID", Order = 11)]
        public string TransRequestId { get; set; }

        [DataMember(Name = "ACTION", Order = 12)]
        public string Action { get; set; }

        [DataMember(Name = "OPTIONS", Order = 13)]
        public string Options { get; set; }

        [DataMember(Name = "SECURE_CODE", Order = 14)]
        public string SecureCode { get; set; }
    }

    [DataContract]
    internal class VnptResponse
    {
        [DataMember(Name = "RESPONSE_CODE")]
        public string ResponseCode { get; set; }

        [DataMember(Name = "DESCRIPTION")]
        public string Description { get; set; }

        [DataMember(Name = "TRANS_DATE_TIME")]
        public string TransDateTime { get; set; }

        [DataMember(Name = "TRANS_REQUEST_ID")]
        public string TransRequestId { get; set; }


        [DataMember(Name = "TRANS_RESPONSE_ID")]
        public string TransResponseId { get; set; }

        [DataMember(Name = "CARD_DETAILS")]
        public List<CardVnptItem> Details { get; set; }

    }

    [DataContract]
    internal class ProviderVnptItem
    {
        [DataMember(Name = "PROVIDER_DT")]
        public string Provider { get; set; }
    }


    [DataContract]
    internal class CardVnptItem
    {
        [DataMember(Name = "SERIAL")]
        public string Serial { get; set; }
        [DataMember(Name = "PIN_CODE")]
        public string PinCode { get; set; }

        [DataMember(Name = "EXPIRED_DATE")]
        public string ExpireaDate { get; set; }

        [DataMember(Name = "PRODUCT_CODE")]
        public string ProductCode { get; set; }

        [DataMember(Name = "SECURE_CODE")]
        public string SecureCode { get; set; }

    }
}