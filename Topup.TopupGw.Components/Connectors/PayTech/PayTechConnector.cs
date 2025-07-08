using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using Topup.Shared;
using Topup.Shared.Dtos;
using Topup.TopupGw.Components.Connectors.Imedia;
using Topup.TopupGw.Contacts.ApiRequests;
using Topup.TopupGw.Contacts.Dtos;
using Topup.TopupGw.Contacts.Enums;
using Topup.TopupGw.Domains.BusinessServices;
using Topup.TopupGw.Domains.Entities;
using Microsoft.Extensions.Logging;
using MongoDB.Driver.Core.WireProtocol.Messages;
using RestSharp;
using ServiceStack;

namespace Topup.TopupGw.Components.Connectors.PayTech;

public class PayTechConnector : IGatewayConnector
{
    private readonly ILogger<PayTechConnector> _logger;
    private readonly ITopupGatewayService _topupGatewayService;

    public PayTechConnector(ITopupGatewayService topupGatewayService, ILogger<PayTechConnector> logger)
    {
        _topupGatewayService = topupGatewayService;
        ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, errors) => true;
        _logger = logger;
    }

    public async Task<MessageResponseBase> TopupAsync(TopupRequestLogDto topupRequestLog, ProviderInfoDto providerInfo)
    {
        using (_logger.BeginScope(topupRequestLog.TransCode))
        {
            _logger.LogInformation("PayTechConnector topup request: " + topupRequestLog.ToJson());
            var responseMessage = new MessageResponseBase();
            try
            {
                if (!_topupGatewayService.ValidConnector(ProviderConst.PAYTECH, providerInfo.ProviderCode))
                {
                    _logger.LogError(
                        $"{topupRequestLog.TransCode}-{topupRequestLog.TransRef}-{providerInfo.ProviderCode}-PayTechConnector ProviderConnector not valid");
                    return new MessageResponseBase
                    {
                        ResponseCode = ResponseCodeConst.Error,
                        ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
                    };
                }

                var sp = topupRequestLog.ProductCode.Split('_');
                string keyCode = sp.Length >= 2 ? sp[0] + "_" + sp[1] : sp[0];

                var providerService = providerInfo.ProviderServices.Find(p => p.ProductCode == keyCode);
                if (providerService == null)
                {
                    _logger.LogError($"{topupRequestLog.TransCode} ProviderService with ProductCoode is null");
                    return new MessageResponseBase
                    {
                        ResponseCode = ResponseCodeConst.Error,
                        ResponseMessage = "Giao dịch lỗi. Thông tin sản phẩm nhà cung cấp chưa được cấu hình"
                    };
                }

                var info = GetMemberInfo(providerService, providerInfo.ExtraInfo);

                var request = await FillterDataRequest(providerInfo, info, new DataInput()
                {
                    Function = Function_PayTech.Order,
                    ServiceCode = topupRequestLog.ServiceCode,
                    Topup = topupRequestLog,
                    Status = 1,
                });

                _logger.LogInformation($"{topupRequestLog.TransCode} TopupRequestAsync : " + request.ToJson());
                var result = await CallApi(providerInfo, request);

                if (result != null)
                {
                    topupRequestLog.ModifiedDate = DateTime.Now;
                    topupRequestLog.ResponseInfo = result.ToJson();

                    _logger.LogInformation(
                        $"ProviderCode= {topupRequestLog.ProviderCode}|TransCode= {topupRequestLog.TransCode}|TransRef= {topupRequestLog.TransRef} Topup return: {result.ToJson()}");
                    if (result.code == "SUC")
                    {
                        var orderReponse = result.order;
                        var transReponse = result.transactions.FirstOrDefault();
                        var reponseMapStatus = CheckMapStatus(orderReponse, transReponse);

                        if (reponseMapStatus == TransRequestStatus.Success)
                        {
                            topupRequestLog.Status = TransRequestStatus.Success;
                            responseMessage.ResponseCode = ResponseCodeConst.Success;
                            responseMessage.ResponseMessage = "Giao dịch thành công";
                        }
                        else
                        {
                            var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(
                                ProviderConst.PAYTECH,
                                transReponse.status, topupRequestLog.TransCode);
                            if (reponseMapStatus == TransRequestStatus.Fail)
                            {
                                topupRequestLog.Status = TransRequestStatus.Fail;
                                responseMessage.ResponseCode =
                                    reResult != null ? reResult.ResponseCode : ResponseCodeConst.ResponseCode_ErrorProvider;
                                responseMessage.ResponseMessage =
                                    reResult != null ? reResult.ResponseName : "Giao dịch lỗi phía NCC";
                            }
                            else
                            {
                                topupRequestLog.Status = TransRequestStatus.Timeout;
                                responseMessage.ResponseCode = reResult != null
                                    ? reResult.ResponseCode
                                    : ResponseCodeConst.ResponseCode_WaitForResult;
                                responseMessage.ResponseMessage = reResult != null
                                    ? reResult.ResponseName
                                    : "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                            }
                        }

                        responseMessage.ProviderResponseCode = $"{orderReponse.status},{transReponse.status}";
                        responseMessage.ProviderResponseMessage =
                            $"{orderReponse.message}-{orderReponse.reason},{transReponse.status}-{transReponse.message}";
                    }
                    else
                    {
                        string keySearch = result.reason.Split('-')[0].TrimStart(' ').TrimEnd(' ');
                        var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.PAYTECH,
                            keySearch, topupRequestLog.TransCode);
                        if (new[] { "FAI", "BUS", "INV" }.Contains(result.code))
                        {
                            topupRequestLog.Status = TransRequestStatus.Fail;
                            responseMessage.ResponseCode =
                                reResult != null ? reResult.ResponseCode : ResponseCodeConst.ResponseCode_ErrorProvider;
                            responseMessage.ResponseMessage = reResult != null ? reResult.ResponseName : "Giao dịch lỗi phía NCC";
                        }
                        else
                        {
                            topupRequestLog.Status = TransRequestStatus.Timeout;
                            responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                            responseMessage.ResponseMessage = reResult != null
                                ? reResult.ResponseName
                                : "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                        }

                        responseMessage.ProviderResponseCode = result.code;
                        responseMessage.ProviderResponseMessage = result.reason;
                    }
                }
                else
                {
                    _logger.LogInformation("Error send request");
                    responseMessage.ResponseMessage = "Lỗi kết nối nhà cung cấp";
                    topupRequestLog.Status = TransRequestStatus.Fail;
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
    }

    public async Task<MessageResponseBase> TransactionCheckAsync(string providerCode, string transCodeToCheck,
        string transCode, string serviceCode = null, ProviderInfoDto providerInfo = null)
    {
        try
        {
            _logger.LogInformation(
                $"PayTechConnector check request: {transCodeToCheck}-{transCode}-{providerCode}-{serviceCode}");
            var responseMessage = new MessageResponseBase();
            if (providerInfo == null)
                providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(providerCode);

            if (providerInfo == null ||
                !_topupGatewayService.ValidConnector(ProviderConst.PAYTECH, providerInfo.ProviderCode))
            {
                _logger.LogError(
                    $"{transCode}-{providerCode}-{providerCode}-PayTechConnector ProviderConnector not valid");
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult,
                    ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ"
                };
            }

            transCode = DateTime.Now.ToString("yyyyMMddHHmmssfff");
            var request = await FillterDataRequest(providerInfo, null, new DataInput()
            {
                Function = Function_PayTech.Query,
                ServiceCode = serviceCode,
                TransCodeConfirm = transCodeToCheck,
                TransCode = transCode,
                Status = 1,
            });

            _logger.LogInformation($"{transCodeToCheck} CheckTran : " + request.ToJson());
            var result = await CallApi(providerInfo, request);

            if (result != null)
            {
                _logger.LogInformation(
                    $"providerCode= {providerCode}|transCodeToCheck= {transCodeToCheck} PayTechConnector check return:{transCode}-{transCodeToCheck} => {result.ToJson()}");
                if (result.code == "SUC")
                {
                    var orderReponse = result.order;
                    var transReponse = result.transactions.FirstOrDefault();
                    var reponseMapStatus = CheckMapStatus(orderReponse, transReponse);

                    if (reponseMapStatus == TransRequestStatus.Success)
                    {
                        responseMessage.ResponseCode = ResponseCodeConst.Success;
                        responseMessage.ResponseMessage = "Giao dịch thành công";
                    }
                    else
                    {
                        //var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.PAYTECH, transReponse.status, transCode);
                        if (reponseMapStatus == TransRequestStatus.Fail)
                        {
                            responseMessage.ResponseCode = ResponseCodeConst.Error;
                            responseMessage.ResponseMessage = "Giao dịch lỗi phía NCC";
                        }
                        else
                        {
                            responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                            responseMessage.ResponseMessage =
                                "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                        }
                    }

                    responseMessage.ProviderResponseCode = $"{orderReponse.status},{transReponse.status}";
                    responseMessage.ProviderResponseMessage = $"{orderReponse.message},{transReponse.message}";
                }
                else
                {
                    //var keySearch = result.reason.Split('-')[0].TrimEnd(' ').TrimStart(' ');
                    //var reResult =
                    //    await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.PAYTECH, keySearch,
                    //        transCode);
                    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                    responseMessage.ResponseMessage =
                        "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                    responseMessage.ProviderResponseCode = result?.reason;
                    responseMessage.ProviderResponseMessage = result?.message;
                }
            }
            else
            {
                _logger.LogInformation($"{transCodeToCheck} Error send request");
                responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                responseMessage.ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
            }

            return responseMessage;
        }
        catch (Exception ex)
        {
            return new MessageResponseBase
            {
                ResponseMessage = "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ",
                ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult,
                Exception = ex.Message
            };
        }
    }

    public async Task<NewMessageResponseBase<InvoiceResultDto>> QueryAsync(PayBillRequestLogDto payBillRequestLog)
    {
        _logger.LogInformation($"{payBillRequestLog.TransCode} PayTechConnector query request: " +
                               payBillRequestLog.ToJson());
        var responseMessage = new NewMessageResponseBase<InvoiceResultDto>
        {
            ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error, "Truy vấn thông tin không thành công")
        };

        var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(payBillRequestLog.ProviderCode);

        if (providerInfo == null)
            return responseMessage;

        if (!_topupGatewayService.ValidConnector(ProviderConst.PAYTECH, providerInfo.ProviderCode))
        {
            _logger.LogError(
                $"{payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{providerInfo.ProviderCode}-PayTechConnector ProviderConnector not valid");
            responseMessage.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error,
                "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ");
            return responseMessage;
        }

        var providerService = providerInfo.ProviderServices.Find(p => p.ProductCode == payBillRequestLog.ProductCode);
        if (providerService == null)
        {
            _logger.LogError(
                $"{payBillRequestLog.TransCode} ProviderService with ProductCode [{payBillRequestLog.ProductCode}] is null");
            return new NewMessageResponseBase<InvoiceResultDto>()
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error,
                    "Giao dịch lỗi. Thông tin sản phẩm nhà cung cấp chưa được cấu hình")
            };
        }

        var info = GetMemberInfo(providerService, providerInfo.ExtraInfo);

        var request = await FillterDataRequest(providerInfo, info, new DataInput()
        {
            Function = Function_PayTech.Order,
            ServiceCode = payBillRequestLog.ServiceCode,
            PayBill = payBillRequestLog,
            TransCode = payBillRequestLog.TransCode,
            Status = 0,
        });

        _logger.LogInformation($"{payBillRequestLog.TransCode} Query Bill : " + request.ToJson());
        var result = await CallApi(providerInfo, request);

        if (result != null)
        {
            _logger.LogInformation(
                $"{payBillRequestLog.ProviderCode}-{payBillRequestLog.TransCode} PayTechConnector Query return: {payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{result.ToJson()}");
            if (result.code == "SUC")
            {
                var orderReponse = result.order;
                var transReponse = result.transactions != null ? result.transactions.FirstOrDefault() : null;
                var reponseMapStatus = CheckMapStatus(orderReponse, transReponse);
                if (transReponse != null && transReponse.status == "REA")
                {
                    var bill = result.bills != null ? result.bills.FirstOrDefault() : null;
                    var cost = result.costs != null ? result.costs.FirstOrDefault() : null;
                    if (bill != null)
                    {
                        responseMessage.Results = new InvoiceResultDto
                        {
                            Amount = bill.amount,
                            CustomerReference = bill.customerName,
                            CustomerName = bill.customerName,
                            Address = bill.customerAddress,
                            Period = bill.paymentCycle,
                            BillType = cost != null ? cost.type : "",
                        };
                        responseMessage.ResponseStatus.ErrorCode = ResponseCodeConst.Success;
                        responseMessage.ResponseStatus.Message = "Giao dịch thành công";
                    }
                }

                if (reponseMapStatus == TransRequestStatus.Timeout)
                {
                    var requestHuy = await FillterDataRequest(providerInfo, null, new DataInput()
                    {
                        Function = Function_PayTech.Confirm,
                        TransCodeConfirm = orderReponse.orderId,
                        TransCode = payBillRequestLog.TransCode + "_HUY",
                        Status = 0,
                    });

                    await CallApi(providerInfo, requestHuy);
                }
            }
            else
            {
                var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.PAYTECH,
                    result.reason, payBillRequestLog.TransCode);
                if (new[] { "FAI", "BUS", "INV" }.Contains(result.code))
                {
                    responseMessage.ResponseStatus.ErrorCode =
                        reResult != null ? reResult.ResponseCode : ResponseCodeConst.ResponseCode_ErrorProvider;
                    responseMessage.ResponseStatus.Message = reResult != null ? reResult.ResponseName : "Giao dịch lỗi phía NCC";
                }
                else
                {
                    responseMessage.ResponseStatus.ErrorCode = ResponseCodeConst.ResponseCode_WaitForResult;
                    responseMessage.ResponseStatus.Message =
                        "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                }
            }
            //responseMessage.ProviderResponseCode = result?.ResCode;
            //responseMessage.ProviderResponseMessage = result?.ResMessage;
        }
        else
        {
            _logger.LogInformation($"{payBillRequestLog.TransCode} Error send request");
            responseMessage.ResponseStatus.Message = "Lỗi kết nối nhà cung cấp";
        }

        return responseMessage;
    }

    public async Task<MessageResponseBase> CardGetByBatchAsync(CardRequestLogDto cardRequestLog)
    {
        _logger.LogInformation($"{cardRequestLog.TransCode} Get card request: " + cardRequestLog.ToJson());
        var responseMessage = new MessageResponseBase();

        var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(cardRequestLog.ProviderCode);

        if (providerInfo == null)
            return responseMessage;

        if (!_topupGatewayService.ValidConnector(ProviderConst.PAYTECH, providerInfo.ProviderCode))
        {
            _logger.LogError(
                $"{cardRequestLog.TransCode}-{cardRequestLog.TransRef}-{cardRequestLog.ProviderCode}-PayTechConnector ProviderConnector not valid");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
            };
        }

        var sp = cardRequestLog.ProductCode.Split('_');
        string keyCode = sp.Length >= 2 ? sp[0] + "_" + sp[1] : sp[0];

        var providerService = providerInfo.ProviderServices.Find(p => p.ProductCode == keyCode);
        if (providerService == null)
        {
            _logger.LogError($"{cardRequestLog.TransCode} ProviderService with ProductCoode is null");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Giao dịch lỗi. Thông tin sản phẩm nhà cung cấp chưa được cấu hình"
            };
        }

        if (string.IsNullOrEmpty(cardRequestLog.ServiceCode))
        {
            if (keyCode.Contains("PINCODE")) cardRequestLog.ServiceCode = "PIN_CODE";
            else if (keyCode.Contains("PINDATA")) cardRequestLog.ServiceCode = "PIN_DATA";
            else if (keyCode.Contains("PINGAME")) cardRequestLog.ServiceCode = "PIN_GAME";
        }

        var info = GetMemberInfo(providerService, providerInfo.ExtraInfo);

        var request = await FillterDataRequest(providerInfo, info, new DataInput()
        {
            Function = Function_PayTech.Order,
            ServiceCode = cardRequestLog.ServiceCode,
            Card = cardRequestLog,
            Status = 1,
        });

        _logger.LogInformation($"{cardRequestLog.TransCode}  CardRequestAsync : " + request.ToJson());
        var result = await CallApi(providerInfo, request);
        if (result != null)
        {
            cardRequestLog.ModifiedDate = DateTime.Now;
            cardRequestLog.ResponseInfo = result.ToJson();
            _logger.Log(LogLevel.Information,
                $"ProviderCode= {cardRequestLog.ProviderCode}|TransCode= {cardRequestLog.TransCode} PayTechConnector|Card return: {result.ToJson()}");
            if (result.code == "SUC")
            {
                var orderReponse = result.order;
                var transReponse = result.transactions != null ? result.transactions.FirstOrDefault() : null;
                var reponseMapStatus = CheckMapStatus(orderReponse, transReponse);
                if (reponseMapStatus == TransRequestStatus.Success)
                {
                    cardRequestLog.Status = TransRequestStatus.Success;
                    responseMessage.ResponseCode = ResponseCodeConst.Success;
                    responseMessage.ResponseMessage = "Giao dịch thành công";
                    try
                    {
                        var cardList = new List<CardRequestResponseDto>();
                        //foreach (var card in result.Data.CardsList)
                        //    cardList.Add(new CardRequestResponseDto
                        //    {
                        //        CardType = cardRequestLog.Vendor,
                        //        CardValue = (int.Parse(cardRequestLog.ProductCode.Split('_')[2]) * 1000).ToString(),
                        //        CardCode = card.CardCode,
                        //        Serial = card.Serial,
                        //        ExpireDate = card.ExpiredDate,
                        //        ExpiredDate = convertExpiredDate(card.ExpiredDate)
                        //    });

                        cardList = GenDecryptListCode(providerInfo.PrivateKeyFile, cardList);
                        responseMessage.Payload = cardList;
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(
                            $"TransCode= {cardRequestLog.TransCode} PayTechConnector Error parsing cards: " +
                            e.Message);
                    }
                }
                else if (reponseMapStatus == TransRequestStatus.Fail)
                {
                    // var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.PAYTECH, result.reason, cardRequestLog.TransCode);
                    cardRequestLog.Status = TransRequestStatus.Fail;
                    responseMessage.ResponseCode = ResponseCodeConst.Error;
                    responseMessage.ResponseMessage = "Giao dịch thất bại";
                }
                else
                {
                    cardRequestLog.Status = TransRequestStatus.Timeout;
                    responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                    responseMessage.ResponseMessage =
                        "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                }
                // responseMessage.ProviderResponseCode = result != null ? result.reason : "";
                // responseMessage.ProviderResponseMessage = result?.message;
            }
            else
            {
                _logger.LogInformation($"TransCode= {cardRequestLog.TransCode} ESaleConnector Error send request");
                responseMessage.ResponseMessage = "Lỗi kết nối nhà cung cấp";
                cardRequestLog.Status = TransRequestStatus.Fail;
            }

            await _topupGatewayService.CardRequestLogUpdateAsync(cardRequestLog);
        }

        return responseMessage;
    }

    public async Task<MessageResponseBase> CheckBalanceAsync(string providerCode, string transCode)
    {
        _logger.LogInformation("Get balance request: " + transCode);
        var responseMessage = new MessageResponseBase();

        try
        {
            var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(providerCode);

            if (providerInfo == null)
                return responseMessage;

            if (!_topupGatewayService.ValidConnector(ProviderConst.PAYTECH, providerInfo.ProviderCode))
            {
                _logger.LogError(
                    $"{providerCode}-{transCode}-{providerInfo.ProviderCode}-PayTechConnector ProviderConnector not valid");
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error,
                    ResponseMessage = "Giao dịch lỗi. Thông tin nhà cung cấp không hợp lệ"
                };
            }

            var request = await FillterDataRequest(providerInfo, null, new DataInput()
            {
                Function = Function_PayTech.Balance,
                TransCode = transCode,
                Status = 1,
            });

            _logger.LogInformation($"Balance object send: {request.ToJson()}");
            var result = await CallApi(providerInfo, request);
            if (result != null)
            {
                _logger.LogInformation($"Balance return: {providerCode}-{transCode}-{result.ToJson()}");
                if (result.code == "SUC")
                {
                    responseMessage.ResponseCode = ResponseCodeConst.Success;
                    responseMessage.ResponseMessage = "Giao dịch thành công";
                    responseMessage.Payload = result.balance != null ? result.balance.balance : 0;
                }
                else
                {
                    var reResult =
                        await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.PAYTECH, result.reason,
                            transCode);
                    if (new[] { "FAI", "BUS", "INV" }.Contains(result.code))
                    {
                        responseMessage.ResponseCode =
                            reResult != null ? reResult.ResponseCode : ResponseCodeConst.ResponseCode_ErrorProvider;
                        responseMessage.ResponseMessage = reResult != null ? reResult.ResponseName : "Giao dịch lỗi phía NCC";
                    }
                    else
                    {
                        responseMessage.ResponseCode = ResponseCodeConst.ResponseCode_WaitForResult;
                        responseMessage.ResponseMessage = reResult != null ? reResult.ResponseName : result.message;
                    }
                }
            }
            else
            {
                _logger.LogInformation("Error send request");
                responseMessage.ResponseMessage = "Lỗi kết nối nhà cung cấp";
            }
        }
        catch (Exception ex)
        {
            responseMessage.Exception = ex.Message;
            _logger.LogInformation(
                $"providerCode= {providerCode}|transCode= {transCode}|CheckBalanceAsync_Exception: {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
        }

        return responseMessage;
    }

    public Task<MessageResponseBase> DepositAsync(DepositRequestDto request)
    {
        throw new NotImplementedException();
    }

    public async Task<MessageResponseBase> PayBillAsync(PayBillRequestLogDto payBillRequestLog)
    {
        _logger.LogInformation("Get Paybill request: " + payBillRequestLog.ToJson());
        var responseMessage = new MessageResponseBase();

        var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(payBillRequestLog.ProviderCode);

        if (providerInfo == null)
            return responseMessage;

        if (!_topupGatewayService.ValidConnector(ProviderConst.PAYTECH, providerInfo.ProviderCode))
        {
            _logger.LogError(
                $"{payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{providerInfo.ProviderCode} PayTechConnector ProviderConnector not valid");
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
                $"{payBillRequestLog.TransCode} ProviderService with ProductCode [{payBillRequestLog.ProductCode}] is null");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Giao dịch lỗi. Thông tin sản phẩm của nhà cung cấp chưa được cấu hình"
            };
        }

        var info = GetMemberInfo(providerService, providerInfo.ExtraInfo);

        var request = await FillterDataRequest(providerInfo, info, new DataInput()
        {
            Function = Function_PayTech.Order,
            ServiceCode = payBillRequestLog.ServiceCode,
            PayBill = payBillRequestLog,
            Status = 0,
        });

        var result = await CallApi(providerInfo, request);
        _logger.LogInformation($"TransCode= {payBillRequestLog.TransCode} Paybill object send: " + request.ToJson());
        if (result != null)
        {
            payBillRequestLog.ModifiedDate = DateTime.Now;
            payBillRequestLog.ResponseInfo = request.ToJson();
            _logger.LogInformation(
                $"{payBillRequestLog.ProviderCode}-{payBillRequestLog.TransCode} Paybill return: {payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{result.ToJson()}");
            if (result.code == "SUC")
            {
                var orderReponse = result.order;
                var transReponse = result.transactions.FirstOrDefault();
                var reponseMapStatus = CheckMapStatus(orderReponse, transReponse);
                if (reponseMapStatus == TransRequestStatus.Timeout)
                {
                    ///Tiền thanh toán khớp với số tiền truy vấn
                    if (payBillRequestLog.TransAmount == transReponse.amount)
                    {
                        var requestConfirm = await FillterDataRequest(providerInfo, info, new DataInput()
                        {
                            Function = Function_PayTech.Confirm,
                            ServiceCode = payBillRequestLog.ServiceCode,
                            PayBill = payBillRequestLog,
                            TransCodeConfirm = payBillRequestLog.TransCode,
                            TransCode = payBillRequestLog.TransCode + "_Confirm",
                            Status = 1,
                        });
                        var resultConfirm = await CallApi(providerInfo, requestConfirm);
                        _logger.LogInformation(
                            $"{payBillRequestLog.ProviderCode}-{payBillRequestLog.TransCode} Paybill return: {payBillRequestLog.TransCode}-{payBillRequestLog.TransRef}-{resultConfirm.ToJson()}");
                        if (resultConfirm.code == "SUC")
                        {
                            var orderConfirmReponse = resultConfirm.order;
                            var transConfirmReponse = resultConfirm.transactions.FirstOrDefault();
                            var reponseConfirmStatus = CheckMapStatus(orderConfirmReponse, transConfirmReponse);
                            if (reponseConfirmStatus == TransRequestStatus.Success)
                            {
                                payBillRequestLog.Status = TransRequestStatus.Success;
                                responseMessage.ResponseCode = ResponseCodeConst.Success;
                                responseMessage.ResponseMessage = "Giao dịch thành công";
                            }
                            else
                            {
                                var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(
                                    ProviderConst.PAYTECH, transConfirmReponse.status, payBillRequestLog.TransCode);
                                if (reponseConfirmStatus == TransRequestStatus.Fail)
                                {
                                    payBillRequestLog.Status = TransRequestStatus.Fail;
                                    responseMessage.ResponseCode =
                                        reResult != null ? reResult.ResponseCode : ResponseCodeConst.ResponseCode_ErrorProvider;
                                    responseMessage.ResponseMessage =
                                        reResult != null ? reResult.ResponseName : "Giao dịch lỗi phía NCC";
                                }
                                else
                                {
                                    payBillRequestLog.Status = TransRequestStatus.Timeout;
                                    responseMessage.ResponseCode = reResult != null
                                        ? reResult.ResponseCode
                                        : ResponseCodeConst.ResponseCode_WaitForResult;
                                    responseMessage.ResponseMessage = reResult != null
                                        ? reResult.ResponseName
                                        : "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                                }
                            }

                            responseMessage.ProviderResponseCode = $"{orderReponse.status},{transReponse.status}";
                            responseMessage.ProviderResponseMessage =
                                $"{orderReponse.message}-{orderReponse.reason},{transReponse.status}-{transReponse.message}";
                        }
                    }
                    else
                    {
                        _logger.LogInformation($"{payBillRequestLog.TransCode} So tien dan khong khop nhau Amount_Input: {payBillRequestLog.TransAmount} -  Amount_QueryPayTech: { transReponse.amount}");
                        payBillRequestLog.Status = TransRequestStatus.Fail;
                        responseMessage.ResponseCode = ResponseCodeConst.Error;
                        responseMessage.ResponseMessage = "Số tiền thanh toán không khớp với số tiền truy vấn";
                    }
                }
                else if (reponseMapStatus == TransRequestStatus.Success)
                {
                    payBillRequestLog.Status = TransRequestStatus.Success;
                    responseMessage.ResponseCode = ResponseCodeConst.Success;
                    responseMessage.ResponseMessage = "Giao dịch thành công";
                    responseMessage.ProviderResponseCode = $"{orderReponse.status},{transReponse.status}";
                    responseMessage.ProviderResponseMessage =
                        $"{orderReponse.message}-{orderReponse.reason},{transReponse.status}-{transReponse.message}";
                }
                else
                {
                    var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.PAYTECH,
                        transReponse.status, payBillRequestLog.TransCode);
                    payBillRequestLog.Status = TransRequestStatus.Fail;
                    responseMessage.ResponseCode = reResult != null ? reResult.ResponseCode : ResponseCodeConst.ResponseCode_ErrorProvider;
                    responseMessage.ResponseMessage = reResult != null ? reResult.ResponseName : "Giao dịch lỗi phía NCC";
                    responseMessage.ProviderResponseCode = $"{orderReponse.status},{transReponse.status}";
                    responseMessage.ProviderResponseMessage =
                        $"{orderReponse.message}-{orderReponse.reason},{transReponse.status}-{transReponse.message}";
                }
            }
            else
            {
                string keySearch = result.reason.Split('-')[0].TrimStart(' ').TrimEnd(' ');
                var reResult = await _topupGatewayService.GetResponseMassageCacheAsync(ProviderConst.PAYTECH, keySearch,
                    payBillRequestLog.TransCode);
                if (new[] { "FAI", "BUS", "INV" }.Contains(result.code))
                {
                    payBillRequestLog.Status = TransRequestStatus.Fail;
                    responseMessage.ResponseCode = reResult != null ? reResult.ResponseCode : ResponseCodeConst.ResponseCode_ErrorProvider;
                    responseMessage.ResponseMessage = reResult != null ? reResult.ResponseName : "Giao dịch lỗi phía NCC";
                }
                else
                {
                    payBillRequestLog.Status = TransRequestStatus.Timeout;
                    responseMessage.ResponseCode = reResult != null
                        ? reResult.ResponseCode
                        : ResponseCodeConst.ResponseCode_WaitForResult;
                    responseMessage.ResponseMessage = reResult != null ? reResult.ResponseName : "Giao dịch chưa có kết quả";
                }

                responseMessage.ProviderResponseCode = result.code;
                responseMessage.ProviderResponseMessage = result.reason;
            }
        }
        else
        {
            _logger.LogInformation($"{payBillRequestLog.TransCode} Error send request");
            responseMessage.ResponseMessage = "Lỗi kết nối nhà cung cấp";
            payBillRequestLog.Status = TransRequestStatus.Fail;
        }

        await _topupGatewayService.PayBillRequestLogUpdateAsync(payBillRequestLog);


        return responseMessage;
    }

    private string Sign(string dataToSign, string privateFile)
    {
        var privateKeyText = File.ReadAllText("files/" + privateFile);
        var privateKeyBlocks = privateKeyText.Split("-", StringSplitOptions.RemoveEmptyEntries);
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

    private List<CardRequestResponseDto> GenDecryptListCode(string privateFile,
        List<CardRequestResponseDto> cardList)
    {
        try
        {
            var privateKeyText = File.ReadAllText("files/" + privateFile);
            var privateKeyBlocks = privateKeyText.Split("-", StringSplitOptions.RemoveEmptyEntries);
            var privateKeyBytes = Convert.FromBase64String(privateKeyBlocks[1]);
            using var rsa = RSA.Create();
            if (privateKeyBlocks[0] == "BEGIN PRIVATE KEY")
                rsa.ImportPkcs8PrivateKey(privateKeyBytes, out _);
            else if (privateKeyBlocks[0] == "BEGIN RSA PRIVATE KEY")
                rsa.ImportRSAPrivateKey(privateKeyBytes, out _);

            foreach (var item in cardList)
            {
                var souceBytesCode = Convert.FromBase64String(item.CardCode);
                var bytesCode = rsa.Decrypt(souceBytesCode, RSAEncryptionPadding.Pkcs1);
                var stringCode = Encoding.UTF8.GetString(bytesCode, 0, bytesCode.Length);
                item.CardCode = stringCode;
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

    private async Task<PayTechResponse> CallApi(ProviderInfoDto providerInfo, DataRequest request)
    {
        var headerDto = request.HeaderDto;
        try
        {
            _logger.LogInformation($"{headerDto.requestId}-{request.Function} PayTechConnector CallApi_Request: " +
                                   request.ToJson());
            var apiUrl = providerInfo.ApiUrl;
            var requestData = new RestRequest("PartnerAPI/services/interfaces", Method.Post);
            requestData.AddHeader("Content-Type", "application/x-www-form-urlencoded");
            requestData.AddHeader("partnerId", headerDto.partnerId);
            requestData.AddHeader("accountId", headerDto.accountId);
            requestData.AddHeader("requestId", headerDto.requestId);
            requestData.AddHeader("signature", headerDto.signature);
            requestData.AddHeader("requestTime", headerDto.requestTime);

            if (request.Function == Function_PayTech.Balance)
                requestData.AddParameter("function", request.Function);
            else if (request.Function == Function_PayTech.Order)
            {
                var orderDto = request.OrderDto;
                requestData.AddParameter("function", orderDto.function);
                requestData.AddParameter("orderId", orderDto.orderId);
                requestData.AddParameter("refer", orderDto.refer);
                requestData.AddParameter("autoCharge", orderDto.autoCharge);
                requestData.AddParameter("fullname", orderDto.fullname);
                requestData.AddParameter("email", orderDto.email);
                requestData.AddParameter("phoneNumber", orderDto.phoneNumber);
                requestData.AddParameter("transactionId", orderDto.transactionId);
                requestData.AddParameter("type", orderDto.type);
                requestData.AddParameter("issuerId", orderDto.issuerId);
                requestData.AddParameter("target", orderDto.target);
                requestData.AddParameter("productId", orderDto.productId);
                requestData.AddParameter("productName", orderDto.productName);
                requestData.AddParameter("amount", orderDto.amount);
                requestData.AddParameter("quantity", orderDto.quantity);
                requestData.AddParameter("autoRetry", orderDto.autoRetry);
            }
            else if (request.Function == Function_PayTech.Confirm)
            {
                var confirmDto = request.ConfirmDto;
                requestData.AddParameter("function", confirmDto.function);
                requestData.AddParameter("orderId", confirmDto.orderId);
                requestData.AddParameter("status", confirmDto.status);
                requestData.AddParameter("reason", confirmDto.reason);
            }
            else if (request.Function == Function_PayTech.Query)
            {
                var queryDto = request.QueryTransDto;
                requestData.AddParameter("function", queryDto.function);
                requestData.AddParameter("orderId", queryDto.orderId);
                requestData.AddParameter("transactionId", queryDto.transactionId);
            }

            ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
            var client = new RestClient(apiUrl);
            var response = await client.ExecuteAsync(requestData);
            _logger.LogInformation(
                $"TransCode= {headerDto.requestId}|Function= {request.Function} PayTechConnector Reponse : {response.StatusCode}|{response.ResponseStatus}|{response.Content}");
            return response.Content.FromJson<PayTechResponse>();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                $"TransCode= {headerDto.requestId}|Function= {request.Function} PayTechConnector CallApi_Exception : {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
        }

        return new PayTechResponse();
    }

    private async Task<DataRequest> FillterDataRequest(ProviderInfoDto providerInfo, MemberInfo info, DataInput data)
    {
        string key = "NHA";
        key = providerInfo.PublicKey.Split('|')[0];
        var headerDto = new HeaderRequest()
        {
            accountId = providerInfo.Username,
            partnerId = providerInfo.ApiUser,
            requestTime = DateTime.Now.ToString("yyyyMMddHHmmss"),
        };

        var request = new DataRequest()
        {
            Function = data.Function
        };

        if (data.Function == Function_PayTech.Balance)
        {
            headerDto.requestId = data.TransCode;
            if (!headerDto.requestId.StartsWith(key))
                headerDto.requestId = $"{key}_" + headerDto.requestId;
        }
        else if (data.Function == Function_PayTech.Confirm)
        {
            headerDto.requestId = data.TransCode;
            if (!headerDto.requestId.StartsWith(key))
                headerDto.requestId = $"{key}_" + headerDto.requestId;
            var confirmDto = new ConfirmRequest()
            {
                function = Function_PayTech.Confirm,
                orderId = data.TransCodeConfirm,
                reason = data.Status == 1 ? "xac-nhan-thanh-toan" : "Huy-don-hang",
                status = data.Status == 1 ? "APPROVE" : "CANCEL"
                //APPROVE: Duyệt thanh toán cho đơn hàng|CANCEL: Hủy bỏ đơn hàng
            };
            request.ConfirmDto = confirmDto;
        }
        else if (data.Function == Function_PayTech.Download)
        {
            headerDto.requestId = data.TransCode;
            var downloadDto = new DownloadRequest()
            {
                function = Function_PayTech.Download,
                orderId = data.TransCodeConfirm,
                transactionId = data.TransCodeConfirm
            };
            request.DownloadDto = downloadDto;
        }
        else if (data.Function == Function_PayTech.Query)
        {
            headerDto.requestId = data.TransCode;
            if (!headerDto.requestId.StartsWith(key))
                headerDto.requestId = $"{key}_" + headerDto.requestId;

            var queryDto = new QueryRequest()
            {
                function = Function_PayTech.Query,
                orderId = data.TransCodeConfirm,
                transactionId = data.TransCodeConfirm,
            };
            request.QueryTransDto = queryDto;
        }
        else
        {
            var orderDto = new OrderRequest
            {
                function = Function_PayTech.Order,
                refer = headerDto.requestTime,
                autoCharge = data.Status,
                type = info.Service,
                issuerId = info.TelCo,
                fullname = info.FullName,
                email = info.Email,
                phoneNumber = info.PhoneNumber,
                productId = info.ProductId,
                productName = info.ProductName,
                target = "",
                amount = 0,
                quantity = 1,
                autoRetry = "0",
            };

            if (data.ServiceCode == "TOPUP" || data.ServiceCode == "TOPUP_DATA")
            {
                var topupRequestLog = data.Topup;
                headerDto.requestId = topupRequestLog.TransCode;
                if (!headerDto.requestId.StartsWith(key))
                    headerDto.requestId = $"{key}_" + headerDto.requestId;

                orderDto.orderId = headerDto.requestId;
                orderDto.transactionId = headerDto.requestId;
                orderDto.target = topupRequestLog.ReceiverInfo;
                orderDto.amount = topupRequestLog.TransAmount;
            }
            else if (data.ServiceCode == "PIN_CODE" || data.ServiceCode == "PIN_DATA" || data.ServiceCode == "PIN_GAME")
            {
                var cardRequestLog = data.Card;
                headerDto.requestId = cardRequestLog.TransCode;
                if (!headerDto.requestId.StartsWith(key))
                    headerDto.requestId = $"{key}_" + headerDto.requestId;

                orderDto.orderId = headerDto.requestId;
                orderDto.transactionId = headerDto.requestId;
                orderDto.target = "";
                orderDto.quantity = cardRequestLog.Quantity;
                orderDto.amount = cardRequestLog.TransAmount;
            }
            else
            {
                var paybillRequestLog = data.PayBill;
                headerDto.requestId = paybillRequestLog.TransCode;
                if (!headerDto.requestId.StartsWith(key))
                    headerDto.requestId = $"{key}_" + headerDto.requestId;

                orderDto.orderId = headerDto.requestId;
                orderDto.transactionId = headerDto.requestId;
                orderDto.target = paybillRequestLog.ReceiverInfo;
                orderDto.amount = paybillRequestLog.TransAmount;
            }

            request.OrderDto = orderDto;
        }

        string strTxt = $"{headerDto.partnerId}{headerDto.accountId}{headerDto.requestId}{headerDto.requestTime}";
        var sign = Sign(strTxt, providerInfo.PrivateKeyFile);
        headerDto.signature = sign;
        request.HeaderDto = headerDto;
        return request;
    }

    private MemberInfo GetMemberInfo(ProviderServiceDto info, string extraInfo)
    {
        var s = info.ServiceCode.Split('|');
        var f = extraInfo.Split('|');
        return new MemberInfo()
        {
            ProductId = s[0],
            ProductName = info.ServiceName,
            Service = s.Length >= 2 ? s[1] : "",
            TelCo = s.Length >= 3 ? s[2] : "",
            AutoCharge = s.Length >= 4 ? Convert.ToInt16(s[3]) : 0,
            FullName = f[0],
            Email = f.Length >= 2 ? f[1] : "",
            PhoneNumber = f.Length >= 3 ? f[2] : ""
        };
    }

    private TransRequestStatus CheckMapStatus(OrderInfo orderReponse, TransactionInfo transReponse)
    {
        var transStatus = TransRequestStatus.Timeout;

        if ((transReponse != null && transReponse.status == "SUC") ||
            (orderReponse != null && orderReponse.status == "SUC"))
            transStatus = TransRequestStatus.Success;
        else
        {
            if (orderReponse.status == "WFP" && transReponse.status == "REA")
                transStatus = TransRequestStatus.Fail;
            else
            {
                var str = new[] { "REJ", "CAN", "PFR", "REC", "REF", "REE", "PFV", "VOC", "VOD", "VED", "FAI" };
                if ((orderReponse != null && str.Contains(orderReponse.status)) ||
                    (transReponse != null && str.Contains(transReponse.status)))
                    transStatus = TransRequestStatus.Fail;
                else transStatus = TransRequestStatus.Timeout;
            }
        }

        return transStatus;
    }
}