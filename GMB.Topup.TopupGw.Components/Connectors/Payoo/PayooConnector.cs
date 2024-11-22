using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using GMB.Topup.Shared;
using GMB.Topup.Shared.Dtos;
using GMB.Topup.Shared.Utils;
using GMB.Topup.TopupGw.Contacts.ApiRequests;
using GMB.Topup.TopupGw.Contacts.Dtos;
using GMB.Topup.TopupGw.Contacts.Enums;
using GMB.Topup.TopupGw.Domains.BusinessServices;
using MassTransit;
using Microsoft.Extensions.Logging;
using ServiceStack;

namespace GMB.Topup.TopupGw.Components.Connectors.Payoo
{
    public class PayooConnector : IGatewayConnector
    {
        private readonly ITopupGatewayService _topupGatewayService;
        private readonly IBusControl _bus;

        private readonly ILogger<PayooConnector> _logger;

        public PayooConnector(ITopupGatewayService topupGatewayService, ILogger<PayooConnector> logger,
            IBusControl bus)
        {
            _topupGatewayService = topupGatewayService;
            _logger = logger;
            _bus = bus;
        }

        public async Task<MessageResponseBase> TopupAsync(TopupRequestLogDto topupRequestLog,
            ProviderInfoDto providerInfo)
        {
            _logger.Log(LogLevel.Information, "PayooConnector request: " + topupRequestLog.ToJson());
            var responseMessage = new MessageResponseBase();
            try
            {
                if (!_topupGatewayService.ValidConnector(ProviderConst.PAYOO, providerInfo.ProviderCode))
                {
                    _logger.LogError(
                        $"{topupRequestLog.TransCode}-{topupRequestLog.TransRef}-{providerInfo.ProviderCode}-PayooConnector not valid");
                    return new MessageResponseBase
                    {
                        ResponseCode = ResponseCodeConst.Error,
                        ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
                    };
                }

                string topupType = "1";
                string dataPackage = "";
                var providerService =
                    providerInfo.ProviderServices.Find(p => p.ProductCode == topupRequestLog.ProductCode);
                var providerCode = string.Empty;
                var name = string.Empty;
                decimal salePrice = 0;
                if (providerService != null)
                {
                    var package = providerService.ServiceCode.Split('|');
                    //var salePriceDto = await _topupGatewayService.ProviderSalePriceGetAsync(providerInfo.ProviderCode, package[0], package[1], topupRequestLog.TransAmount);
                    providerCode = package[0];
                    topupType = package[1];
                    //salePrice = Convert.ToDecimal(salePriceDto != null ? salePriceDto.CardPrice : 0);
                    dataPackage = package.Length >= 3 ? package[2] : "";
                    name = providerService.ServiceName;
                }
                else
                {
                    _logger.LogWarning(
                        $"{topupRequestLog.TransCode} PayooConnector request with ProductCode [{topupRequestLog.ProductCode}] is null");

                    return new MessageResponseBase
                    {
                        ResponseCode = ResponseCodeConst.Error,
                        ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
                    };
                }

                var requestDate = DateTime.Now.ToString("yyyyMMddHHmmss");
                topupRequestLog.TransIndex = requestDate;
                var objData = setRequestTopupPayment(providerInfo.Username, providerInfo.ApiUser, providerCode,
                    topupRequestLog.TransAmount.ToString(), topupRequestLog.ReceiverInfo, topupType, salePrice,
                    topupRequestLog.TransCode, dataPackage, name, requestDate);

                objData.Signature = Sign(objData.RequestData, providerInfo.PrivateKeyFile);
                var body = await CallApi(providerInfo, objData, topupRequestLog.TransCode);
                var reponse = new PayooResponse();
                if (!string.IsNullOrEmpty(body))
                {
                    XmlDocument doc = new XmlDocument();
                    doc.LoadXml(body);
                    XmlNode xn = doc.SelectNodes("TopupPaymentBEResult").Item(0);
                    reponse.ReturnCode = Convert.ToInt32(xn["ReturnCode"].InnerText);
                    reponse.SystemTrace = xn["SystemTrace"] != null ? xn["SystemTrace"].InnerText : "";
                    reponse.OrderNo = xn["OrderNo"] != null ? xn["OrderNo"].InnerText : "";
                    reponse.SubReturnCode = xn["SubReturnCode"] != null ? xn["SubReturnCode"].InnerText : "";
                    reponse.DescriptionCode = xn["DescriptionCode"] != null ? xn["DescriptionCode"].InnerText : "";
                }
                else
                {
                    reponse.ReturnCode = 501102;
                }

                _logger.LogInformation(
                    $"Topup return: {topupRequestLog.TransCode}-{topupRequestLog.TransRef}-{body} => Convert: {reponse.ToJson()}");
                topupRequestLog.ModifiedDate = DateTime.Now;
                topupRequestLog.ResponseInfo = body;
                if (reponse.ReturnCode == 0)
                {
                    topupRequestLog.Status = TransRequestStatus.Success;
                    responseMessage.ResponseCode = ResponseCodeConst.Success;
                    responseMessage.ResponseMessage = "Giao dịch thành công";
                }
                else
                {
                    var arrayErrors = _topupGatewayService.ConvertArrayCode(providerInfo.ExtraInfo ?? string.Empty);
                    if (reponse != null && arrayErrors.Contains(reponse.ReturnCode))
                    {
                        string returnCode = reponse.ReturnCode.ToString();
                        if (reponse.ReturnCode == -9 && !string.IsNullOrEmpty(reponse.SubReturnCode))
                            returnCode = reponse.SubReturnCode;
                        var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.PAYOO,
                            returnCode, topupRequestLog.TransCode);
                        topupRequestLog.Status = TransRequestStatus.Fail;
                        responseMessage.ResponseCode =
                            reResult != null ? reResult.ResponseCode : ResponseCodeConst.Error;
                        responseMessage.ResponseMessage = reResult != null ? reResult.ResponseName :
                            !string.IsNullOrEmpty(reponse.DescriptionCode) ? reponse.DescriptionCode :
                            "Giao dịch không thành công.";
                    }
                    else
                    {
                        // var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.PAYOO,
                        //     reponse.ReturnCode.ToString(), topupRequestLog.TransCode);
                        topupRequestLog.Status = TransRequestStatus.Timeout;
                        responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                        responseMessage.ResponseMessage =
                            "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                    }
                }


                await _topupGatewayService.TopupRequestLogUpdateAsync(topupRequestLog);
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

        public async Task<MessageResponseBase> TransactionCheckAsync(string providerCode, string transCodeToCheck,
            string transCode, string serviceCode = null, ProviderInfoDto providerInfo = null)
        {
            _logger.LogInformation($"{transCodeToCheck} PayooConnector check request: " + transCodeToCheck + "|" +
                                   transCode);
            var responseMessage = new MessageResponseBase();
            try
            {
                if (providerInfo == null)
                    providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(providerCode);

                if (providerInfo == null ||
                    !_topupGatewayService.ValidConnector(ProviderConst.PAYOO, providerInfo.ProviderCode))
                {
                    _logger.LogError($"{transCode}-{providerCode}-PayooConnector not valid");
                    return new MessageResponseBase
                    {
                        ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult,
                        ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ"
                    };
                }

                string transDate = "";
                if (serviceCode.StartsWith("PIN"))
                {
                    var cardLog = await _topupGatewayService.CardRequestLogAsync(transCodeToCheck);
                    transDate = cardLog != null && !string.IsNullOrEmpty(cardLog.TransIndex)
                        ? cardLog.TransIndex
                        : DateTime.Now.ToString("yyyyMMddHHmmss");
                }
                else
                {
                    var tranLog =
                        await _topupGatewayService.GetTopupRequestLogAsync(transCodeToCheck, ProviderConst.PAYOO);
                    transDate = tranLog != null && !string.IsNullOrEmpty(tranLog.TransIndex)
                        ? tranLog.TransIndex
                        : DateTime.Now.ToString("yyyyMMddHHmmss");
                }

                var objData = setRequestTransStatus(providerInfo.ApiUser, transCodeToCheck, transDate);
                objData.Signature = Sign(objData.RequestData, providerInfo.PrivateKeyFile);
                var body = await CallApi(providerInfo, objData, transCode);
                var reponse = new PayooResponse();
                if (!string.IsNullOrEmpty(body))
                {
                    XmlDocument doc = new XmlDocument();
                    doc.LoadXml(body);
                    XmlNode xn = doc.SelectNodes("GetTransactionStatusBEResult").Item(0);
                    reponse.ReturnCode = Convert.ToInt32(xn["ReturnCode"].InnerText);
                    reponse.Status = xn["Status"] != null ? xn["Status"].InnerText : "";
                    reponse.OrderNo = xn["OrderNo"] != null ? xn["OrderNo"].InnerText : "";
                }
                else
                {
                    reponse.ReturnCode = 501102;
                }

                _logger.LogInformation(
                    $"Card return: {transCodeToCheck}-{transCode}-{body} => Convert: {reponse.ToJson()}");
                if (reponse.ReturnCode == 0)
                {
                    if (reponse.Status == "1")
                    {
                        responseMessage.ResponseCode = ResponseCodeConst.Success;
                        responseMessage.ResponseMessage = "Giao dịch thành công";
                        if (serviceCode.StartsWith("PIN"))
                        {
                            try
                            {
                                var objDataget =
                                    setRequestGetPinCode(providerInfo.ApiUser, transCodeToCheck, transDate);
                                objDataget.Signature = Sign(objDataget.RequestData, providerInfo.PrivateKeyFile);
                                var bodyGet = await CallApi(providerInfo, objDataget, transCode);
                                if (!string.IsNullOrEmpty(bodyGet))
                                {
                                    XmlDocument docCard = new XmlDocument();
                                    docCard.LoadXml(bodyGet);
                                    XmlNode xnCard = docCard.SelectNodes("CodeGetCardListBEResult").Item(0);
                                    var returnCodeCard = Convert.ToInt32(xnCard["ReturnCode"].InnerText);
                                    var cards = new List<PayCodeInfo>();
                                    if (returnCodeCard == 0)
                                    {
                                        var payCodes = xnCard.SelectNodes("Paycodes").Item(0);
                                        foreach (XmlNode card in payCodes)
                                        {
                                            cards.Add(new PayCodeInfo()
                                            {
                                                CardCode = (card["CardId"] != null ? card["CardId"].InnerText : "")
                                                    .Replace("-", "").EncryptTripDes(),
                                                SeriNumber = card["SeriNumber"] != null
                                                    ? card["SeriNumber"].InnerText
                                                    : "",
                                                Expired = card["Expired"] != null ? card["Expired"].InnerText : "",
                                                TypeCard = card["TypeCard"] != null ? card["TypeCard"].InnerText : "",
                                            });
                                        }

                                        responseMessage.Payload = cards;
                                    }
                                }
                            }
                            catch (Exception exx)
                            {
                                _logger.LogError($"{transCodeToCheck} PayooConnector GetCard_Exception: {exx}");
                            }
                        }
                    }
                    else if (reponse.Status == "2" || reponse.Status == "5")
                    {
                        responseMessage.ResponseCode = ResponseCodeConst.Error;
                        responseMessage.ResponseMessage = "Giao dịch không thành công";
                    }
                    else
                    {
                        responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                        responseMessage.ResponseMessage =
                            "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                    }
                }
                else if (new[] { -2 }.Contains(reponse.ReturnCode))
                {
                    responseMessage.ResponseCode = ResponseCodeConst.Error;
                    responseMessage.ResponseMessage = "Giao dịch không thành công";
                }
                else
                {
                    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                    responseMessage.ResponseMessage =
                        "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                }

                responseMessage.ProviderResponseCode = reponse.ReturnCode.ToString();
                responseMessage.ProviderResponseMessage = "";
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

        public async Task<NewMessageResponseBase<InvoiceResultDto>> QueryAsync(PayBillRequestLogDto payBillRequestLog)
        {
            var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(payBillRequestLog.ProviderCode);
            if (payBillRequestLog.ServiceCode.StartsWith("PROVIDER"))
            {
                var reponse = await getCardProviderListAsync(providerInfo);
                return new NewMessageResponseBase<InvoiceResultDto>()
                {
                    ResponseStatus = new ResponseStatusApi()
                    {
                        Message = reponse,
                        ErrorCode = ResponseCodeConst.Success,
                    }
                };
            }
            else if (payBillRequestLog.ServiceCode.StartsWith("SALEPRICE"))
            {
                var lst = await getTopupValue(providerInfo, payBillRequestLog.CategoryCode, payBillRequestLog.Vendor,
                    payBillRequestLog.ReceiverInfo);
                return new NewMessageResponseBase<InvoiceResultDto>()
                {
                    ResponseStatus = new ResponseStatusApi()
                    {
                        Message = lst.ToJson(),
                        ErrorCode = ResponseCodeConst.Success,
                    }
                };
            }
            else return null;
        }

        public async Task<MessageResponseBase> CardGetByBatchAsync(CardRequestLogDto cardRequestLog)
        {
            _logger.LogInformation(
                $"{cardRequestLog.TransCode} PayooConnector card request: " + cardRequestLog.ToJson());
            var responseMessage = new MessageResponseBase();
            var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(cardRequestLog.ProviderCode);

            if (providerInfo == null)
                return responseMessage;

            if (!_topupGatewayService.ValidConnector(ProviderConst.PAYOO, providerInfo.ProviderCode))
            {
                _logger.LogError(
                    $"{cardRequestLog.TransCode}-{cardRequestLog.TransRef}-{providerInfo.ProviderCode}-PayooConnector not valid");
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error,
                    ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
                };
            }

            var str = cardRequestLog.ProductCode.Split('_');
            var keyCode = $"{str[0]}_{str[1]}";
            var providerService = providerInfo.ProviderServices.Find(p => p.ProductCode == keyCode);
            var providerId = string.Empty;
            if (providerService != null)
                providerId = providerService.ServiceCode.Split('|')[0];
            else
            {
                _logger.LogWarning(
                    $"{cardRequestLog.TransCode} PayooConnector request with ProductCode [{cardRequestLog.ProductCode}] is null");

                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error,
                    ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
                };
            }

            var salePrice = await getSalePrice(providerInfo, cardRequestLog, providerId);
            var requestDate = DateTime.Now.ToString("yyyyMMddHHmmss");
            cardRequestLog.TransIndex = requestDate;
            var objData = setRequestPinCodePayment(providerInfo.Username, providerInfo.ApiUser, providerId,
                cardRequestLog.TransAmount.ToString(), cardRequestLog.Quantity, salePrice, cardRequestLog.TransCode,
                requestDate);

            objData.Signature = Sign(objData.RequestData, providerInfo.PrivateKeyFile);
            var body = await CallApi(providerInfo, objData, cardRequestLog.TransCode);
            var reponse = new PayooResponse();
            if (!string.IsNullOrEmpty(body))
            {
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(body);
                XmlNode xn = doc.SelectNodes("CodePaymentBEResult").Item(0);
                reponse.ReturnCode = Convert.ToInt32(xn["ReturnCode"].InnerText);
                reponse.SystemTrace = xn["SystemTrace"] != null ? xn["SystemTrace"].InnerText : "";
                reponse.OrderNo = xn["OrderNo"] != null ? xn["OrderNo"].InnerText : "";
                var cards = new List<PayCodeInfo>();
                if (reponse.ReturnCode == 0)
                {
                    var payCodes = xn.SelectNodes("PayCodes").Item(0);
                    foreach (XmlNode card in payCodes)
                    {
                        cards.Add(new PayCodeInfo()
                        {
                            CardCode = (card["CardId"] != null ? card["CardId"].InnerText : "").Replace("-", ""),
                            SeriNumber = card["SeriNumber"] != null ? card["SeriNumber"].InnerText : "",
                            Expired = card["Expired"] != null ? card["Expired"].InnerText : "",
                            TypeCard = card["TypeCard"] != null ? card["TypeCard"].InnerText : "",
                        });
                    }

                    reponse.PayCodes = cards;
                }
            }
            else
            {
                reponse.ReturnCode = 501102;
            }

            _logger.LogInformation(
                $"Card return: {cardRequestLog.TransCode}-{cardRequestLog.TransRef}-{body} => Convert: {reponse.ToJson()}");
            cardRequestLog.ModifiedDate = DateTime.Now;
            cardRequestLog.ResponseInfo = body;
            if (reponse.ReturnCode == 0)
            {
                cardRequestLog.Status = TransRequestStatus.Success;
                responseMessage.ResponseCode = ResponseCodeConst.Success;
                responseMessage.ResponseMessage = "Giao dịch thành công";
                var cardList = new List<CardRequestResponseDto>();
                foreach (var card in reponse.PayCodes)
                    cardList.Add(new CardRequestResponseDto
                    {
                        CardType = cardRequestLog.Vendor,
                        CardValue = cardRequestLog.TransAmount.ToString(),
                        CardCode = card.CardCode,
                        Serial = card.SeriNumber,
                        ExpireDate = card.Expired,
                        ExpiredDate = getExpireDate(card.Expired),
                    });
                responseMessage.Payload = cardList;
            }
            else
            {
                var arrayErrors = _topupGatewayService.ConvertArrayCode(providerInfo.ExtraInfo ?? string.Empty);
                if (reponse != null && arrayErrors.Contains(reponse.ReturnCode))
                {
                    string returnCode = reponse.ReturnCode.ToString();
                    if (reponse.ReturnCode == -9 && !string.IsNullOrEmpty(reponse.SubReturnCode))
                        returnCode = reponse.SubReturnCode;
                    var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.PAYOO,
                        returnCode, cardRequestLog.TransCode);
                    cardRequestLog.Status = TransRequestStatus.Fail;
                    responseMessage.ResponseCode = reResult != null ? reResult.ResponseCode : ResponseCodeConst.Error;
                    responseMessage.ResponseMessage = reResult != null ? reResult.ResponseName :
                        !string.IsNullOrEmpty(reponse.DescriptionCode) ? reponse.DescriptionCode :
                        "Giao dịch không thành công.";
                }
                else
                {
                    // var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.PAYOO,
                    //     reponse.ReturnCode.ToString(), cardRequestLog.TransCode);
                    cardRequestLog.Status = TransRequestStatus.Timeout;
                    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                    responseMessage.ResponseMessage =
                        "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                }
            }

            await _topupGatewayService.CardRequestLogUpdateAsync(cardRequestLog);
            return responseMessage;
        }

        public async Task<MessageResponseBase> CheckBalanceAsync(string providerCode, string transCode)
        {
            _logger.LogInformation(
                $"CheckBalanceAsync request: ======== NOT IMPLEMENTED ====== {providerCode}|{transCode}");
            throw new NotImplementedException();
        }

        public async Task<MessageResponseBase> DepositAsync(DepositRequestDto request)
        {
            _logger.LogInformation("DepositRequestDto request: ======== NOT IMPLEMENTED ====== /n" +
                                   request.ToJson());
            throw new NotImplementedException();
        }

        public Task<MessageResponseBase> PayBillAsync(PayBillRequestLogDto payBillRequestLog)
        {
            _logger.LogInformation("Get paybill request: ======== NOT IMPLEMENTED ====== /n" +
                                   payBillRequestLog.ToJson());
            throw new NotImplementedException();
        }

        private async Task<decimal> getSalePrice(ProviderInfoDto providerInfo, CardRequestLogDto cardRequestLog,
            string providerId)
        {
            decimal price = 0;
            try
            {
                var objSalePrice = setRequestSalePrice(providerInfo.Username, providerInfo.ApiUser,
                    cardRequestLog.TransAmount.ToString(), providerId, cardRequestLog.Quantity);
                objSalePrice.Signature = Sign(objSalePrice.RequestData, providerInfo.PrivateKeyFile);
                var body = await CallApi(providerInfo, objSalePrice, string.Empty);

                if (!string.IsNullOrEmpty(body))
                {
                    XmlDocument doc = new XmlDocument();
                    doc.LoadXml(body);
                    XmlNode xn = doc.SelectNodes("PaycodeInquiryBEResult").Item(0);
                    price = Convert.ToDecimal(Convert
                        .ToDouble(xn["PurchasingPrice"] != null ? xn["PurchasingPrice"].InnerText : "0").ToString());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    $"{providerId}|{cardRequestLog.TransCode}|{cardRequestLog.Quantity} getSalePrice Error: " + ex);
            }

            return price;
        }

        private async Task<List<ProviderSalePriceDto>> getTopupValue(ProviderInfoDto providerInfo, string topupType,
            string providerCode, string receiverInfo)
        {
            var objTopupValue = setRequestTopupValue(providerInfo.Username, providerInfo.ApiUser, receiverInfo,
                topupType, providerCode);
            objTopupValue.Signature = Sign(objTopupValue.RequestData, providerInfo.PrivateKeyFile);
            var body = await CallApi(providerInfo, objTopupValue, string.Empty);
            var dtos = new List<ProviderSalePriceDto>();
            if (!string.IsNullOrEmpty(body))
            {
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(body);

                XmlNode xnReponse = doc.SelectNodes("GetTopupValueListResult").Item(0);
                if (Convert.ToInt32(xnReponse["ReturnCode"].InnerText) == 0)
                {
                    var topupLists = xnReponse.SelectNodes("TopupValueList").Item(0);
                    foreach (XmlNode card in topupLists)
                    {
                        var dto = new ProviderSalePriceDto()
                        {
                            ProviderCode = providerInfo.ProviderCode,
                            ProviderType = providerCode,
                            CardValue =
                                Convert.ToDecimal(card["CardValue"] != null ? card["CardValue"].InnerText : "0"),
                            CardPrice = Convert.ToDecimal(card["PurchasingPrice"] != null
                                ? card["PurchasingPrice"].InnerText
                                : "0"),
                            CardValueName = card["CardValueName"] != null ? card["CardValueName"].InnerText : "",
                            TopupType = topupType,
                            DataPackageName = card["DataPackage"] != null ? card["DataPackage"].InnerText : ""
                        };

                        dtos.Add(dto);
                    }
                }
            }

            return dtos;
        }

        private async Task<string> getCardProviderListAsync(ProviderInfoDto providerInfo)
        {
            var objData = setRequestProviderValue(providerInfo.Username, providerInfo.ApiUser);
            objData.Signature = Sign(objData.RequestData, providerInfo.PrivateKeyFile);
            var body = await CallApi(providerInfo, objData, string.Empty);
            return body;
        }

        #region Private

        private async Task<string> CallApi(ProviderInfoDto providerInfo, PayooService.UniGWSRequest request,
            string transCode)
        {
            try
            {
                using (var data = new PayooService.UniGWSSoapClient(
                           PayooService.UniGWSSoapClient.EndpointConfiguration.UniGWSSoap, providerInfo.ApiUrl))
                {
                    var reponseDto = await data.Execute6Async(request);
                    _logger.LogInformation($"{transCode}|{request.Operation} CallApi Reponse : true");
                    var body = reponseDto.Body.Execute6Result.ResponseData;
                    _logger.LogInformation(
                        $"{transCode}|{request.Operation} CallApi_Data_Reponse Body.Length= {body.Length}");
                    return body;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"{transCode}|{request.Operation} CallApi Error: " + ex);
                return string.Empty;
            }
        }

        private string Sign(string dataToSign, string privateFile)
        {
            var privateKeyText = File.ReadAllText("files/" + privateFile);
            var privateKeyBlocks = privateKeyText.Split("-", StringSplitOptions.RemoveEmptyEntries);
            var key = privateKeyBlocks[1].Replace("\r\n", "");
            var privateKeyBytes = Convert.FromBase64String(key);
            var dataByte = Encoding.UTF8.GetBytes(dataToSign);
            using var rsa = RSA.Create();
            if (privateKeyBlocks[0] == "BEGIN PRIVATE KEY")
                rsa.ImportPkcs8PrivateKey(privateKeyBytes, out _);
            else if (privateKeyBlocks[0] == "BEGIN RSA PRIVATE KEY") rsa.ImportRSAPrivateKey(privateKeyBytes, out _);
            var sig = rsa.SignData(dataByte, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var signature = Convert.ToBase64String(sig);
            return signature;
        }

        private PayooService.UniGWSRequest setRequestSalePrice(string userId, string agentId, string cardValue,
            string providerId, int quantity)
        {
            StringBuilder sp = new StringBuilder();
            sp.Append($"<PaycodeInquiryBERequest><UserId>{userId}</UserId>");
            sp.Append($"<AgentId>{agentId}</AgentId>");
            sp.Append($"<CardValue>{cardValue}</CardValue>");
            sp.Append($"<ProviderId>{providerId}</ProviderId>");
            sp.Append($"<Quantity>{quantity}</Quantity>");
            sp.Append($"</PaycodeInquiryBERequest>");
            string str = sp.ToString();
            var objParamater = new PayooService.UniGWSRequest()
            {
                RequestTime = DateTime.Now.ToString("dd/MM/yyyy HHmmss"),
                Operation = "MMS_PaycodeInquiryBE",
                RequestData = str,
                Signature = "",
            };
            return objParamater;
        }

        private PayooService.UniGWSRequest setRequestPinCodePayment(string userId, string agentId, string providerId,
            string cardValue, int quantity, decimal totalPrice, string transCode, string date)
        {
            StringBuilder sp = new StringBuilder();
            sp.Append($"<CodePaymentBERequest><UserId>{userId}</UserId>");
            sp.Append($"<AgentId>{agentId}</AgentId>");
            sp.Append($"<CardValue>{cardValue}</CardValue>");
            sp.Append($"<SystemTrace>{transCode}</SystemTrace>");
            sp.Append($"<ProviderId>{providerId}</ProviderId>");
            sp.Append($"<Quantity>{quantity}</Quantity>");
            sp.Append($"<TotalPurchasingAmount>{totalPrice}</TotalPurchasingAmount>");
            sp.Append($"<TransactionTime>{date}</TransactionTime>");
            sp.Append($"<ChannelPerform>WEB</ChannelPerform>");
            sp.Append($"</CodePaymentBERequest>");
            string str = sp.ToString();
            var objData = new PayooService.UniGWSRequest()
            {
                RequestTime = DateTime.Now.ToString("dd/MM/yyyy HHmmss"),
                Operation = "MMS_CodePaymentBE",
                RequestData = str,
                Signature = "",
            };
            return objData;
        }

        private PayooService.UniGWSRequest setRequestTopupPayment(string userId, string agentId, string providerCode,
            string cardValue, string phoneNo, string topupType, decimal totalPrice, string transCode,
            string dataPackage, string name, string date)
        {
            StringBuilder sp = new StringBuilder();
            sp.Append($"<TopupPaymentBERequest><UserId>{userId}</UserId>");
            sp.Append($"<AgentId>{agentId}</AgentId>");
            sp.Append($"<PhoneNo>{phoneNo}</PhoneNo>");
            sp.Append($"<ProviderCode>{providerCode}</ProviderCode>");
            sp.Append($"<TopupType>{topupType}</TopupType>");
            sp.Append($"<CardValue>{cardValue}</CardValue>");
            // sp.Append($"<TotalPurchasingAmount>{totalPrice}</TotalPurchasingAmount>");
            sp.Append($"<SystemTrace>{transCode}</SystemTrace>");
            sp.Append($"<TransactionTime>{date}</TransactionTime>");
            sp.Append($"<DataPackage>{dataPackage}</DataPackage>");
            sp.Append($"<CardValueName>{name}</CardValueName>");
            sp.Append($"<ChannelPerform>WEB</ChannelPerform>");
            sp.Append($"</TopupPaymentBERequest>");
            string str = sp.ToString();
            var objData = new PayooService.UniGWSRequest()
            {
                RequestTime = DateTime.Now.ToString("dd/MM/yyyy HHmmss"),
                Operation = "MMS_TopupPaymentBE",
                RequestData = str,
                Signature = "",
            };
            return objData;
        }

        private PayooService.UniGWSRequest setRequestGetPinCode(string agentId, string transCode, string transDate)
        {
            StringBuilder sp = new StringBuilder();
            sp.Append($"<CodeGetCardListBERequest>");
            sp.Append($"<AgentId>{agentId}</AgentId>");
            sp.Append($"<SystemTrace>{transCode}</SystemTrace>");
            sp.Append($"<TransactionTime>{transDate}</TransactionTime>");
            sp.Append($"<RequestTime>{DateTime.Now.ToString("yyyyMMddHHmmss")}</RequestTime>");
            sp.Append($"</CodeGetCardListBERequest>");

            string str = sp.ToString();
            var objData = new PayooService.UniGWSRequest()
            {
                RequestTime = DateTime.Now.ToString("dd/MM/yyyy HHmmss"),
                Operation = "MMS_CodeGetCardListBE",
                RequestData = str,
                Signature = "",
            };
            return objData;
        }

        private PayooService.UniGWSRequest setRequestTransStatus(string agentId, string transCode, string tranDate)
        {
            StringBuilder sp = new StringBuilder();
            sp.Append($"<GetTransactionStatusBERequest>");
            sp.Append($"<SystemTrace>{transCode}</SystemTrace>");
            sp.Append($"<RequestTime>{DateTime.Now.ToString("yyyyMMddHHmmss")}</RequestTime>");
            sp.Append($"<TransactionTime>{tranDate}</TransactionTime>");
            sp.Append($"<AgentId>{agentId}</AgentId>");
            sp.Append($"</GetTransactionStatusBERequest>");
            string str = sp.ToString();
            var objData = new PayooService.UniGWSRequest()
            {
                RequestTime = DateTime.Now.ToString("dd/MM/yyyy HHmmss"),
                Operation = "MMS_GetTransactionStatusBE",
                RequestData = str,
                Signature = "",
            };
            return objData;
        }

        private PayooService.UniGWSRequest setRequestTopupValue(string userId, string agentId, string phoneNo,
            string topupType, string providerCode)
        {
            StringBuilder sp = new StringBuilder();
            sp.Append($"<GetTopupValueListRequest><UserId>{userId}</UserId>" +
                      $"<AgentId>{agentId}</AgentId>" +
                      $"<PhoneNo>{phoneNo}</PhoneNo>" +
                      $"<TopupType>{topupType}</TopupType>" +
                      $"<ProviderCode>{providerCode}</ProviderCode>" +
                      $"</GetTopupValueListRequest>");
            var objParamater = new PayooService.UniGWSRequest()
            {
                RequestTime = DateTime.Now.ToString("dd/MM/yyyy HHmmss"),
                Operation = "MMS_GetTopupValueList",
                RequestData = sp.ToString(),
                Signature = "",
            };
            return objParamater;
        }

        private PayooService.UniGWSRequest setRequestProviderValue(string userId, string agentId)
        {
            StringBuilder sp = new StringBuilder();
            sp.Append(
                $"<GetCardProviderListRequest><UserId>{userId}</UserId><AgentId>{agentId}</AgentId></GetCardProviderListRequest>");
            var objData = new PayooService.UniGWSRequest()
            {
                RequestTime = DateTime.Now.ToString("dd/MM/yyyy HHmmss"),
                Operation = "MMS_GetCardProviderList",
                RequestData = sp.ToString(),
                Signature = "",
            };
            return objData;
        }

        private DateTime getExpireDate(string expireDate)
        {
            try
            {
                if (string.IsNullOrEmpty(expireDate))
                    return new DateTime(DateTime.Now.AddYears(2).Year, 12, 31);

                var s = expireDate.Split('-', '/');
                return new DateTime(Convert.ToInt32(s[0]), Convert.ToInt32(s[1]), Convert.ToInt32(s[2]));
            }
            catch (Exception e)
            {
                _logger.LogError($"{expireDate} getExpireDate Error convert_date : " + e.Message);
                return new DateTime(DateTime.Now.AddYears(2).Year, 12, 31);
            }
        }

        public async Task<ResponseMessageApi<object>> GetProductInfo(GetProviderProductInfo info)
        {
            var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(info.ProviderCode);
            var rs = await getTopupValue(providerInfo, info.TopupType, info.ProviderType, info.AccountNo);
            return new ResponseMessageApi<object>
            {
                Result = rs,
            };
        }

        public Task<MessageResponseBase> CheckPrePostPaid(string msisdn, string transCode, string providerCode)
        {
            throw new NotImplementedException();
        }
    }

    #endregion
}