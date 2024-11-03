using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HLS.Paygate.Gw.Domain.Services;
using HLS.Paygate.Gw.Model.Commands;
using HLS.Paygate.Gw.Model.Commands.Stock;
using HLS.Paygate.Gw.Model.Dtos;
using HLS.Paygate.Gw.Model.Events.Stock;
using HLS.Paygate.Shared;
using HLS.Paygate.Shared.AbpConnector;
using HLS.Paygate.Shared.Dtos;
using HLS.Paygate.Shared.Utils;
using HLS.Paygate.TopupGw.Contacts.Dtos;
using HLS.Paygate.Worker.Components.Connectors;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Paygate.Discovery.Requests.Balance;
using Paygate.Discovery.Requests.TopupGateways;
using ServiceStack;

namespace HLS.Paygate.Worker.Components.Consumers
{
    public class CardSaleConsumer : IConsumer<CardSaleRequestCommand>
    {
        private readonly ILogger<CardSaleConsumer> _logger;
        private readonly ISaleService _saleService;
        private readonly IServiceGateway _gateway;
        private readonly ExternalServiceConnector _externalServiceConnector;
        private readonly ILimitTransAccountService _limitTransAccountService;
        private readonly CheckLimitTransaction _checkLimit;

        public CardSaleConsumer(ISaleService saleService,
            ExternalServiceConnector externalServiceConnector,
            ILimitTransAccountService limitTransAccountService, ILogger<CardSaleConsumer> logger,
            CheckLimitTransaction checkLimit)
        {
            _saleService = saleService;
            _externalServiceConnector = externalServiceConnector;
            _limitTransAccountService = limitTransAccountService;
            _logger = logger;
            _checkLimit = checkLimit;
            _gateway = HostContext.AppHost.GetServiceGateway();
        }

        public async Task Consume(ConsumeContext<CardSaleRequestCommand> context)
        {
            try
            {
                //TODO (Namnl 8/9/2020): Chỗ này thêm check timeout
                var saleRequest = context.Message.SaleRequest;
                _logger.LogInformation("CardSaleRequestConsumer is comming request: " + saleRequest.ToJson());

                var checkExist = await
                    _saleService.SaleRequestCheckAsync(saleRequest.TransRef,
                        saleRequest.PartnerCode);
                _logger.LogInformation($"CheckSaleRequestDone:{saleRequest.TransCode}-{saleRequest.TransRef}");

                var response = new MessageResponseBase();
                if (checkExist.ResponseCode != ResponseCodeConst.ResponseCode_TransactionNotFound)
                {
                    response.ResponseMessage =
                        $"Giao dịch của tài khoản: {saleRequest.PartnerCode} có mã giao dịch: {saleRequest.TransRef} đã tồn tại";
                    response.ResponseCode = ResponseCodeConst.ResponseCode_RequestAlreadyExists;
                    await context.RespondAsync<MessageResponseBase>(new
                    {
                        Id = context.Message.CorrelationId,
                        ReceiveTime = DateTime.Now,
                        response.ResponseCode,
                        response.ResponseMessage
                    });
                }
                else
                {
                    var discount = await
                        _externalServiceConnector.CheckProductDiscount(saleRequest.PartnerCode, saleRequest.ProductCode, 0, saleRequest.Quantity);

                    if (discount == null || discount.ProductValue <= 0)
                    {
                        throw new Exception("Thông tin sản phẩm không tồn tại");
                    }

                    var paymentAmount = discount.PaymentAmount;

                    if (paymentAmount <= 0)
                        throw new ArgumentOutOfRangeException(nameof(context.Message.SaleRequest.Amount));
                    if (saleRequest.AgentType != AgentType.AgentApi)
                    {
                        //check hạn mức
                        var checkLimit = await
                            _limitTransAccountService.CheckLimitAccount(
                                context.Message.SaleRequest.StaffAccount, paymentAmount);
                        _logger.LogInformation(
                            $"{saleRequest.TransCode}-{saleRequest.TransRef}-CheckLimit return:{checkLimit.ToJson()}");

                        if (checkLimit.ResponseCode != "01")
                        {
                            await context.RespondAsync<MessageResponseBase>(new
                            {
                                Id = context.Message.CorrelationId,
                                ReceiveTime = DateTime.Now,
                                ResponseCode = "00",
                                checkLimit.ResponseMessage
                            });
                            return;
                        }

                        var checkLimitProduct = await
                            _checkLimit.CheckLimitProductPerDay(saleRequest.PartnerCode, saleRequest.ProductCode,
                                discount.ProductValue, saleRequest.Quantity);
                        _logger.LogInformation(
                            $"{saleRequest.TransCode}-{saleRequest.TransRef}-CheckLimitProductPerDay return:{checkLimitProduct.ToJson()}");
                        if (checkLimitProduct.ResponseCode != "01")
                        {
                            await context.RespondAsync<MessageResponseBase>(new
                            {
                                Id = context.Message.CorrelationId,
                                ReceiveTime = DateTime.Now,
                                ResponseCode = "00",
                                checkLimitProduct.ResponseMessage
                            });
                            return;
                        }
                    }

                    var serviceConfiguration = await _externalServiceConnector.ServiceConfigurationAsync(
                      saleRequest.PartnerCode,
                      saleRequest.ServiceCode,
                      saleRequest.CategoryCode, saleRequest.ProductCode, true);

                    if (serviceConfiguration != null && serviceConfiguration.Count > 0)
                    {
                        saleRequest.DiscountRate = discount?.DiscountValue;
                        saleRequest.FixAmount = discount?.FixAmount;
                        saleRequest.DiscountAmount = discount?.DiscountAmount;
                        saleRequest.Amount = discount.ProductValue;
                        var serviceConfig = serviceConfiguration.OrderBy(c => c.Priority).First();
                        saleRequest.Provider = serviceConfig.ProviderCode;
                        saleRequest.ProviderTransCode = serviceConfig.TransCodeConfig;

                        saleRequest = await _saleService.SaleRequestCreateAsync(saleRequest);
                        if (null != saleRequest)
                        {
                            var paymentResponse = await _gateway.SendAsync(new BalancePaymentRequest()
                            {
                                AccountCode = saleRequest.PartnerCode,
                                PaymentAmount = paymentAmount,
                                CurrencyCode = CurrencyCode.VND.ToString("G"),
                                TransRef = saleRequest.TransRef,
                                TransCode = saleRequest.TransCode,
                                TransNote = $"Thanh toán cho giao dịch: {saleRequest.TransRef}"
                            });
                            _logger.LogInformation(
                                $"PaymentResponse:{saleRequest.TransCode}-{saleRequest.TransRef}-{paymentResponse.ToJson()}");
                            if (paymentResponse.ResponseCode == "01")
                            {
                                saleRequest.PaymentAmount = paymentAmount;
                                saleRequest.PaymentTransCode = paymentResponse.ResponseMessage;
                                saleRequest.Status = SaleRequestStatus.Paid;
                                var cardUpdate = await _saleService.SaleRequestUpdateAsync(saleRequest);

                                await _saleService.PublishConsumerReport(new SaleReponseDto()
                                {
                                    NextStep = 0,
                                    Sale = saleRequest,
                                    Status = SaleRequestStatus.Paid,
                                    Balance = Convert.ToDecimal(paymentResponse.Payload.ToString().Split('|')[0]),
                                });

                                //Call api Lấy thẻ trả về kết quả
                                if (cardUpdate != null)
                                {
                                    _logger.LogInformation(
                                        $"Update topup item payment success: {cardUpdate.TransCode}-{cardUpdate.TransRef}-{cardUpdate.Status}");

                                    var providerTwo =
                                        (from x in serviceConfiguration.Where(c =>
                                                c.ProviderCode != serviceConfig.ProviderCode)
                                         select new ProviderConfig
                                         {
                                             ProviderCode = x.ProviderCode,
                                             Priority = x.Priority,
                                             TransCodeConfig = x.TransCodeConfig,
                                         }).ToList();

                                    var requestTopup = new GateCardRequest
                                    {
                                        Quantity = saleRequest.Quantity,
                                        Amount = saleRequest.Amount,
                                        ProductCode = saleRequest.ProductCode,
                                        TransRef = saleRequest.TransCode,
                                        ProviderCode = serviceConfig.ProviderCode,
                                        TransCodeProvider = saleRequest.ProviderTransCode,
                                        Vendor = saleRequest.ProductCode.Split('_')[0],
                                        RequestDate = saleRequest.CreatedTime,
                                        ReferenceCode = saleRequest.TransRef
                                    };

                                    var responseCard = await CallCardPriority(requestTopup, providerTwo);

                                    var providerCode = saleRequest.ProductProvider;// responseCard.Results?.ProviderCode;
                                    var transCodeProvider = saleRequest.ProviderTransCode;// responseCard.Results?.TransCodeProvider;
                                    if (!string.IsNullOrEmpty(providerCode))
                                        saleRequest.Provider = providerCode;
                                    if (!string.IsNullOrEmpty(transCodeProvider))
                                        saleRequest.ProviderTransCode = transCodeProvider;

                                    if (responseCard.ResponseStatus.ErrorCode == ResponseCodeConst.Success)
                                    {
                                        saleRequest.Status = SaleRequestStatus.Success;
                                        await _saleService.SaleRequestUpdateStatusAsync(saleRequest.TransCode,
                                            saleRequest.Provider, SaleRequestStatus.Success, transCodeProvider);
                                        responseCard.ResponseStatus = new ResponseStatusApi(
                                            responseCard.ResponseStatus.ErrorCode,
                                            $"Bạn đã lấy thẻ thành công số tiền {saleRequest.Amount:0}. Mã GD {saleRequest.TransCode}");

                                        try
                                        {
                                            var cards = responseCard.Results.ConvertTo<List<CardRequestResponseDto>>();
                                            var lstTopupItem = (from item in cards
                                                               select new SaleItemDto
                                                               {
                                                                   Amount = Convert.ToInt32(item.CardValue),
                                                                   Serial = item.Serial,
                                                                   CardExpiredDate = Convert.ToDateTime(item.ExpireDate),
                                                                   Status = SaleRequestStatus.Success,
                                                                   Vendor = item.CardValue,
                                                                   CardCode = item.CardCode.EncryptTripDes(),
                                                                   CardValue = Convert.ToInt32(item.CardValue),
                                                                   ServiceCode = saleRequest.ServiceCode,
                                                                   PartnerCode = saleRequest.PartnerCode,
                                                                   SaleType = "PINCODE",
                                                                   SaleTransCode = saleRequest.TransCode,
                                                                   CreatedTime = DateTime.Now
                                                               }).ToList();
                                            await _saleService.SaleItemListCreateAsync(lstTopupItem);
                                        }
                                        catch (Exception cardEx)
                                        {

                                        }

                                    }
                                    else if (responseCard.ResponseStatus.ErrorCode ==
                                             ResponseCodeConst.ResponseCode_WaitForResult
                                             || responseCard.ResponseStatus.ErrorCode ==
                                             ResponseCodeConst.ResponseCode_TimeOut
                                             || responseCard.ResponseStatus.ErrorCode ==
                                             ResponseCodeConst.ResponseCode_InProcessing)
                                    {
                                        responseCard.ResponseStatus = new ResponseStatusApi(
                                            responseCard.ResponseStatus.ErrorCode,
                                            "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ");
                                        saleRequest.Status = SaleRequestStatus.WaitForResult;
                                        _logger.LogWarning(
                                            $"CardPending: {saleRequest.TransCode}-{saleRequest.TransRef}");
                                        await _saleService.SaleRequestUpdateStatusAsync(saleRequest.TransCode,
                                            saleRequest.Provider, SaleRequestStatus.WaitForResult, transCodeProvider);
                                    }
                                    else
                                    {
                                        _logger.LogWarning($"CardRefund: {saleRequest.TransCode}-{saleRequest.TransRef}");
                                        await context.Publish<PaymentCancelCommand>(new
                                        {
                                            context.Message.CorrelationId,
                                            saleRequest.TransCode,
                                            saleRequest.PaymentTransCode,
                                            TransNote = $"Hoàn tiền cho giao dịch thanh toán: {saleRequest.TransRef}",
                                            RevertAmount = saleRequest.PaymentAmount,
                                            AccountCode = saleRequest.PartnerCode,
                                            Timestamp = DateTime.Now
                                        });

                                        saleRequest.Status = SaleRequestStatus.Failed;
                                        await _saleService.SaleRequestUpdateStatusAsync(saleRequest.TransCode,
                                            saleRequest.Provider, SaleRequestStatus.Failed, transCodeProvider);
                                    }

                                    await _saleService.PublishConsumerReport(new SaleReponseDto()
                                    {
                                        NextStep = 1,
                                        Sale = saleRequest,
                                        Status = saleRequest.Status,
                                    });
                                }
                                else
                                {
                                    _logger.LogInformation(
                                        $"Get card not found. Begin refund: {saleRequest.TransCode}-{saleRequest.TransRef}");
                                    await context.RespondAsync<MessageResponseBase>(new
                                    {
                                        Id = context.Message.CorrelationId,
                                        ReceiveTime = DateTime.Now,
                                        ResponseCode = ResponseCodeConst.ResponseCode_CardNotInventory,
                                        ResponseMessage =
                                            "Không lấy được thông tin thẻ. Vui lòng liên hệ CSKH để được hỗ trợ"
                                    });
                                    //Hoàn tiền
                                    await context.Publish<PaymentCancelCommand>(new
                                    {
                                        context.Message.CorrelationId,
                                        saleRequest.TransCode,
                                        saleRequest.PaymentTransCode,
                                        TransNote = $"Hoàn tiền cho giao dịch thanh toán: {saleRequest.TransRef}",
                                        RevertAmount = saleRequest.PaymentAmount,
                                        AccountCode = saleRequest.PartnerCode,
                                        Timestamp = DateTime.Now
                                    });

                                    saleRequest.Status = SaleRequestStatus.Failed;
                                    await _saleService.SaleRequestUpdateStatusAsync(saleRequest.TransCode,
                                        string.Empty,
                                        SaleRequestStatus.Failed);

                                    await _saleService.PublishConsumerReport(new SaleReponseDto()
                                    {
                                        NextStep = 1,
                                        Sale = saleRequest,
                                        Status = SaleRequestStatus.Failed,
                                    });
                                }
                            }
                            else
                            {
                                saleRequest.Status = SaleRequestStatus.Failed;
                                await _saleService.SaleRequestUpdateStatusAsync(saleRequest.TransCode,
                                    string.Empty,
                                    SaleRequestStatus.Failed);
                                await context.RespondAsync<MessageResponseBase>(new
                                {
                                    Id = context.Message.CorrelationId,
                                    ReceiveTime = DateTime.Now,
                                    ResponseCode = "6001", //chỗ này xem lại mã lỗi bên ví
                                    ResponseMessage =
                                        "Không thể thanh toán cho giao dịch. Vui lòng kiểm tra lại số dư"
                                });

                                await _saleService.PublishConsumerReport(new SaleReponseDto()
                                {
                                    NextStep = 1,
                                    Sale = saleRequest,
                                    Status = SaleRequestStatus.Failed,
                                });
                            }
                        }
                        else
                        {
                            response.ResponseMessage = "Khởi tạo giao dịch lỗi";
                            response.ResponseCode = ResponseCodeConst.ResponseCode_00;
                            await context.RespondAsync<MessageResponseBase>(new
                            {
                                Id = context.Message.CorrelationId,
                                ReceiveTime = DateTime.Now,
                                response.ResponseCode,
                                response.ResponseMessage
                            });
                        }
                    }
                    else
                    {
                        await context.RespondAsync<MessageResponseBase>(new
                        {
                            Id = context.Message.CorrelationId,
                            ReceiveTime = DateTime.Now,
                            ResponseCode = ResponseCodeConst.ResponseCode_ServiceConfigNotValid,
                            ResponseMessage = "Giao dịch lỗi. Dịch vụ chưa được cấu hình"
                        });
                    }

                }
            }
            catch (Exception e)
            {
                _logger.LogError("CardSaleRequestConsumer error:" + e);
            }
        }

        private async Task<NewMessageReponseBase<List<CardRequestResponseDto>>> CallCardGate(GateCardRequest request)
        {
            try
            {
                var response = await _gateway.SendAsync(request);
                _logger.LogInformation($"{request.TransCodeProvider}-{request.TransRef}-{request.ProviderCode} CallCardGate Return: {response?.ToJson()}");
                if (response == null)
                {
                    _logger.LogWarning(
                        $"{request.TransCodeProvider}-{request.TransRef}-{request.ProviderCode}-Can not get Response TopupGate");
                    //await SendTeleMessage(new SendTeleTrasactionRequest
                    //{
                    //    BotType = BotType.Dev,
                    //    TransRef = request.TransCodeProvider,
                    //    TransCode = request.TransRef,
                    //    Title = "Giao dịch lỗi - Hàm CallCardGate.Can not get Response TopupGate",
                    //    Message =
                    //        $"GD {request.TransRef}-{request.TransCodeProvider}\nGD chưa được xử lý thành công. Không có response từ TopupGw",
                    //    BotMessageType = BotMessageType.Error
                    //});

                    return new NewMessageReponseBase<List<CardRequestResponseDto>>()
                    {
                        ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_WaitForResult,
                            "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ"),
                    };
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    $"{request.TransCodeProvider}-{request.TransRef}-{request.ProviderCode} CallCardGate Exception: {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
                //await SendTeleMessage(new SendTeleTrasactionRequest
                //{
                //    BotType = BotType.Dev,
                //    TransRef = request.TransCodeProvider,
                //    TransCode = request.TransRef,
                //    Title = $"GD có vấn đề!. Vui lòng kiểm tra trạng thái giao dịch",
                //    Message =
                //        $"Mã GD {request.TransRef}-{request.TransCodeProvider}\nHàm CallCardGate\nLỗi:{ex.Message}",
                //    BotMessageType = BotMessageType.Error
                //});

                return new NewMessageReponseBase<List<CardRequestResponseDto>>()
                {
                    ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_WaitForResult,
                        "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ"),
                };
            }
        }

        private async Task<NewMessageReponseBase<List<CardRequestResponseDto>>> CallCardPriority(GateCardRequest request,
            List<ProviderConfig> providerCodes)
        {
            try
            {
                var result = await CallCardGate(request);
                _logger.LogInformation(
                    $"{request.TransCodeProvider}|{request.TransRef}|{request.ProviderCode} CallCardPriority reponse : {result.ToJson()}");

                if (result?.ResponseStatus.ErrorCode == ResponseCodeConst.Error && providerCodes.Count > 0)
                {
                    foreach (var item in providerCodes.OrderBy(c => c.Priority))
                    {
                        try
                        {
                            var config = item;
                            request.TransCodeProvider = string.IsNullOrEmpty(config.TransCodeConfig)
                                ? request.TransRef
                                : config.TransCodeConfig + "_" + request.TransRef;
                            request.ProviderCode = config.ProviderCode;

                            var update = await _saleService.SaleRequestUpdateStatusAsync(request.TransRef,
                                request.ProviderCode, SaleRequestStatus.InProcessing, request.TransCodeProvider);
                            if (!update)
                                break;
                            result = await CallCardGate(request);
                            _logger.LogInformation(
                                $"{request.TransCodeProvider}-{request.TransRef}-{request.ProviderCode} CallCardPriority Return: {result.ToJson()}");
                            if (result.ResponseStatus.ErrorCode == ResponseCodeConst.Error)
                                continue;
                            break;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(
                                $"{request.TransCodeProvider}-{request.TransRef}-{request.ProviderCode} CallCardPriority Exception: {ex.Message}|{ex.StackTrace}|{ex.InnerException}");

                            //await SendTeleMessage(new SendTeleTrasactionRequest
                            //{
                            //    BotType = BotType.Dev,
                            //    TransRef = request.TransCodeProvider,
                            //    TransCode = request.TransRef,
                            //    Title = $"GD có vấn đề!. Vui lòng kiểm tra trạng thái giao dịch",
                            //    Message =
                            //        $"Mã GD {request.TransRef}-{request.TransCodeProvider}\nHàm CallCardPriority.\nLỗi:{ex.Message}",
                            //    BotMessageType = BotMessageType.Error
                            //});
                            break;
                        }
                    }
                }

                return result;
            }
            catch (Exception e)
            {
                _logger.LogInformation(
                    $"{request.TransCodeProvider}|{request.TransRef}|{request.ProviderCode} CallCardPriority error : {e}");
                //await SendTeleMessage(new SendTeleTrasactionRequest
                //{
                //    BotType = BotType.Dev,
                //    TransRef = request.TransCodeProvider,
                //    TransCode = request.TransRef,
                //    Title = "GD có vấn đề!. Vui lòng kiểm tra trạng thái giao dịch",
                //    Message =
                //        $"Mã GD {request.TransRef}-{request.TransCodeProvider}\nHàm CallCardPriority\nLỗi:{e.Message}",
                //    BotMessageType = BotMessageType.Error
                //});
                return new NewMessageReponseBase<List<CardRequestResponseDto>>
                {
                    ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_WaitForResult,
                        "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ")
                };
            }
        }
    }
}
