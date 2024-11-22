using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Topup.Shared;
using Topup.Shared.Dtos;
using Topup.Shared.Utils;
using Topup.TopupGw.Contacts.ApiRequests;
using Topup.TopupGw.Contacts.Dtos;
using Topup.TopupGw.Contacts.Enums;
using Topup.TopupGw.Domains.BusinessServices;
using Microsoft.Extensions.Logging;
using RestSharp;
using ServiceStack;


namespace Topup.TopupGw.Components.Connectors.Finviet
{
    public class FinVietConnector : GatewayConnectorBase
    {
        private readonly ILogger<FinVietConnector> _logger;

        public FinVietConnector(ITopupGatewayService topupGatewayService,
            ILogger<FinVietConnector> logger)
            : base(topupGatewayService)
        {
            _logger = logger;
        }

        public override async Task<MessageResponseBase> TopupAsync(TopupRequestLogDto topupRequestLog,
            ProviderInfoDto providerInfo)
        {
            _logger.LogInformation("FinVietConnector Topup request: " + topupRequestLog.ToJson());
            var responseMessage = new MessageResponseBase();
            try
            {
                if (!TopupGatewayService.ValidConnector(ProviderConst.FINVIET, providerInfo.ProviderCode))
                {
                    _logger.LogError(
                        $"{topupRequestLog.TransCode}-{topupRequestLog.TransRef}-{providerInfo.ProviderCode} - FinVietConnector ProviderConnector not valid");
                    return new MessageResponseBase
                    {
                        ResponseCode = ResponseCodeConst.Error,
                        ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
                    };
                }

                string telco = "";
                string function = $"/api/topup";
                if (topupRequestLog.ServiceCode == "TOPUP")
                {
                    telco = topupRequestLog.Vendor switch
                    {
                        "VTE" => "vte",
                        "VNA" => "vnp",
                        "VMS" => "mbf",
                        "VNM" => "vnm",
                        "GMOBILE" => "gmb",
                        _ => ""
                    };
                }
                else
                {
                    var sk = topupRequestLog.ProductCode.Split('_');
                    var key = string.Join("_", sk.Skip(0).Take(2));
                    var product = providerInfo.ProviderServices.Find(p => p.ProductCode == key);
                    if (product == null)
                    {
                        _logger.LogError($"{topupRequestLog.TransCode}-{topupRequestLog.TransRef}-{topupRequestLog.ProviderCode} - FinVietConnector ProviderConnector not config productCode= {topupRequestLog.ProductCode}");
                        return new MessageResponseBase
                        {
                            ResponseCode = ResponseCodeConst.Error,
                            ResponseMessage = "Giao dịch lỗi. Chưa cấu hình sản phẩm"
                        };
                    }
                    telco = product.ServiceCode;
                    if (product.ServiceName == "POSTPAID")
                        function = "/api/postpaid";
                }

                var request = new RequestDto
                {
                    username = providerInfo.Username,
                    reqid = topupRequestLog.TransCode,
                    phone = topupRequestLog.ReceiverInfo,
                    amount = topupRequestLog.TransAmount,
                    reqtime = DateTime.Now.ToString("yyyyMMddHHmmss"),
                    telco = telco
                };

                request.password = EncryptTripleDES(providerInfo.Password, providerInfo.ApiPassword);

                var checksum = string.Join("", request.username, request.password,
                     request.reqid, request.reqtime, request.phone, request.amount, providerInfo.PublicKey);

                request.checksum = CreateHash(checksum);
                responseMessage.TransCodeProvider = topupRequestLog.TransCode;

                var reponse = await CallApi<RequestDto>(providerInfo, function, request.reqid, request);
                var result = reponse.ConvertTo<ResponseDto>();
                responseMessage.ProviderResponseCode = result.code;
                responseMessage.ProviderResponseMessage = result.message;
                responseMessage.ProviderResponseTransCode = result.transid;
                if (result.code == "0")
                {
                    topupRequestLog.Status = TransRequestStatus.Success;
                    responseMessage.ResponseCode = ResponseCodeConst.Success;
                    responseMessage.ResponseMessage = "Giao dịch thành công";
                }
                else
                {
                    topupRequestLog.ModifiedDate = DateTime.Now;
                    topupRequestLog.ResponseInfo = result.ToJson();
                    _logger.LogInformation($"FinVietConnector Topup fail return: {topupRequestLog.ProviderCode} - {topupRequestLog.TransCode} - {topupRequestLog.TransRef} - {result.ToJson()}");
                    var reResult = await TopupGatewayService.GetResponseMassageCacheAsync(ProviderConst.FINVIET, result.code, providerInfo.ProviderCode);
                    topupRequestLog.Status = TransRequestStatus.Timeout;
                    responseMessage.ResponseCode = reResult != null ? reResult.ResponseCode : ResponseCodeConst.ResponseCode_WaitForResult;
                    responseMessage.ResponseMessage = reResult != null ? reResult.ResponseName : "Giao dịch chưa có kết quả.";
                    topupRequestLog.ModifiedDate = DateTime.Now;
                }

                _logger.Log(LogLevel.Information, $"{topupRequestLog.TransCode} ==> {responseMessage.ToJson()}");
            }
            catch (Exception e)
            {
                _logger.LogError($"{topupRequestLog.TransCode} -{topupRequestLog.TransRef} FinVietConnector Topup error {e}");
                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
            }

            return responseMessage;
        }

        public override async Task<MessageResponseBase> TransactionCheckAsync(string providerCode, string transCodeToCheck,
            string transCode, string serviceCode = null, ProviderInfoDto providerInfo = null)
        {
            try
            {
                _logger.LogInformation($"FinVietConnector check request: {transCodeToCheck}-{transCode}-{providerCode}-{serviceCode}");
                var responseMessage = new MessageResponseBase();
                if (providerInfo == null)
                    providerInfo = await TopupGatewayService.ProviderInfoCacheGetAsync(providerCode);

                if (providerInfo == null || !TopupGatewayService.ValidConnector(ProviderConst.FINVIET, providerInfo.ProviderCode))
                {
                    _logger.LogError($"{transCode}-{providerCode}-{providerCode} - FinVietConnector ProviderConnector not valid");
                    return new MessageResponseBase
                    {
                        ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult,
                        ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ"
                    };
                }
                var request = new CheckTransRequest
                {
                    reqid = transCodeToCheck,
                    username = providerInfo.Username,
                };

                string checksum = string.Join("", request.username, request.reqid, providerInfo.PublicKey);
                request.checksum = CreateHash(checksum);

                _logger.LogInformation($"providerCode= {providerCode} - TransCode= {transCodeToCheck} FinVietConnector check send: " + request.ToJson());
                var reponse = await CallApi<CheckTransRequest>(providerInfo, $"/api/checktrans", request.reqid, request);
                var result = reponse.ConvertTo<ResponseTransDto>();
                if (result.code == "0")
                {
                    _logger.LogInformation($"providerCode= {providerCode} - transCodeToCheck= {transCodeToCheck} FinVietConnector check return: {result.ToJson()}");
                    if (result.trans.code == "0")
                    {
                        responseMessage.ResponseCode = ResponseCodeConst.Success;
                        responseMessage.ResponseMessage = "Giao dịch thành công";
                        responseMessage.ProviderResponseCode = result.trans.code;
                        responseMessage.ProviderResponseMessage = result.trans.message;
                        responseMessage.ProviderResponseTransCode = result.trans.transid;
                        if (result.trans.cards != null && result.trans.cards.Count > 0)
                        {
                            var cardList = result.trans.cards.Select(card => new CardRequestResponseDto
                            {
                                CardCode = card.password,
                                Serial = card.serial,
                                CardValue = card.amount,
                                CardType = string.Empty,
                                ExpiredDate = UnixTimeStampToDateTime(card.expireddate),
                                ExpireDate = UnixTimeStampToDateTime(card.expireddate).ToString("yyyy-MM-dd HH:mm:ss")
                            }).ToList();
                            cardList = GenDecryptListCode(cardList, providerInfo.ApiPassword, isTripDes: true);
                            responseMessage.Payload = cardList;
                        }
                    }
                    else
                    {
                        var arrayErrors = TopupGatewayService.ConvertArrayCode(providerInfo.ExtraInfo ?? string.Empty);
                        if (arrayErrors.Contains(Convert.ToInt32(result.trans.code)))
                        {
                            responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_ErrorProvider;
                            responseMessage.ResponseMessage = "Giao dịch không thành công";
                        }
                        else
                        {
                            _logger.LogInformation($"transCodeToCheck= {transCodeToCheck} - FinVietConnector");
                            responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                            responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                        }
                    }
                }
                else
                {
                    _logger.LogInformation($"transCodeToCheck= {transCodeToCheck} - FinVietConnector");
                    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                    responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                }
                return responseMessage;
            }
            catch (Exception ex)
            {
                _logger.LogError($"transCodeToCheck= {transCodeToCheck} FinVietConnector Exception: {ex}");
                return new MessageResponseBase
                {
                    ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ",
                    ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult
                };
            }
        }

        public override async Task<NewMessageResponseBase<InvoiceResultDto>> QueryAsync(PayBillRequestLogDto payBillRequestLog)
        {
            _logger.LogInformation($"{payBillRequestLog.TransCode} FinVietConnector query request: " +
                             payBillRequestLog.ToJson());
            var responseMessage = new NewMessageResponseBase<InvoiceResultDto>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error, "Truy vấn thông tin không thành công")
            };

            var providerInfo = await TopupGatewayService.ProviderInfoCacheGetAsync(payBillRequestLog.ProviderCode);

            if (providerInfo == null)
                return responseMessage;

            if (!TopupGatewayService.ValidConnector(ProviderConst.FINVIET, providerInfo.ProviderCode))
            {
                _logger.LogError($"{payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{providerInfo.ProviderCode} - FinVietConnector ProviderConnector not valid");
                responseMessage.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error, "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ");
                return responseMessage;
            }

            var providerService = providerInfo.ProviderServices.Find(p => p.ProductCode == payBillRequestLog.ProductCode);
            if (providerService == null)
            {
                _logger.LogError($"{payBillRequestLog.TransCode} FinVietConnector ProviderService with ProductCode [{payBillRequestLog.ProductCode}] is null");
                return new NewMessageResponseBase<InvoiceResultDto>()
                {
                    ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error, "Giao dịch lỗi. Thông tin sản phẩm nhà cung cấp chưa được cấu hình")
                };
            }

            RequestBillDto request = null;
            string function = string.Empty;
            if (providerService.ServiceName == "POSTPAID")
            {
                function = $"/api/postpaid";
                request = new RequestBillDto
                {
                    username = providerInfo.Username,
                    reqid = payBillRequestLog.TransCode,
                    reqtime = DateTime.Now.ToString("yyyyMMddHHmmss"),
                    phone = payBillRequestLog.ReceiverInfo,
                    password = EncryptTripleDES(providerInfo.Password, providerInfo.ApiPassword),
                    amount = 1001
                };

                var checksum = string.Join("", request.username, request.password,
                    request.reqid, request.reqtime, request.phone, request.amount, providerInfo.PublicKey);
                request.checksum = CreateHash(checksum);
            }
            else
            {
                function = $"/api/billinfo";
                var sv = providerService.ServiceCode.Split('|');
                request = new RequestBillDto
                {
                    username = providerInfo.Username,
                    reqid = payBillRequestLog.TransCode,
                    reqtime = DateTime.Now.ToString("yyyyMMddHHmmss"),
                    customerCode = payBillRequestLog.ReceiverInfo,
                    password = EncryptTripleDES(providerInfo.Password, providerInfo.ApiPassword),
                    serviceCode = sv[0],
                    providerCode = sv[1],
                    areaCode = sv[2],
                };

                var checksum = string.Join("", request.username, request.password,
                     request.reqid, request.reqtime, request.serviceCode, request.providerCode, request.customerCode, providerInfo.PublicKey);
                request.checksum = CreateHash(checksum);
            }


            _logger.LogInformation($"{payBillRequestLog.TransCode} FinVietConnector Query Bill : " + request.ToJson());
            var reponse = providerService.ServiceName == "POSTPAID"
                ? await CallApi<RequestBillDto>(providerInfo, function, request.reqid, request)
                : await CallApiQuery(providerInfo, function, request);
            _logger.LogInformation($"{payBillRequestLog.ProviderCode}-{payBillRequestLog.TransCode} FinVietConnector Query return: {payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{reponse.ToJson()}");
            var result = reponse.ConvertTo<ResponseBillDto>();
            if (result.code == "0")
            {
                InvoiceResultDto dto = null;
                responseMessage.ResponseStatus.ErrorCode = ResponseCodeConst.Success;
                responseMessage.ResponseStatus.Message = "Thành công";
                if (providerService.ServiceName == "POSTPAID")
                {
                    dto = new InvoiceResultDto
                    {
                        Amount = result.debitamt,
                        CustomerReference = payBillRequestLog.ReceiverInfo,
                        BillId = payBillRequestLog.ReceiverInfo,
                        CustomerName = string.Empty,
                        Address = string.Empty,
                        Period = string.Empty
                    };
                }
                else
                {
                    dto = new InvoiceResultDto
                    {
                        Amount = result.amount,
                        CustomerReference = payBillRequestLog.ReceiverInfo,
                        BillId = payBillRequestLog.ReceiverInfo,
                        CustomerName = result.customerInfo != null ? result.customerInfo.name : string.Empty,
                        Address = result.customerInfo != null ? result.customerInfo.address : string.Empty,
                        Period = result.billInfos != null ? result.billInfos.First().period : "",
                        PeriodDetails = result.billInfos != null
                   ? (from x in result.billInfos
                      select new PeriodDto
                      {
                          Amount = x.amount,
                          Period = x.period,
                          BillNumber = x.billId,
                          BillType = string.Empty,
                      }).ToList()
                   : new List<PeriodDto>()
                    };

                }
                responseMessage.Results = dto;
            }
            else
            {
                var reResult = await TopupGatewayService.GetResponseMassageCacheAsync(ProviderConst.FINVIET, result.code, payBillRequestLog.TransCode);
                responseMessage.ResponseStatus.ErrorCode = reResult != null ? reResult.ResponseCode : ResponseCodeConst.ResponseCode_WaitForResult;
                responseMessage.ResponseStatus.Message = reResult != null ? reResult.ResponseName : "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
            }

            return responseMessage;
        }

        public override async Task<MessageResponseBase> CardGetByBatchAsync(CardRequestLogDto cardRequestLog)
        {
            _logger.Log(LogLevel.Information, $"{cardRequestLog.TransCode} Get card request: " + cardRequestLog.ToJson());
            var responseMessage = new MessageResponseBase();

            var providerInfo = await TopupGatewayService.ProviderInfoCacheGetAsync(cardRequestLog.ProviderCode);

            if (providerInfo == null)
                return responseMessage;

            if (!TopupGatewayService.ValidConnector(ProviderConst.FINVIET, providerInfo.ProviderCode))
            {
                _logger.LogError($"{cardRequestLog.TransCode}-{cardRequestLog.TransRef}-{cardRequestLog.ProviderCode} - FinVietConnector ProviderConnector not valid");
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error,
                    ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
                };
            }

            var sk = cardRequestLog.ProductCode.Split('_');
            var key = string.Join("_", sk.Skip(0).Take(2));
            var product = providerInfo.ProviderServices.Find(p => p.ProductCode == key);
            if (product == null)
            {
                _logger.LogError($"{cardRequestLog.TransCode}-{cardRequestLog.TransRef}-{cardRequestLog.ProviderCode} - FinVietConnector ProviderConnector not config productCode= {cardRequestLog.ProductCode}");
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error,
                    ResponseMessage = "Giao dịch lỗi. Chưa cấu hình sản phẩm"
                };
            }

            var request = new RequestDto
            {
                username = providerInfo.Username,
                password = EncryptTripleDES(providerInfo.Password, providerInfo.ApiPassword),
                reqid = cardRequestLog.TransCode,
                quantity = cardRequestLog.Quantity,
                cardtype = product.ServiceCode,
                cardamount = cardRequestLog.TransAmount,
                reqtime = DateTime.Now.ToString("yyyyMMddHHmmss"),
            };

            var checksum = string.Join("", request.username, request.password,
                 request.reqid, request.reqtime, request.cardtype, request.cardamount, request.quantity, providerInfo.PublicKey);

            request.checksum = CreateHash(checksum);
            responseMessage.TransCodeProvider = cardRequestLog.TransCode;

            _logger.LogInformation($"TransCode= {cardRequestLog.TransCode} FinVietConnector - Card object send: {request.ToJson()}");

            var reponse = await CallApi<RequestDto>(providerInfo, "api/buycard", request.reqid, request);
            var result = reponse.ConvertTo<ResponseDto>();

            cardRequestLog.ModifiedDate = DateTime.Now;
            responseMessage.ProviderResponseCode = result.code;
            responseMessage.ProviderResponseMessage = result.message;
            responseMessage.ProviderResponseTransCode = result.transid;
            _logger.LogInformation($"ProviderCode= {cardRequestLog.ProviderCode} - TransCode= {cardRequestLog.TransCode} FinVietConnector - Card return: {result.ToJson()}");
            if (result.code == "0")
            {
                responseMessage.ResponseCode = ResponseCodeConst.Success;
                responseMessage.ResponseMessage = "Giao dịch thành công";
                cardRequestLog.Status = TransRequestStatus.Success;
                try
                {
                    var cardList = result.cards.Select(card => new CardRequestResponseDto
                    {
                        CardType = cardRequestLog.Vendor,
                        CardValue = card.amount,
                        CardCode = card.password,
                        Serial = card.serial,
                        ExpiredDate = UnixTimeStampToDateTime(card.expireddate),
                        ExpireDate = UnixTimeStampToDateTime(card.expireddate).ToString("yyyy-MM-dd HH:mm:ss")
                    }).ToList();

                    cardList = GenDecryptListCode(cardList, providerInfo.ApiPassword);
                    responseMessage.Payload = cardList;
                }
                catch (Exception e)
                {
                    _logger.LogError($"TransCode= {cardRequestLog.TransCode} FinVietConnector Error parsing cards: " + e.Message);
                }
            }
            else
            {
                var reResult = await TopupGatewayService.GetResponseMassageCacheAsync(ProviderConst.FINVIET, result.code, cardRequestLog.TransCode);
                cardRequestLog.Status = TransRequestStatus.Fail;
                responseMessage.ResponseCode = reResult != null ? reResult.ResponseCode : ResponseCodeConst.ResponseCode_WaitForResult;
                responseMessage.ResponseMessage = reResult != null ? reResult.ResponseName : result.message;
            }
            await TopupGatewayService.CardRequestLogUpdateAsync(cardRequestLog);
            return responseMessage;

        }

        public override async Task<MessageResponseBase> CheckBalanceAsync(string providerCode, string transCode)
        {
            _logger.LogInformation("Get balance request: " + transCode);
            var responseMessage = new MessageResponseBase();

            var providerInfo = await TopupGatewayService.ProviderInfoCacheGetAsync(providerCode);

            if (providerInfo == null)
                return responseMessage;

            if (!TopupGatewayService.ValidConnector(ProviderConst.FINVIET, providerInfo.ProviderCode))
            {
                _logger.LogError($"providerCode= {providerCode}|transCode= {transCode} - FinVietConnector ProviderConnector not valid");
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error,
                    ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
                };
            }
            var request = new CheckBalanceRequest()
            {
                username = providerInfo.Username,
                password = EncryptTripleDES(providerInfo.Password, providerInfo.ApiPassword),
            };

            string checksum = string.Join("", request.username, request.password, providerInfo.PublicKey);
            request.checksum = CreateHash(checksum);
            _logger.LogInformation($"providerCode= {providerInfo.ProviderCode} - Balance object send: {request.ToJson()}");
            var reponse = await CallApi<CheckBalanceRequest>(providerInfo, "api/balance", transCode, request);
            var result = reponse.ConvertTo<ResponseDto>();
            _logger.Log(LogLevel.Information, $"providerCode= {providerCode}|transCode= {transCode} - FinVietConnector Balance return: {result.ToJson()}");
            if (result.code == "0")
            {
                responseMessage.ResponseCode = ResponseCodeConst.Success;
                responseMessage.ResponseMessage = "Giao dịch thành công";
                responseMessage.Payload = result.balance;
            }
            else
            {
                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
            }
            return responseMessage;
        }

        public Task<MessageResponseBase> DepositAsync(DepositRequestDto request)
        {
            throw new NotImplementedException();
        }

        public override async Task<MessageResponseBase> PayBillAsync(PayBillRequestLogDto payBillRequestLog)
        {
            _logger.LogInformation("FinVietConnector Paybill request: " + payBillRequestLog.ToJson());
            var responseMessage = new MessageResponseBase();

            var providerInfo = await TopupGatewayService.ProviderInfoCacheGetAsync(payBillRequestLog.ProviderCode);

            if (providerInfo == null)
                return responseMessage;

            if (!TopupGatewayService.ValidConnector(ProviderConst.FINVIET, providerInfo.ProviderCode))
            {
                _logger.LogError($"{payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{providerInfo.ProviderCode} FinVietConnector ProviderConnector not valid");
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error,
                    ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
                };
            }

            var providerService = providerInfo.ProviderServices.Find(p => p.ProductCode == payBillRequestLog.ProductCode);
            if (providerService == null)
            {
                _logger.LogError(
                    $"{payBillRequestLog.TransCode} FinVietConnector ProviderService with ProductCode [{payBillRequestLog.ProductCode}] is null");
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error,
                    ResponseMessage = "Giao dịch lỗi. Thông tin sản phẩm của nhà cung cấp chưa được cấu hình"
                };
            }

            var sv = providerService.ServiceCode.Split('|');
            var request = new RequestBillDto
            {
                username = providerInfo.Username,
                reqid = payBillRequestLog.TransCode,
                reqtime = DateTime.Now.ToString("yyyyMMddHHmmss"),
                customerCode = payBillRequestLog.ReceiverInfo,
                payAll = true,
                amount = payBillRequestLog.TransAmount,
                password = EncryptTripleDES(providerInfo.Password, providerInfo.ApiPassword),
                serviceCode = sv[0],
                providerCode = sv[1],
                areaCode = sv[2],
            };

            var checksum = string.Join("", request.username, request.password,
                 request.reqid, request.reqtime, request.serviceCode, request.providerCode, request.customerCode, request.amount, request.payAll.ToString().ToLower(), providerInfo.PublicKey);

            request.checksum = CreateHash(checksum);
            _logger.LogInformation($"{payBillRequestLog.TransCode}  FinVietConnector payBill : " + request.ToJson());
            var reponse = await CallApi<RequestBillDto>(providerInfo, $"/api/billpay", request.reqid, request);
            var result = reponse.ConvertTo<ResponseBillDto>();
            _logger.LogInformation($"{payBillRequestLog.ProviderCode}-{payBillRequestLog.TransCode} FinVietConnector Paybill return: {payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{result.ToJson()}");
            payBillRequestLog.ModifiedDate = DateTime.Now;
            payBillRequestLog.ResponseInfo = request.ToJson();
            responseMessage.ProviderResponseCode = result.code;
            responseMessage.ProviderResponseMessage = result.message;
            responseMessage.ProviderResponseTransCode = result.transid;
            if (result.code == "0")
            {
                payBillRequestLog.ModifiedDate = DateTime.Now;
                payBillRequestLog.ResponseInfo = result.ToJson();
                _logger.LogInformation($"FinVietConnector return: {payBillRequestLog.ProviderCode} - {payBillRequestLog.TransCode} - {payBillRequestLog.TransRef} - {result.ToJson()}");
                payBillRequestLog.Status = TransRequestStatus.Success;
                responseMessage.ResponseCode = ResponseCodeConst.Success;
                responseMessage.ResponseMessage = "Thành công";
            }
            else
            {
                var reResult = await TopupGatewayService.GetResponseMassageCacheAsync(ProviderConst.FINVIET, result.code, payBillRequestLog.TransCode);
                payBillRequestLog.Status = TransRequestStatus.Fail;
                responseMessage.ResponseCode = reResult != null ? reResult.ResponseCode : ResponseCodeConst.ResponseCode_WaitForResult;
                responseMessage.ResponseMessage = reResult != null ? reResult.ResponseName : "Giao dịch chưa có kết quả";
                responseMessage.ProviderResponseCode = result.code;
                responseMessage.ProviderResponseMessage = result.message;
            }

            await TopupGatewayService.PayBillRequestLogUpdateAsync(payBillRequestLog);

            return responseMessage;
        }

        private List<CardRequestResponseDto> GenDecryptListCode(List<CardRequestResponseDto> cardList, string key, bool isTripDes = false)
        {
            try
            {
                foreach (var item in cardList)
                {
                    item.CardCode = DecryptTripleDES(item.CardCode, key);
                    if (isTripDes)
                        item.CardCode = item.CardCode.EncryptTripDes();
                }

                return cardList;
            }
            catch (Exception ex)
            {
                _logger.LogError("GenDecryptListCode exception: " + ex.Message);
                return cardList;
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

        private async Task<object> CallApi<T>(ProviderInfoDto providerInfo, string function, string transCode, T request)
        {
            try
            {
                var client = new JsonServiceClient(providerInfo.ApiUrl)
                {
                    Timeout = TimeSpan.FromSeconds(providerInfo.Timeout)
                };
                var res = await client.PostAsync<object>(function, request);
                return res;
            }
            catch (Exception ex)
            {
                _logger.LogError($"TransCode= {transCode} - Func= {function} - FinVietConnector CallApi Exception: {ex}");
                return new ResponseDto()
                {
                    code = "501102",
                    message = ex.Message
                };
            }
        }

        private async Task<ResponseBillDto> CallApiQuery(ProviderInfoDto providerInfo, string function, RequestBillDto dto)
        {
            try
            {
                var requestData = new RestRequest(function, Method.Get);
                requestData.AddHeader("Content-Type", "application/json");
                requestData.AddJsonBody(dto.ToJson());
                ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
                var client = new RestClient(providerInfo.ApiUrl);
                var response = await client.ExecuteAsync(requestData);
                _logger.LogInformation($"Func= {function} - reqid = {dto.reqid} - customerCode = {dto.customerCode} - FinVietConnector CallApiQuery reponse : {response.Content}|{response.StatusCode}|{response.ErrorMessage}");
                return response.Content.FromJson<ResponseBillDto>();
            }

            catch (Exception ex)
            {
                _logger.LogError($"Func= {function} - reqid = {dto.reqid} - customerCode = {dto.customerCode} - FinVietConnector CallApiQuery Exception: {ex}");
                return new ResponseBillDto()
                {
                    code = ((WebServiceException)ex).StatusCode.ToString(),
                    message = ((WebServiceException)ex).ErrorMessage
                };
            }
        }

        private static string EncryptTripleDES(string password, string keyString)
        {
            var key = Encoding.UTF8.GetBytes(keyString);
            using (var tdes = new TripleDESCryptoServiceProvider())
            {
                tdes.Key = key;
                tdes.IV = new byte[8]; // Initialization vector of 8 bytes for TripleDES
                tdes.Mode = CipherMode.ECB; // ECB mode is used as default for TripleDES
                tdes.Padding = PaddingMode.PKCS7;
                ICryptoTransform encryptor = tdes.CreateEncryptor(tdes.Key, tdes.IV);
                byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
                byte[] encrypted = encryptor.TransformFinalBlock(passwordBytes, 0, passwordBytes.Length);
                return Convert.ToBase64String(encrypted);
            }
        }

        private static string CreateHash(string data)
        {
            using (SHA1 sha1 = SHA1.Create())
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(data);
                byte[] hashBytes = sha1.ComputeHash(inputBytes);
                return Convert.ToBase64String(hashBytes);
            }
        }

        private string DecryptTripleDES(string encryptedValue, string keyString)
        {
            try
            {
                var key = Encoding.UTF8.GetBytes(keyString);
                byte[] iv = new byte[8];
                using (var decryptor = TripleDES.Create())
                {
                    decryptor.Key = key;
                    decryptor.IV = iv;
                    decryptor.Padding = PaddingMode.PKCS7;
                    decryptor.Mode = CipherMode.ECB;
                    using (var decryptorStream = decryptor.CreateDecryptor())
                    using (var memoryStream = new MemoryStream(Convert.FromBase64String(encryptedValue)))
                    using (var cryptoStream = new CryptoStream(memoryStream, decryptorStream, CryptoStreamMode.Read))
                    using (var reader = new StreamReader(cryptoStream, Encoding.UTF8))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"FinVietConnector DecryptTripleDES Exception: {ex}");
                return encryptedValue;
            }
        }

        private static DateTime UnixTimeStampToDateTime(string unixTimeStamp)
        {
            try
            {
                DateTime dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
                dateTime = dateTime.AddMilliseconds(Convert.ToDouble(unixTimeStamp)).ToLocalTime();
                return dateTime;
            }
            catch (Exception ex)
            {
                return DateTime.Now.AddYears(2);
            }
        }

        internal class RequestDto
        {
            public string username { get; set; }
            public string password { get; set; }
            public string reqid { get; set; }
            public string reqtime { get; set; }
            public string telco { get; set; }
            public string phone { get; set; }
            public decimal? amount { get; set; }
            public string cardtype { get; set; }
            public decimal? cardamount { get; set; }
            public decimal? quantity { get; set; }
            public string checksum { get; set; }
        }

        internal class RequestBillDto
        {
            public string username { get; set; }
            public string password { get; set; }
            public string reqid { get; set; }
            public string reqtime { get; set; }
            public string serviceCode { get; set; }
            public string providerCode { get; set; }
            public string customerCode { get; set; }
            public string areaCode { get; set; }
            public string phone { get; set; }
            public decimal? amount { get; set; }
            public bool? payAll { get; set; }
            public string checksum { get; set; }
        }

        internal class CheckTransRequest
        {
            public string username { get; set; }
            public string reqid { get; set; }
            public string checksum { get; set; }
        }

        internal class CheckBalanceRequest
        {
            public string username { get; set; }
            public string password { get; set; }
            public string checksum { get; set; }
        }

        internal class ResponseDto
        {
            public string code { get; set; }
            public string message { get; set; }
            public string reqid { get; set; }
            public string transid { get; set; }
            public decimal balance { get; set; }
            public decimal minAmt { get; set; }
            public List<cardItemDto> cards { get; set; }
        }

        internal class ResponseBillDto
        {
            public string code { get; set; }
            public string message { get; set; }
            public string reqid { get; set; }
            public string transid { get; set; }
            public decimal minAmt { get; set; }
            public decimal paymentFee { get; set; }
            public decimal debitamt { get; set; }
            public decimal amount { get; set; }
            public bool isPartialPaymentAllowed { get; set; }
            public List<billInfoDto> billInfos { get; set; }
            public customerInfoDto customerInfo { get; set; }
        }

        internal class customerInfoDto
        {
            public string code { get; set; }

            public string name { get; set; }

            public string address { get; set; }

        }

        internal class cardItemDto
        {
            public string amount { get; set; }
            public string serial { get; set; }
            public string password { get; set; }
            public string expireddate { get; set; }
        }

        internal class billInfoDto
        {
            public string billId { get; set; }

            public string billName { get; set; }

            public decimal amount { get; set; }

            public string period { get; set; }
        }

        internal class ResponseTransDto
        {
            public string code { get; set; }
            public string message { get; set; }
            public string reqid { get; set; }
            public ResponseDto trans { get; set; }
        }

    }
}
