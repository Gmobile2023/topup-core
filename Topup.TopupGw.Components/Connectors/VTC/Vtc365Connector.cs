using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
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
using Topup.TopupGw.Components.Connectors.VTC365;

namespace Topup.TopupGw.Components.Connectors.VTC;

public class Vtc365Connector : GatewayConnectorBase
{
    private readonly ILogger<Vtc365Connector> _logger;
    private readonly ICacheManager _cacheManager;

    public Vtc365Connector(ITopupGatewayService topupGatewayService, ILogger<Vtc365Connector> logger,
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
        if (!TopupGatewayService.ValidConnector(ProviderConst.VTC365, topupRequestLog.ProviderCode))
        {
            _logger.LogError(
                "{TransCode}-{TransRef}-{ProviderCode}-Vtc365Connector ProviderConnector not valid",
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

        var providerService = providerInfo.ProviderServices.Find(p => p.ProductCode == productCode);
        string data;
        if (providerService != null)
            data = providerService.ServiceCode;
        else
        {
            _logger.LogWarning(
                $"{topupRequestLog.TransCode} Vtc365Connector-ProviderService with ProductCode [{topupRequestLog.ProductCode}] is null");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Giao dịch lỗi. Nhà cung cấp không có thông tin sản phẩm tương ứng"
            };
        }

        string function = "share/Pay/topup-mobile";
        var request = new Request365Request()
        {
            categoryID = data.Split('|')[1],
            productID = data.Split('|')[0],
            customerID = topupRequestLog.ReceiverInfo,
            partnerCode = providerInfo.ApiUser,
            productAmount = transAmount.ToString(),
            partnerTransID = topupRequestLog.TransCode,
            partnerTransDate = DateTime.Now.ToString("yyyyMMddHHmmss"),
            data = ""
        };

        if (topupRequestLog.ServiceCode.Contains("TOPUP_GAME"))
            function = "share/Pay/topup-game";
        try
        {
            string inputData =
                $"{request.partnerCode}|{request.categoryID}|{request.productID}|{request.productAmount}|{request.customerID}|{request.partnerTransID}|{request.partnerTransDate}|{request.data}";
            var dataSign = Sign(inputData, "./" + providerInfo.PrivateKeyFile);
            request.dataSign = dataSign;

            var response = CallApiRequest(providerInfo.ApiUrl, function, request);
            if (response.status == 1)
            {
                responseMessage.TransCode = response.partnerTransID;
                responseMessage.PaymentAmount = topupRequestLog.TransAmount;
                responseMessage.ResponseCode = ResponseCodeConst.Success;
                responseMessage.ResponseMessage = "Giao dịch thành công";
                topupRequestLog.Status = TransRequestStatus.Success;
                _logger.LogInformation(topupRequestLog.TransCode + " Vtc365Connector-TopupReturnValue: " +
                                       response.status + "|" + response.responseCode);
            }
            else if (response.status == -1)
            {
                var errCodes = Array.Empty<int>();
                if (!string.IsNullOrEmpty(providerInfo.ExtraInfo))
                    errCodes = ConvertArrayCode(providerInfo.ExtraInfo);

                var reResult = await TopupGatewayService.GetResponseMassageCacheAsync(ProviderConst.VTC365,
                    response.responseCode.ToString(), topupRequestLog.TransCode);
                if (errCodes.Contains(response.responseCode))
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
            else
            {
                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                responseMessage.ResponseMessage = "Giao dịch chưa có kết quả";
                topupRequestLog.Status = TransRequestStatus.Timeout;
            }

            _logger.LogInformation(
                $"TransCode= {topupRequestLog.TransCode}|Vtc365Connector-TopupReturn: {responseMessage.ResponseCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"TransCode= {topupRequestLog.TransCode}|Vtc365Connector-Exception : {ex.Message}");
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
        try
        {
            _logger.LogInformation($"{transCodeToCheck}-{transCode}-{providerCode} Vtc365Connector check request: ");
            if (providerInfo == null)
                providerInfo = await TopupGatewayService.ProviderInfoCacheGetAsync(providerCode);

            if (providerInfo == null ||
                !TopupGatewayService.ValidConnector(ProviderConst.VTC365, providerInfo.ProviderCode))
            {
                _logger.LogError(
                    $"{transCodeToCheck}-{transCode}-{providerCode}-Vtc365Connector ProviderConnector not valid");
                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                return responseMessage;
            }

            var request = new Request365Request()
            {
                categoryID = "",
                productID = "",
                customerID = "",
                partnerCode = providerInfo.ApiUser,
                productAmount = "",
                partnerTransID = transCodeToCheck,
                partnerTransDate = DateTime.Now.ToString("yyyyMMddHHmmss"),
                data = ""
            };

            string inputData =
                $"{request.partnerCode}|{request.categoryID}|{request.productID}|{request.productAmount}|{request.customerID}|{request.partnerTransID}|{request.partnerTransDate}|{request.data}";
            var dataSign = Sign(inputData, "./" + providerInfo.PrivateKeyFile);
            request.dataSign = dataSign;
            var resultProvider = new ResponseProvider();
            var checkResult = CallApiRequest(providerInfo.ApiUrl, "share/GetInfo/check-partner-order", request);
            resultProvider.Code = checkResult.responseCode.ToString();
            resultProvider.Message = checkResult.description.ToString();
            if (checkResult.status == 1 && !string.IsNullOrEmpty(checkResult.dataInfo))
            {
                responseMessage.ResponseCode = ResponseCodeConst.Success;
                responseMessage.ResponseMessage = "Giao dịch thành công";
                if (serviceCode.StartsWith("PIN"))
                {
                    try
                    {
                        var decodeBaseString = DecodeBaseString(checkResult.dataInfo);
                        var transInfo = decodeBaseString.FromJson<checkTranItem>();
                        request.partnerTransDate = DateTime.Now.ToString("yyyyMMddHHmmss");
                        request.data = transInfo.OrderID;
                        string inputDataCard =
                            $"{request.partnerCode}|{request.categoryID}|{request.productID}|{request.productAmount}|{request.customerID}|{request.partnerTransID}|{request.partnerTransDate}|{request.data}";
                        var dataSignCard = Sign(inputDataCard, "./" + providerInfo.PrivateKeyFile);
                        request.dataSign = dataSignCard;
                        var checkResultCard =
                            CallApiRequest(providerInfo.ApiUrl, "share/GetInfo/get-carddata", request);
                        if (checkResultCard.status == 1 && !string.IsNullOrEmpty(checkResultCard.dataInfo))
                        {
                            var dataInfo =
                                Cryptography.TripleDesDecrypt(checkResultCard.dataInfo, providerInfo.PublicKey);
                            var cardData = dataInfo.FromJson<CardData>();
                            var cardList = new List<CardRequestResponseDto>();
                            foreach (var card in cardData.ListCard)
                            {
                                cardList.Add(new CardRequestResponseDto
                                {
                                    CardCode = card.Code.EncryptTripDes(),
                                    Serial = card.Serial,
                                    ExpireDate = card.ExpriredDate.Split('T')[0],
                                    ExpiredDate = DateTime.ParseExact(card.ExpriredDate, "yyyy-MM-ddTHH:mm:ss",
                                        CultureInfo.InvariantCulture),
                                    CardValue = card.Value.ToString(),
                                });
                            }

                            resultProvider.PayLoad = cardList.ToJson();
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(
                            $"TransCode= {transCodeToCheck} Vtc365Connector.Error parsing cards: {e.Message}");
                    }
                }
            }
            else if (checkResult.status == -1)
            {
                var errCodes = new int[0];
                if (!string.IsNullOrEmpty(providerInfo.ExtraInfo))
                    errCodes = ConvertArrayCode(providerInfo.ExtraInfo);
                //var reResult = await TopupGatewayService.GetResponseMassageCacheAsync(ProviderConst.VTC365, checkResult.responseCode.ToString(), transCodeToCheck);
                if (errCodes.Contains(checkResult.responseCode))
                {
                    responseMessage.ResponseCode = ResponseCodeConst.Error;
                    responseMessage.ResponseMessage = "Giao dịch không thành công từ nhà cung cấp";
                }
                else
                {
                    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                    responseMessage.ResponseMessage = "Giao dịch chưa có kết quả";
                }
            }
            else
            {
                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                responseMessage.ResponseMessage = checkResult.description;
            }

            responseMessage.Payload = resultProvider;
            _logger.LogInformation(request.partnerTransID + " Vtc365Connector-CheckTransReturnValue: " +
                                   checkResult.status + "|" + checkResult.responseCode);
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

    public override async Task<NewMessageResponseBase<InvoiceResultDto>> QueryAsync(
        PayBillRequestLogDto payBillRequestLog)
    {
        if (!TopupGatewayService.ValidConnector(ProviderConst.VTC365, payBillRequestLog.ProviderCode))
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
                $"{payBillRequestLog.TransCode} Vtc365Connector-ProviderService with ProductCode [{payBillRequestLog.ProductCode}] is null");
            responseMessage.ResponseStatus.ErrorCode = ResponseCodeConst.Error;
            responseMessage.ResponseStatus.Message = "Giao dịch không thành công";
            return responseMessage;
        }

        try
        {
            if (string.IsNullOrEmpty(payBillRequestLog.TransCode))
                payBillRequestLog.TransCode = DateTime.Now.ToString("yyyyMMddHHmmssfff");

            var queryInfo = await QueryBill(providerInfo, data.Split('|')[1], payBillRequestLog.ReceiverInfo,
                payBillRequestLog.TransCode);
            if (queryInfo != null)
            {
                string billNumber = queryInfo.PeriodDetails != null
                    ? queryInfo.PeriodDetails.FirstNonDefault()?.BillNumber
                    : "";
                if (!string.IsNullOrEmpty(billNumber))
                {
                    var key =
                        $"PayGate_BillQuery:Items:{payBillRequestLog.ProviderCode}_{data.Split('|')[1]}_{payBillRequestLog.ReceiverInfo}";
                    await _cacheManager.AddEntity(key, billNumber, TimeSpan.FromMinutes(15));
                }

                responseMessage.ResponseStatus.ErrorCode = ResponseCodeConst.Success;
                responseMessage.Results = queryInfo;
            }
            else
            {
                responseMessage.ResponseStatus.ErrorCode = ResponseCodeConst.Error;
            }

            _logger.LogInformation(
                $"TransCode= {payBillRequestLog.TransCode}|Vtc365Connector_PayBillReturn: {responseMessage.ResponseStatus.ErrorCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                $"TransCode= {payBillRequestLog.TransCode}|Vtc365Connector_PayBill_Exception : {ex.Message}");
            responseMessage.ResponseStatus.ErrorCode = ResponseCodeConst.ResponseCode_WaitForResult;
        }

        return responseMessage;
    }

    public override async Task<MessageResponseBase> CardGetByBatchAsync(CardRequestLogDto cardRequestLog)
    {
        _logger.LogInformation($"{cardRequestLog.TransCode} Get card request: " + cardRequestLog.ToJson());
        var responseMessage = new MessageResponseBase();

        if (!TopupGatewayService.ValidConnector(ProviderConst.VTC365,
                cardRequestLog.ProviderCode.Split('-')[0].Split('_')[0]))
        {
            _logger.LogError(
                $"{cardRequestLog.ProviderCode}-{cardRequestLog.TransCode}-Vtc365Connector ProviderConnector not valid");
            responseMessage.ResponseCode = ResponseCodeConst.Error;
            responseMessage.ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ";
            return responseMessage;
        }

        var providerInfo = await TopupGatewayService.ProviderInfoCacheGetAsync(cardRequestLog.ProviderCode);
        if (providerInfo == null)
        {
            _logger.LogError(
                $"{cardRequestLog.ProviderCode}-{cardRequestLog.TransCode}-Vtc365Connector ProviderConnector not valid");
            responseMessage.ResponseCode = ResponseCodeConst.Error;
            responseMessage.ResponseMessage = "Không tồn tại cấu hình của nhà cung cấp";
            return responseMessage;
        }

        if (providerInfo.ProviderServices == null)
        {
            _logger.LogWarning(
                $"{cardRequestLog.TransCode} Vtc365Connector-ProviderService with ProductCode [{cardRequestLog.ProductCode}] is null");
            responseMessage.ResponseCode = ResponseCodeConst.Error;
            responseMessage.ResponseMessage = "Giao dịch không thành công sản phẩm chưa được cấu hình";
            return responseMessage;
        }

        var providerService = providerInfo.ProviderServices.Find(p => p.ProductCode == cardRequestLog.ProductCode);
        if (providerService == null)
        {
            _logger.LogWarning(
                $"{cardRequestLog.TransCode} Vtc365Connector-ProviderService with ProductCode [{cardRequestLog.ProductCode}] is null");
            responseMessage.ResponseCode = ResponseCodeConst.Error;
            responseMessage.ResponseMessage = "Giao dịch lỗi. Nhà cung cấp không có thông tin sản phẩm tương ứng.";
            return responseMessage;
        }

        var data = providerService.ServiceCode;
        string function = "share/Pay/buy-card";
        var request = new Request365Request
        {
            productID = data.Split('|')[0],
            categoryID = data.Split('|')[1],
            customerID = "",
            partnerCode = providerInfo.ApiUser,
            productAmount = cardRequestLog.TransAmount.ToString(),
            partnerTransID = cardRequestLog.TransCode,
            partnerTransDate = DateTime.Now.ToString("yyyyMMddHHmmss"),
            data = cardRequestLog.Quantity.ToString()
        };

        _logger.LogInformation(
            $"TransCode= {request.partnerTransID}|Vtc365Connector.Card object send: {request.ToJson()}");

        string inputData =
            $"{request.partnerCode}|{request.categoryID}|{request.productID}|{request.productAmount}|{request.customerID}|{request.partnerTransID}|{request.partnerTransDate}|{request.data}";
        var dataSign = Sign(inputData, "./" + providerInfo.PrivateKeyFile);
        request.dataSign = dataSign;

        var reporse = CallApiRequest(providerInfo.ApiUrl, function, request);
        if (reporse.status == 1)
        {
            responseMessage.ResponseCode = ResponseCodeConst.Success;
            responseMessage.ResponseMessage = "Giao dịch thành công";
            cardRequestLog.Status = TransRequestStatus.Success;
            try
            {
                var dataInfo = Cryptography.TripleDesDecrypt(reporse.dataInfo, providerInfo.PublicKey);
                var cardData = dataInfo.FromJson<CardData>();
                var cardList = new List<CardRequestResponseDto>();
                foreach (var card in cardData.ListCard)
                {
                    cardList.Add(new CardRequestResponseDto
                    {
                        CardCode = card.Code,
                        Serial = card.Serial,
                        ExpireDate = card.ExpriredDate.Split('T')[0],
                        ExpiredDate = DateTime.ParseExact(card.ExpriredDate, "yyyy-MM-ddTHH:mm:ss",
                            CultureInfo.InvariantCulture),
                        CardValue = card.Value.ToString(),
                    });
                }

                responseMessage.Payload = cardList;
            }
            catch (Exception e)
            {
                _logger.LogError(
                    $"TransCode= {cardRequestLog.TransCode} Vtc365Connector.Error parsing cards: {e.Message}");
            }
        }
        else if (reporse.status == -1)
        {
            var errCodes = new int[0];
            if (!string.IsNullOrEmpty(providerInfo.ExtraInfo))
                errCodes = ConvertArrayCode(providerInfo.ExtraInfo);

            var reResult = await TopupGatewayService.GetResponseMassageCacheAsync(ProviderConst.VTC365,
                reporse.responseCode.ToString(), cardRequestLog.TransCode);
            if (errCodes.Contains(reporse.responseCode))
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
        else
        {
            responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
            responseMessage.ResponseMessage = "Giao dịch chưa có kết quả";
            cardRequestLog.Status = TransRequestStatus.Timeout;
        }

        await TopupGatewayService.CardRequestLogUpdateAsync(cardRequestLog);
        return responseMessage;
    }

    public override async Task<MessageResponseBase> CheckBalanceAsync(string providerCode, string transCode)
    {
        var responseMessage = new MessageResponseBase();

        _logger.LogInformation($"{transCode}-{providerCode} Vtc365Connector check request: ");
        var providerInfo = await TopupGatewayService.ProviderInfoCacheGetAsync(providerCode);
        if (providerInfo == null ||
            !TopupGatewayService.ValidConnector(ProviderConst.VTC365, providerInfo.ProviderCode))
        {
            _logger.LogError($"{transCode}-{providerCode}-Vtc365Connector ProviderConnector not valid");
            responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
            responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
            return responseMessage;
        }

        var request = new Request365Request()
        {
            categoryID = "",
            productID = "",
            customerID = "",
            partnerCode = providerInfo.ApiUser,
            productAmount = "",
            partnerTransID = providerInfo.ApiUser + "_" + DateTime.Now.ToString("yyyyMMddHHmmss"),
            partnerTransDate = DateTime.Now.ToString("yyyyMMddHHmmss"),
            data = ""
        };

        string inputData =
            $"{request.partnerCode}|{request.categoryID}|{request.productID}|{request.productAmount}|{request.customerID}|{request.partnerTransID}|{request.partnerTransDate}|{request.data}";
        var dataSign = Sign(inputData, "./" + providerInfo.PrivateKeyFile);
        request.dataSign = dataSign;

        var checkResult = CallApiRequest(providerInfo.ApiUrl, "share/getInfo/get-Balance", request);
        var toupResult = checkResult.status == 1;
        _logger.LogInformation($"partnerTransID= {request.partnerTransID}|Vtc365Connector-CheckReturnValue: " +
                               checkResult.status + "|" + checkResult.responseCode);

        if (toupResult)
        {
            responseMessage.Payload = Convert.ToDouble(checkResult.balance);
            responseMessage.ResponseCode = ResponseCodeConst.Success;
            responseMessage.ResponseMessage = "Thành công";
        }
        else
            responseMessage.ResponseCode = ResponseCodeConst.Error;

        _logger.LogInformation($"Vtc365Connector-Query Balance: {responseMessage.ToJson()}");
        return responseMessage;
    }

    public override async Task<MessageResponseBase> PayBillAsync(PayBillRequestLogDto payBillRequestLog)
    {
        _logger.LogInformation("Get Paybill request: " + payBillRequestLog.ToJson());
        if (!TopupGatewayService.ValidConnector(ProviderConst.VTC365, payBillRequestLog.ProviderCode))
            return MessageResponseBase.Error("Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ");

        var responseMessage = new MessageResponseBase(ResponseCodeConst.ResponseCode_WaitForResult, "");

        var providerInfo = await TopupGatewayService.ProviderInfoCacheGetAsync(payBillRequestLog.ProviderCode);
        if (providerInfo == null)
        {
            responseMessage.ResponseCode = ResponseCodeConst.Error;
            responseMessage.ResponseMessage = "Không tồn tại cấu hình của nhà cung cấp";
            return responseMessage;
        }

        string productCode = payBillRequestLog.ProductCode;
        decimal transAmount = Convert.ToDecimal(payBillRequestLog.TransAmount);
        if (providerInfo.ProviderServices == null)
        {
            responseMessage.ResponseCode = ResponseCodeConst.Error;
            responseMessage.ResponseMessage = "Giao dịch không thành công";
            return responseMessage;
        }

        var providerService = providerInfo.ProviderServices.Find(p => p.ProductCode == productCode);
        var data = string.Empty;
        if (providerService != null)
            data = providerService.ServiceCode;
        else
        {
            _logger.LogWarning(
                $"{payBillRequestLog.TransCode} Vtc365Connector-ProviderService with ProductCode [{payBillRequestLog.ProductCode}] is null");
            responseMessage.ResponseCode = ResponseCodeConst.Error;
            responseMessage.ResponseMessage = "Giao dịch lỗi. Nhà cung cấp không có thông tin sản phẩm tương ứng.";
            responseMessage.ProviderResponseCode = ResponseCodeConst.Error;
            responseMessage.ProviderResponseMessage = "Sản phẩm cấu hình của đối tác chưa tồn tại";
            return responseMessage;
        }

        string billNumber = "";
        var key =
            $"PayGate_BillQuery:Items:{payBillRequestLog.ProviderCode}_{data.Split('|')[1]}_{payBillRequestLog.ReceiverInfo}";
        if (data.Split('|').Length >= 3)
        {
            billNumber = await _cacheManager.GetEntity<string>(key);
            if (string.IsNullOrEmpty(billNumber))
            {
                var resultQuery = await QueryBill(providerInfo, data.Split('|')[1],
                    payBillRequestLog.ReceiverInfo,
                    payBillRequestLog.TransCode + "_" + DateTime.Now.ToString("HHmmss"));
                if (resultQuery != null)
                    billNumber = resultQuery.PeriodDetails.FirstNonDefault()?.BillNumber;
            }

            if (string.IsNullOrEmpty(billNumber))
            {
                responseMessage.ResponseCode = ResponseCodeConst.Error;
                responseMessage.ResponseMessage = "Giao dịch không thành công";
                responseMessage.ProviderResponseCode = ResponseCodeConst.Error;
                responseMessage.ProviderResponseMessage = "Không truy vấn được thông tin hóa đơn.";
                return responseMessage;
            }
        }

        string function = "share/Pay/pay-bill";
        var newtopup = new Request365Request()
        {
            categoryID = data.Split('|')[1],
            productID = data.Split('|')[0],
            customerID = payBillRequestLog.ReceiverInfo,
            partnerCode = providerInfo.ApiUser,
            productAmount = transAmount.ToString(),
            partnerTransID = payBillRequestLog.TransCode,
            partnerTransDate = DateTime.Now.ToString("yyyyMMddHHmmss"),
            data = billNumber
        };

        try
        {
            string inputData =
                $"{newtopup.partnerCode}|{newtopup.categoryID}|{newtopup.productID}|{newtopup.productAmount}|{newtopup.customerID}|{newtopup.partnerTransID}|{newtopup.partnerTransDate}|{newtopup.data}";
            var dataSign = Sign(inputData, "./" + providerInfo.PrivateKeyFile);
            newtopup.dataSign = dataSign;

            var reporse = CallApiRequest(providerInfo.ApiUrl, function, newtopup);
            responseMessage.ResponseCode = reporse.responseCode.ToString();
            responseMessage.ResponseMessage = reporse.description;

            if (reporse.status == 1)
            {
                responseMessage.TransCode = reporse.partnerTransID;
                responseMessage.ResponseMessage = "Giao dịch thành công";
                responseMessage.PaymentAmount = payBillRequestLog.TransAmount;
                responseMessage.ResponseCode = ResponseCodeConst.Success;

                payBillRequestLog.Status = TransRequestStatus.Success;
                _logger.LogInformation(payBillRequestLog.TransCode + " Vtc365Connector_PayBillReturnValue: " +
                                       reporse.status + "|" + reporse.responseCode);

                if (!string.IsNullOrEmpty(billNumber))
                    await _cacheManager.ClearCache(key);
            }
            else if (reporse.status == -1)
            {
                var errCodes = new int[0];
                if (!string.IsNullOrEmpty(providerInfo.ExtraInfo))
                    errCodes = ConvertArrayCode(providerInfo.ExtraInfo);

                var reResult = await TopupGatewayService.GetResponseMassageCacheAsync(ProviderConst.VTC365,
                    reporse.responseCode.ToString(), payBillRequestLog.TransCode);
                if (errCodes.Contains(reporse.responseCode))
                {
                    responseMessage.ResponseCode = ResponseCodeConst.Error;
                    responseMessage.ResponseMessage = reResult != null
                        ? reResult.ResponseName
                        : "Giao dịch không thành công từ nhà cung cấp";
                    payBillRequestLog.Status = TransRequestStatus.Fail;
                }
                else
                {
                    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                    responseMessage.ResponseMessage =
                        reResult != null ? reResult.ResponseName : "Giao dịch chưa có kết quả";
                    payBillRequestLog.Status = TransRequestStatus.Timeout;
                }
            }
            else
            {
                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                responseMessage.ResponseMessage = "Giao dịch chưa có kết quả";
                payBillRequestLog.Status = TransRequestStatus.Timeout;
            }

            _logger.LogInformation(
                $"TransCode= {payBillRequestLog.TransCode}|Vtc365Connector_PayBillReturn: {responseMessage.ResponseCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                $"TransCode= {payBillRequestLog.TransCode}|Vtc365Connector_PayBill_Exception : {ex.Message}");
            responseMessage.ResponseCode = ResponseCodeConst.Error;
            payBillRequestLog.Status = TransRequestStatus.Fail;
        }

        await TopupGatewayService.PayBillRequestLogUpdateAsync(payBillRequestLog);

        return responseMessage;
    }

    private async Task<InvoiceResultDto> QueryBill(ProviderInfoDto providerInfo, string categoryId, string receiverInfo,
        string transCode)
    {
        try
        {
            string function = "share/GetInfo/check-bill-info";
            var request = new Request365Request()
            {
                categoryID = categoryId,
                productID = "",
                customerID = receiverInfo,
                partnerCode = providerInfo.ApiUser,
                productAmount = "",
                partnerTransID = transCode,
                partnerTransDate = DateTime.Now.ToString("yyyyMMddHHmmss"),
                data = ""
            };

            string inputData =
                $"{request.partnerCode}|{request.categoryID}|{request.productID}|{request.productAmount}|{request.customerID}|{request.partnerTransID}|{request.partnerTransDate}|{request.data}";
            var dataSign = Sign(inputData, "./" + providerInfo.PrivateKeyFile);
            request.dataSign = dataSign;

            var response = CallApiRequest(providerInfo.ApiUrl, function, request);
            if (response.status == 1)
            {
                _logger.LogInformation(transCode + " Vtc365Connector_QueryPayBillReturnValue: " +
                                       response.status + "|" + response.responseCode);
                var infoString = DecodeBaseString(response.dataInfo);
                var resultData = infoString.FromJson<BillItem>();
                var details = new List<PeriodDto>();
                foreach (var d in resultData.bills)
                {
                    details.Add(new PeriodDto()
                    {
                        Amount = Convert.ToDecimal(d.amount),
                        Period = d.period,
                        BillType = d.bill_type,
                        BillNumber = d.bill_number,
                    });
                }

                string billNumber = resultData.bills.FirstOrDefault()?.bill_number;
                return new InvoiceResultDto()
                {
                    Address = resultData.customer.customer_address,
                    Amount = details.Sum(c => c.Amount),
                    CustomerName = resultData.customer.customer_name,
                    CustomerReference = resultData.customer.customer_code,
                    Period = details.FirstOrDefault()?.Period,
                    PeriodDetails = details,
                };
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError($"TransCode= {transCode}|Vtc365Connector_Query_Exception : {ex.Message}");
            return null;
        }
    }

    public override async Task<ResponseMessageApi<object>> GetProductInfo(GetProviderProductInfo info)
    {
        var responseMessage = new ResponseMessageApi<object>();
        if (!TopupGatewayService.ValidConnector(ProviderConst.VTC365, info.ProviderCode.Split('-')[0].Split('_')[0]))
        {
            _logger.LogInformation($"{info.ProviderCode}-Vtc365Connector ProviderConnector not valid");
            return new ResponseMessageApi<object>()
            {
                Error = new ErrorMessage()
                {
                    Code = 0,
                    Message = "",
                }
            };
        }

        var providerInfo = await TopupGatewayService.ProviderInfoCacheGetAsync(info.ProviderCode);
        if (providerInfo == null)
        {
            _logger.LogInformation($"{info.ProviderCode}-Vtc365Connector ProviderConnector not valid");
            responseMessage.Error = new ErrorMessage()
            {
                Code = 0,
            };
            return responseMessage;
        }

        var reponse = await GetInfoCategory(providerInfo, info);
        responseMessage.Result = reponse;
        return responseMessage;
    }

    private Vtc365Response CallApiRequest(string url, string fuction, Request365Request request)
    {
        try
        {
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            var client = new JsonServiceClient(url) { Timeout = TimeSpan.FromMinutes(10) };
            var result = client.Post<Vtc365Response>(fuction, request.ToJson());
            _logger.LogInformation(
                $"{request.partnerTransID} CallApiRequest_Vtc365Connector-Reponse {fuction} result: " +
                result.ToJson());
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError($"{request.partnerTransID} CallApiRequest_Vtc365Connector-Reponse {fuction} exception : " +
                             ex.Message);
            return new Vtc365Response()
            {
                status = 26
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
            _logger.LogError($"PrivateKey= {privateFile} Sign.Exception: {ex}");
            return string.Empty;
        }
    }

    private int[] ConvertArrayCode(string extraInfo)
    {
        var arrays = (from x in extraInfo.Split(';', '|', ',').ToList()
            select Convert.ToInt32(x)).ToArray();
        return arrays;
    }

    private DateTime GetExpireDate(string expireDate)
    {
        try
        {
            if (string.IsNullOrEmpty(expireDate))
                return new DateTime(DateTime.Now.AddYears(2).Year, 12, 31);

            var s = expireDate.Split('/', '-');
            return new DateTime(Convert.ToInt32(s[0]), Convert.ToInt32(s[1]), Convert.ToInt32(s[2]));
        }
        catch (Exception e)
        {
            _logger.LogError($"{expireDate} getExpireDate Error convert_date : " + e.Message);
            return new DateTime(DateTime.Now.AddYears(2).Year, 12, 31);
        }
    }

    private async Task<string> GetInfoCategory(ProviderInfoDto providerInfo, GetProviderProductInfo info)
    {
        try
        {
            string categoryID = "0";
            string function = "share/GetInfo/get-all-category";
            if (info.ProviderType == "0" || info.ProviderType == "")
                function = "share/GetInfo/get-all-category";
            else if (info.ProviderType == "1")
            {
                function = "share/GetInfo/get-category";
                categoryID = info.AccountNo;
            }
            else if (info.ProviderType == "2")
            {
                function = "share/GetInfo/get-product";
                categoryID = info.AccountNo;
            }

            var request = new Request365Request
            {
                categoryID = categoryID,
                productID = "",
                customerID = "",
                partnerCode = providerInfo.ApiUser,
                productAmount = "",
                partnerTransID = providerInfo.ApiUser + DateTime.Now.ToString("yyyyMMddHHmmss"),
                partnerTransDate = DateTime.Now.ToString("yyyyMMddHHmmss"),
                data = "",
            };

            _logger.LogInformation(
                $"TransCode= {request.partnerTransID}|Vtc365Connector.object ProductInfo_send: {request.ToJson()}");
            string inputData =
                $"{request.partnerCode}|{request.categoryID}|{request.productID}|{request.productAmount}|{request.customerID}|{request.partnerTransID}|{request.partnerTransDate}|{request.data}";
            var dataSign = Sign(inputData, "./" + providerInfo.PrivateKeyFile);
            request.dataSign = dataSign;

            var reporse = CallApiRequest(providerInfo.ApiUrl, function, request);
            if (reporse.status == 1)
            {
                var dataInfo = DecodeBaseString(reporse.dataInfo);
                return dataInfo;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"getInfoCategory Exception: {ex}");
        }

        return string.Empty;
    }

    private string DecodeBaseString(string data)
    {
        try
        {
            var base64EncodedBytes = System.Convert.FromBase64String(data);
            return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError($"DecodeBaseString Exception: {ex}");
            return string.Empty;
        }
    }
}