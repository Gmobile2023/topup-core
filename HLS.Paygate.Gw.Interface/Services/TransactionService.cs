using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using HLS.Paygate.Gw.Interface.Filters;
using HLS.Paygate.Gw.Model.RequestDtos;
using HLS.Paygate.Shared;
using HLS.Paygate.Shared.Common;
using HLS.Paygate.Shared.ConfigDtos;
using HLS.Paygate.Shared.Dtos;
using HLS.Paygate.Shared.Utils;
using MassTransit;
using Microsoft.Extensions.Logging;
using Paygate.Contracts.Commands.Commons;
using Paygate.Contracts.Requests.Commons;
using Paygate.Discovery.Requests.Workers;
using ServiceStack;

namespace HLS.Paygate.Gw.Interface.Services;

public partial class TopupService
{
    #region CheckTrans

    [Authenticate]
    [PartnerFilter]
    public async Task<object> GetAsync(CheckTransAuthenRequest check)
    {
        try
        {
            if (check == null)
                return new NewMessageReponseBase<string>
                {
                    ResponseStatus =
                        new ResponseStatusApi(ResponseCodeConst.ResponseCode_00, "Yêu cầu không hợp lệ")
                };
            _logger.LogInformation("CheckTransAuthenRequest: {Request}", check.ToJson());
            var rs = await _saleService.SaleRequestCheckAsync(check.TransCodeToCheck, check.PartnerCode);
            var response = new NewMessageReponseBase<string>
            {
                Results = rs.ExtraInfo,
                ResponseStatus = new ResponseStatusApi(rs.ResponseCode, rs.ResponseMessage)
                {
                    TransCode = check.TransCodeToCheck
                }
            };
            _logger.LogInformation("CheckTransAuthen return: {Response}", response.ToJson());
            return response;
        }
        catch (Exception e)
        {
            _logger.LogError($"CheckTransAuthenRequest:{check?.TransCode}-{check?.TransCodeToCheck}-{e}");
            return new NewMessageReponseBase<string>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_WaitForResult,
                    "Giao dịch chưa có kết quả")
            };
        }
    }

    // checktrans v2 
    // trả thêm thông tin giao dịch
    [Authenticate]
    [PartnerFilter]
    public async Task<object> GetAsync(CheckTransAuthenV2Request check)
    {
        _logger.LogInformation("ChecktransV2Request: {Request}", check.ToJson());
        var rs = await _saleService.SaleRequestPartnerChecktransAsync(check.TransCodeToCheck, check.PartnerCode,
            check.ClientKey);

        var responseMessage = new ResponseStatusApi(rs.ResponseCode, rs.ResponseMessage)
        {
            TransCode = rs.ExtraInfo ?? check.TransCodeToCheck
        };
        var response = new NewMessageReponseBase<CheckTransResult>
        {
            Results = rs.Payload,
            ResponseStatus = responseMessage
        };
        _logger.LogInformation($"ChecktransV2Return:{response.ToJson()}");
        return response;
    }

    // checktrans v2 . Đặt route riêng cho ASIM
    [Authenticate]
    [PartnerFilter]
    public async Task<object> GetAsync(CheckTransAuthenNewRequest check)
    {
        _logger.LogInformation("CheckTransAuthenNewRequest: {Request}", check.ToJson());
        var rs = await _saleService.SaleRequestPartnerChecktransAsync(check.TransCodeToCheck, check.PartnerCode,
            check.TransCodeToCheck);

        var responseMessage = new ResponseStatusApi(rs.ResponseCode, rs.ResponseMessage)
        {
            TransCode = rs.ExtraInfo ?? check.TransCodeToCheck
        };
        var response = new NewMessageReponseBase<CheckTransResult>
        {
            Results = rs.Payload,
            ResponseStatus = responseMessage
        };
        _logger.LogInformation($"CheckTransAuthenNewReturn:{response.ToJson()}");
        return response;
    }

    #endregion

    [Authenticate]
    [PartnerFilter]
    public async Task<object> PostAsync(TopupPartnerRequest topupRequest)
    {
        if (topupRequest == null)
            return new NewMessageReponseBase<WorkerResult>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_00, "Yêu cầu không hợp lệ")
            };
        //var headers = base.Request.Headers.ToJson();
        //_logger.LogInformation("Get Header" + headers);
        _logger.LogInformation("{Partner}: TopupRequest {Request}", topupRequest.PartnerCode,
            topupRequest.ToJson());

        try
        {
            var useQueueTopup = true;
            var useQueueTopupConfig = _configuration["RabbitMq:UseQueueTopup"];
            if (!string.IsNullOrEmpty(useQueueTopupConfig))
                useQueueTopup = bool.Parse(_configuration["RabbitMq:UseQueueTopup"]);

            var response = new NewMessageReponseBase<SaleResult>
            {
                Results = new SaleResult
                {
                    Amount = topupRequest.Amount
                },
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_WaitForResult,
                    "Giao dịch chưa có kết quả. Vui lòng liên hệ CSKH để được hỗ trợ")
            };
            var serviceCode = SaleCommon.GetTopupService(topupRequest.CategoryCode);
            if (useQueueTopup)
            {
                var rs = await _topupRequestClient.GetResponse<NewMessageReponseBase<WorkerResult>>(new
                {
                    topupRequest.Amount,
                    Channel = Channel.API,
                    AgentType = AgentType.AgentApi,
                    AccountType = SystemAccountType.MasterAgent,
                    topupRequest.CategoryCode,
                    topupRequest.ProductCode,
                    topupRequest.PartnerCode,
                    topupRequest.ReceiverInfo,
                    RequestIp = Request.RemoteIp,
                    ServiceCode = serviceCode,
                    StaffAccount = topupRequest.PartnerCode,
                    StaffUser = topupRequest.PartnerCode,
                    topupRequest.TransCode,
                    RequestDate = DateTime.Now,
                    topupRequest.IsCheckReceiverType,
                    topupRequest.IsNoneDiscount,
                    topupRequest.DefaultReceiverType,
                    topupRequest.IsCheckAllowTopupReceiverType
                }, CancellationToken.None, RequestTimeout.After(m: 10));
                var mess = rs.Message;
                //_logger.LogInformation($"{topupRequest.TransCode}-GetResponse:{mess.ToJson()}");
                response.ResponseStatus = new ResponseStatusApi
                {
                    ErrorCode = mess.ResponseStatus.ErrorCode,
                    Message = mess.ResponseStatus.Message,
                    TransCode = topupRequest.TransCode
                };
                response.Results.PaymentAmount = mess.Results.PaymentAmount;
                response.Results.TransCode = mess.Results.TransRef;
                response.Results.ReferenceCode = mess.Results.TransCode;
                response.Results.ReceiverType = mess.Results.ReceiverType;
                response.Results.Discount = mess.Results.Discount;
                response.Results.ServiceCode = serviceCode;
            }
            else
            {
                var getApi = await _grpcClient.GetClientCluster(GrpcServiceName.Worker).SendAsync(new WorkerTopupRequest
                {
                    Amount = topupRequest.Amount,
                    Channel = Channel.API,
                    AgentType = AgentType.AgentApi,
                    AccountType = SystemAccountType.MasterAgent,
                    CategoryCode = topupRequest.CategoryCode,
                    ProductCode = topupRequest.ProductCode,
                    PartnerCode = topupRequest.PartnerCode,
                    ReceiverInfo = topupRequest.ReceiverInfo,
                    RequestIp = Request.RemoteIp,
                    ServiceCode = serviceCode,
                    StaffAccount = topupRequest.PartnerCode,
                    StaffUser = topupRequest.PartnerCode,
                    TransCode = topupRequest.TransCode,
                    RequestDate = DateTime.Now,
                    IsCheckReceiverType = topupRequest.IsCheckReceiverType,
                    IsNoneDiscount = topupRequest.IsNoneDiscount,
                    DefaultReceiverType = topupRequest.DefaultReceiverType,
                    IsCheckAllowTopupReceiverType = topupRequest.IsCheckAllowTopupReceiverType
                });
                response.ResponseStatus = new ResponseStatusApi
                {
                    ErrorCode = getApi.ResponseStatus.ErrorCode,
                    Message = getApi.ResponseStatus.Message,
                    TransCode = topupRequest.TransCode
                };
                response.Results.PaymentAmount = getApi.Results.PaymentAmount;
                response.Results.TransCode = getApi.Results.TransRef;
                response.Results.ReferenceCode = getApi.Results.TransCode;
                response.Results.ReceiverType = getApi.Results.ReceiverType;
                response.Results.Discount = getApi.Results.Discount;
                response.Results.ServiceCode = serviceCode;
            }

            _logger.LogInformation("{TransRef}: TopupPartnerRequest response: {Response}", topupRequest.TransCode,
                response.ToJson());
            return response;
        }
        catch (Exception e)
        {
            _logger.LogError("{TransRef}: TopupPartnerRequest exception: {Exception}", topupRequest.TransCode, e);
            await _bus.Publish<SendBotMessage>(new
            {
                Message = $"{topupRequest.TransCode}-{topupRequest.PartnerCode}-TopupPartnerRequest có lỗi: {e}",
                Module = "Gateway",
                MessageType = BotMessageType.Error,
                Title = "TopupPartnerRequest error",
                BotType = BotType.Dev,
                TimeStamp = DateTime.Now,
                CorrelationId = Guid.NewGuid()
            });

            return new NewMessageReponseBase<string>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_WaitForResult,
                    "Giao dịch chưa có kết quả. Vui lòng liên hệ CSKH để được hỗ trợ")
                {
                    TransCode = topupRequest.TransCode
                }
            };
        }
    }

    [Authenticate]
    [PartnerFilter]
    public async Task<object> PostAsync(PinCodePartnerRequest cardSaleRequest)
    {
        try
        {
            _logger.LogInformation("PinCodePartnerRequest {Request}", cardSaleRequest.ToJson());
            var rs = new NewMessageReponseBase<string>
            {
                ResponseStatus =
                    new ResponseStatusApi(ResponseCodeConst.ResponseCode_WaitForResult,
                        "Giao dịch chưa có kết quả. Vui lòng liên hệ CSKH để được hỗ trợ")
            };
            if (cardSaleRequest == null)
                return new NewMessageReponseBase<string>
                {
                    ResponseStatus =
                        new ResponseStatusApi(ResponseCodeConst.ResponseCode_00, "Yêu cầu không hợp lệ")
                };
            if (cardSaleRequest.Quantity <= 0)
                throw new HttpError(HttpStatusCode.BadRequest);
            var response = await _grpcClient.GetClientCluster(GrpcServiceName.Worker).SendAsync(new WorkerPinCodeRequest
            {
                CardValue = cardSaleRequest.CardValue,
                AgentType = AgentType.AgentApi,
                AccountType = SystemAccountType.MasterAgent,
                Channel = Channel.API,
                Email = cardSaleRequest.Email,
                CategoryCode = cardSaleRequest.CategoryCode,
                ProductCode = cardSaleRequest.ProductCode,
                PartnerCode = cardSaleRequest.PartnerCode,
                RequestIp = Request.UserHostAddress,
                ServiceCode = cardSaleRequest.ServiceCode,
                StaffAccount = cardSaleRequest.PartnerCode,
                StaffUser = cardSaleRequest.PartnerCode,
                RequestDate = DateTime.Now,
                Quantity = cardSaleRequest.Quantity,
                TransCode = cardSaleRequest.TransCode
            });
            _logger.LogInformation("PinCodePartnerRequest return {Response}", response.ResponseStatus.ToJson());
            response.ResponseStatus.TransCode = cardSaleRequest.TransCode;


            if (response.ResponseStatus.ErrorCode != ResponseCodeConst.Success) return response;
            var cards = new List<CardResponsePartnerDto>();
            try
            {
                foreach (var item in response.Results)
                {
                    var itemCard = item.ConvertTo<CardResponsePartnerDto>();
                    itemCard.ExpireDate = item.ExpiredDate.ToString("dd/MM/yyyy");
                    itemCard.CardCode = Cryptography.DefaultTripleEncrypt(item.CardCode, cardSaleRequest.ClientKey);
                    cards.Add(itemCard);
                }
            }
            catch (Exception e)
            {
                _logger.LogInformation("Convert card error: {Message}", e);
            }

            return new NewMessageReponseBase<List<CardResponsePartnerDto>>
            {
                ResponseStatus = response.ResponseStatus,
                Results = cards
            };
        }
        catch (Exception e)
        {
            _logger.LogError("PinCodePartnerRequest error {Request}-{Error}", cardSaleRequest?.TransCode, e);
            await _bus.Publish<SendBotMessage>(new
            {
                Message =
                    $"{cardSaleRequest?.TransCode}-{cardSaleRequest?.PartnerCode}-PinCodePartnerRequest có lỗi: {e}",
                Module = "Gateway",
                MessageType = BotMessageType.Error,
                Title = "PinCodePartnerRequest error",
                BotType = BotType.Dev,
                TimeStamp = DateTime.Now,
                CorrelationId = Guid.NewGuid()
            });
            return new NewMessageReponseBase<string>
            {
                ResponseStatus =
                    new ResponseStatusApi(ResponseCodeConst.ResponseCode_WaitForResult,
                        "Giao dịch chưa có kết quả. Vui lòng liên hệ CSKH để được hỗ trợ")
            };
        }
    }

    [Authenticate]
    [PartnerFilter]
    public async Task<object> PostAsync(PayBillPartnerRequest payBill)
    {
        try
        {
            if (payBill == null)
                return new NewMessageReponseBase<string>
                {
                    ResponseStatus =
                        new ResponseStatusApi(ResponseCodeConst.ResponseCode_00, "Yêu cầu không hợp lệ")
                };
            _logger.LogInformation("PayBillPartnerRequest {Request}", payBill.ToJson());
            var response = await _grpcClient.GetClientCluster(GrpcServiceName.Worker).SendAsync(new WorkerPayBillRequest
            {
                Amount = payBill.Amount,
                Channel = Channel.API,
                AgentType = AgentType.AgentApi,
                AccountType = SystemAccountType.MasterAgent,
                CategoryCode = payBill.CategoryCode,
                ProductCode = payBill.ProductCode,
                PartnerCode = payBill.PartnerCode,
                ReceiverInfo = payBill.ReceiverInfo,
                RequestIp = Request.UserHostAddress,
                ServiceCode = ServiceCodes.PAY_BILL,
                StaffAccount = payBill.PartnerCode,
                TransCode = payBill.TransCode,
                RequestDate = DateTime.Now,
                ExtraInfo = !string.IsNullOrEmpty(payBill.ExtraInfo)
                    ? payBill.ExtraInfo
                    : new InvoiceDto
                    {
                        Address = "",
                        Period = "",
                        Email = "",
                        FullName = "",
                        CustomerReference = ""
                    }.ToJson()
            });
            _logger.LogInformation("{TransRef}: PayBillPartnerRequestReturn: {Response}", payBill.TransCode,
                response.ToJson());

            return new NewMessageReponseBase<string>
            {
                ResponseStatus =
                    new ResponseStatusApi(response.ResponseStatus.ErrorCode, response.ResponseStatus.Message)
                    {
                        TransCode = payBill.TransCode
                    }
            };
        }
        catch (Exception e)
        {
            _logger.LogError(
                $"PayBillPartnerRequest exception: {payBill?.TransCode}-{payBill?.ReceiverInfo}-Error:{e}");
            await _bus.Publish<SendBotMessage>(new
            {
                Message = $"{payBill?.TransCode}-{payBill?.PartnerCode}-PayBillPartnerRequest có lỗi: {e}",
                Module = "Gateway",
                MessageType = BotMessageType.Error,
                Title = "PayBillPartnerRequest error",
                BotType = BotType.Dev,
                TimeStamp = DateTime.Now,
                CorrelationId = Guid.NewGuid()
            });
            return new NewMessageReponseBase<string>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_WaitForResult,
                    "Giao dịch chưa có kết quả. Vui lòng liên hệ CSKH để được hỗ trợ")
                {
                    TransCode = payBill?.TransCode
                }
            };
        }
    }

    [Authenticate]
    [PartnerFilter]
    public async Task<object> Get(BillQueryPartnerRequest billQuery)
    {
        try
        {
            if (billQuery == null)
                return new NewMessageReponseBase<object>
                {
                    ResponseStatus =
                        new ResponseStatusApi(ResponseCodeConst.ResponseCode_00, "Yêu cầu không hợp lệ")
                };
            _logger.LogInformation("BillQueryPartnerRequest {Request}", billQuery.ToJson());
            var rs = new NewMessageReponseBase<InvoiceResultDto>
            {
                ResponseStatus = new ResponseStatusApi
                {
                    TransCode = billQuery.TransCode
                }
            };
            var response = await _grpcClient.GetClientCluster(GrpcServiceName.Worker).SendAsync(
                new WorkerBillQueryRequest
                {
                    CategoryCode = billQuery.CategoryCode,
                    PartnerCode = billQuery.PartnerCode,
                    ProductCode = billQuery.ProductCode,
                    ReceiverInfo = billQuery.ReceiverInfo,
                    TransCode = billQuery.TransCode,
                    ServiceCode = ServiceCodes.QUERY_BILL,
                    IsInvoice = true,
                    RequestDate = DateTime.Now
                });
            rs.ResponseStatus = response.ResponseStatus;
            rs.ResponseStatus.TransCode = billQuery.TransCode;
            if (response.ResponseStatus.ErrorCode != ResponseCodeConst.Success) return rs;
            rs.Results = response.Results;
            rs.Results.BillId = null;
            rs.Results.BillType = null;
            _logger.LogInformation("BillQueryPartnerRequestReturn return {Response}", rs.ToJson());
            return rs;
        }
        catch (Exception e)
        {
            _logger.LogError("{TransRef}: BillQueryPartnerRequest exception: {Exception}",
                billQuery?.TransCode, e);
            return new NewMessageReponseBase<InvoiceResponseDto>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_BillException,
                    "Truy vấn thông tin không thành công. Vui lòng thử lại sau")
                {
                    TransCode = billQuery?.TransCode
                }
            };
        }
    }
}