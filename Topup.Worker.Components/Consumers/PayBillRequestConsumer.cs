using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HLS.Paygate.Gw.Domain.Services;
using HLS.Paygate.Gw.Model.Commands;
using HLS.Paygate.Gw.Model.Commands.TopupGw;
using HLS.Paygate.Gw.Model.Dtos;
using HLS.Paygate.Gw.Model.Enums;
using HLS.Paygate.Gw.Model.Events;
using HLS.Paygate.Shared;
using HLS.Paygate.Shared.AbpConnector;
using HLS.Paygate.Shared.Dtos;
using HLS.Paygate.Worker.Components.Connectors;
using MassTransit;
using Microsoft.Extensions.Logging;
using NLog;
using Paygate.Discovery.Requests.Balance;
using ServiceStack;
using TopupCommand = HLS.Paygate.Gw.Model.Commands.TopupGw.TopupCommand;

namespace HLS.Paygate.Worker.Components.Consumers
{
    public class PayBillRequestConsumer : IConsumer<PayBillRequestCommand>
    {
        //readonly IRequestClient<PaymentProcessCommand> _paymentClient;
        private readonly IRequestClient<TopupCommand> _topupClient;
        private readonly CheckLimitTransaction _checkLimit;

        //private readonly Logger _logger = LogManager.GetLogger("PayBillRequestConsumer");
        private readonly ILogger<PayBillRequestConsumer> _logger;
        private readonly ISaleService _saleService;
        private readonly ExternalServiceConnector _externalServiceConnector;
        private readonly ILimitTransAccountService _limitTransAccountService;
        private readonly IRequestClient<PayBillCommand> _payBillClient;
        //private readonly IRequestClient<BillQueryCommand> _billQueryClient;
        private readonly IServiceGateway _gateway;

        public PayBillRequestConsumer(ISaleService saleService,
            ExternalServiceConnector externalServiceConnector,
            //IRequestClient<PaymentProcessCommand> paymentClient,
            IRequestClient<PayBillCommand> payBillClient,
            //IRequestClient<BillQueryCommand> billQueryClient,
            IRequestClient<TopupCommand> topupClient, ILimitTransAccountService limitTransAccountService,
            ILogger<PayBillRequestConsumer> logger, CheckLimitTransaction checkLimit)
        {
            _saleService = saleService;
            _externalServiceConnector = externalServiceConnector;
            //_paymentClient = paymentClient;
            _payBillClient = payBillClient;
            //_billQueryClient = billQueryClient;
            _topupClient = topupClient;
            _limitTransAccountService = limitTransAccountService;
            _logger = logger;
            _checkLimit = checkLimit;
            _gateway = HostContext.AppHost.GetServiceGateway();
        }

        public async Task Consume(ConsumeContext<PayBillRequestCommand> context)
        {
            var saleRequest = context.Message.SaleRequest;
            _logger.LogInformation("PayBill request is comming: " + saleRequest.ToJson());

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
                if (saleRequest.Amount <= 0)
                    throw new ArgumentOutOfRangeException(nameof(saleRequest.Amount));
                //Check phi giao dich
                var fee = await _externalServiceConnector.GetProductFee(saleRequest.PartnerCode,
                    saleRequest.ProductCode, saleRequest.Amount);
                if (fee == null)
                {
                    _logger.LogInformation("Thông tin sản phẩm + Phí không hợp lệ");
                    throw new Exception("Thông tin sản phẩm + Phí không hợp lệ");
                }
                _logger.LogInformation($"GetFee:{saleRequest.TransCode}-{saleRequest.TransRef}-{fee.ToJson()}");
                var discount = await
                    _externalServiceConnector.CheckProductDiscount(saleRequest.PartnerCode,
                        context.Message.SaleRequest.ProductCode,saleRequest.ReceiverInfo, saleRequest.Amount, 1);
                if (discount == null)
                {
                    _logger.LogInformation("Thông tin sản phẩm không tồn tại + Chiết khấu không hợp lệ");
                    throw new Exception("Thông tin sản phẩm không tồn tại");
                }
                _logger.LogInformation($"GetDiscount:{saleRequest.TransCode}-{saleRequest.TransRef}-{discount.ToJson()}");

                saleRequest.PaymentAmount = discount.PaymentAmount;
                if (saleRequest.PaymentAmount <= 0)
                    throw new ArgumentOutOfRangeException(nameof(context.Message.SaleRequest.Amount));
                if (saleRequest.AgentType != AgentType.AgentApi)
                {
                    //check hạn mức
                    var checkLimit = await
                        _limitTransAccountService.CheckLimitAccount(saleRequest.StaffAccount,
                            saleRequest.PaymentAmount + fee.FeeValue);
                    _logger.LogInformation($"{saleRequest.TransCode}-{saleRequest.TransRef}-CheckLimit return:{checkLimit.ToJson()}");

                    if (checkLimit.ResponseCode != "01")
                    {
                        _logger.LogInformation(checkLimit.ResponseMessage);
                        await context.RespondAsync<MessageResponseBase>(new
                        {
                            Id = context.Message.CorrelationId,
                            ReceiveTime = DateTime.Now,
                            checkLimit.ResponseCode,
                            checkLimit.ResponseMessage
                        });
                        return;
                    }

                    var checkLimitProduct = await
                        _checkLimit.CheckLimitProductPerDay(saleRequest.PartnerCode, saleRequest.ProductCode,
                            saleRequest.Amount, saleRequest.Quantity);
                    _logger.LogInformation($"{saleRequest.TransCode}-{saleRequest.TransRef}-CheckLimitProductPerDay return:{checkLimitProduct.ToJson()}");
                    if (checkLimitProduct.ResponseCode != "01")
                    {
                        await context.RespondAsync<MessageResponseBase>(new
                        {
                            Id = context.Message.CorrelationId,
                            ReceiveTime = DateTime.Now,
                            checkLimitProduct.ResponseCode,
                            checkLimitProduct.ResponseMessage
                        });
                        return;
                    }
                }

                var serviceConfiguration = await _externalServiceConnector.ServiceConfigurationAsync(
                    saleRequest.PartnerCode,
                    saleRequest.ServiceCode,
                    saleRequest.CategoryCode, saleRequest.ProductCode,saleRequest.Channel == Channel.API);
                _logger.LogInformation($"{saleRequest.TransCode}-{saleRequest.TransRef}-ServiceConfiguration:{serviceConfiguration.ToJson()}");
                if (serviceConfiguration != null && serviceConfiguration.Count > 0)
                {
                    var serviceConfig = serviceConfiguration.OrderBy(c => c.Priority).First();
                    saleRequest.DiscountRate = discount.DiscountValue;
                    saleRequest.FixAmount = discount.FixAmount;
                    saleRequest.DiscountAmount = discount?.DiscountAmount;
                    saleRequest.Fee = fee.FeeValue;
                    saleRequest = await _saleService.SaleRequestCreateAsync(saleRequest);
                    if (null != saleRequest)
                    {
                        saleRequest.PaymentAmount += fee.FeeValue; //Thanh toans thanh cong thi cap nhap  paymentamount
                        _logger.LogInformation(
                            $"Create paybill request success: {saleRequest.TransCode}-{saleRequest.TransRef}");
                        response.ResponseMessage = "Tiếp nhận giao dịch thành công";
                        response.ResponseCode = ResponseCodeConst.ResponseCode_RequestReceived;
                        saleRequest.Provider = serviceConfig.ProviderCode;


                        #region Payment

                        if (string.IsNullOrEmpty(saleRequest.PartnerCode))
                            throw new ArgumentNullException(nameof(saleRequest.PartnerCode));

                        if (string.IsNullOrEmpty(saleRequest.CurrencyCode))
                            saleRequest.CurrencyCode = CurrencyCode.VND.ToString("G");

                        // var paymentResponse = await _paymentClient.GetResponse<MessageResponseBase>(new
                        // {
                        //     context.CorrelationId,
                        //     AccountCode = saleRequest.PartnerCode,
                        //     saleRequest.PaymentAmount,
                        //     saleRequest.CurrencyCode,
                        //     saleRequest.TransRef,
                        //     saleRequest.ServiceCode,
                        //     saleRequest.CategoryCode,
                        //     TransNote = $"Thanh toán cho giao dịch: {saleRequest.TransRef}"
                        // });

                        var paymentResponse = await _gateway.SendAsync(new BalancePaymentRequest()
                        {
                            AccountCode = saleRequest.PartnerCode,
                            PaymentAmount = saleRequest.PaymentAmount,
                            CurrencyCode = saleRequest.CurrencyCode,
                            TransRef = saleRequest.TransRef,
                            TransCode = saleRequest.TransCode,
                            TransNote = $"Thanh toán cho giao dịch: {saleRequest.TransRef}"
                        });

                        #endregion

                        _logger.LogInformation(
                            $"Payment for bill request return: {paymentResponse.ResponseCode}-{paymentResponse.ResponseMessage} {saleRequest.TransCode}-{saleRequest.TransRef}");

                        if (paymentResponse.ResponseCode == "01")
                        {
                            saleRequest.Status = SaleRequestStatus.Paid;
                            saleRequest.PaymentTransCode = paymentResponse.ResponseMessage;
                            string transCodeProvider = string.Empty;
                            string providerCode = string.Empty;
                            var topupUpdate = await _saleService.SaleRequestUpdateAsync(saleRequest);

                            if (topupUpdate != null)
                            {
                                await _saleService.PublishConsumerReport(new SaleReponseDto()
                                {
                                    NextStep = 0,
                                    Sale = topupUpdate,
                                    Status = SaleRequestStatus.Paid,
                                    Balance = Convert.ToDecimal(
                                        paymentResponse.Payload.ToString().Split('|')[0]),
                                    FeeDto = fee.ToJson(),
                                });

                                var providerTwo =
                                    (from x in serviceConfiguration.Where(c =>
                                            c.ProviderCode != serviceConfig.ProviderCode)
                                     select new ProviderConfig
                                     {
                                         ProviderCode = x.ProviderCode,
                                         Priority = x.Priority,
                                     }).ToList();

                                if (saleRequest.ProductCode.StartsWith("VMS") ||
                                    saleRequest.ProductCode.StartsWith("VTE") ||
                                    saleRequest.ProductCode.StartsWith("VNA"))
                                {
                                    var topupResult = await _topupClient.GetResponse<MessageResponseBase>(new
                                    {
                                        saleRequest.ServiceCode,
                                        saleRequest.CategoryCode,
                                        saleRequest.Amount,
                                        saleRequest.ReceiverInfo,
                                        saleRequest.ProductCode,
                                        TransRef = saleRequest.TransCode,
                                        ProviderCode = serviceConfig.ProviderCode,
                                        ProviderCodes = providerTwo,
                                        Vendor = saleRequest.ProductCode.Split('_')[0],
                                        PartnerCode = saleRequest.PartnerCode,
                                        ReferenceCode = saleRequest.TransRef,
                                        TransCodeProvider = saleRequest.ProviderTransCode,
                                        RequestDate = saleRequest.CreatedTime
                                    }, CancellationToken.None, RequestTimeout.After(m: 5));

                                    response = topupResult.Message;
                                    providerCode = response.ProviderCode;
                                    transCodeProvider = response.TransCodeProvider;
                                    saleRequest.Provider = providerCode;
                                    if (!string.IsNullOrEmpty(transCodeProvider))
                                        saleRequest.ProviderTransCode = transCodeProvider;
                                }
                                else
                                {
                                    var payBillResult = await _payBillClient.GetResponse<MessageResponseBase>(
                                        new
                                        {
                                            saleRequest.ServiceCode,
                                            saleRequest.CategoryCode,
                                            saleRequest.Amount,
                                            saleRequest.ReceiverInfo,
                                            TransRef = saleRequest.TransCode,
                                            serviceConfig.ProviderCode,
                                            ProviderCodes = providerTwo,
                                            RequestDate = saleRequest.CreatedTime,
                                            ProductCode = saleRequest.ProductCode,
                                            PartnerCode = saleRequest.PartnerCode,
                                            ReferenceCode = saleRequest.TransRef,
                                            TransCodeProvider = saleRequest.ProviderTransCode,
                                            context.Message.IsInvoice
                                        }, CancellationToken.None, RequestTimeout.After(m: 5));

                                    response = payBillResult.Message;
                                    providerCode = response.ProviderCode;
                                    transCodeProvider = response.TransCodeProvider;
                                    if (!string.IsNullOrEmpty(transCodeProvider))
                                        saleRequest.ProviderTransCode = transCodeProvider;
                                }

                                _logger.LogInformation($"{saleRequest.TransCode}-{saleRequest.TransRef}-Call topupgate response:{response.ToJson()}");

                                if (response.ResponseCode == ResponseCodeConst.Success)
                                {
                                    response.ResponseMessage =
                                        $"Thanh toán cho hóa đơn {saleRequest.ReceiverInfo} thành công";

                                    saleRequest.Status = SaleRequestStatus.Success;
                                    await _saleService.SaleRequestUpdateStatusAsync(saleRequest.TransCode,
                                        saleRequest.Provider, SaleRequestStatus.Success, transCodeProvider);
                                }
                                else if (response.ResponseCode == ResponseCodeConst.ResponseCode_WaitForResult
                                         || response.ResponseCode == ResponseCodeConst.ResponseCode_TimeOut
                                         || response.ResponseCode == ResponseCodeConst.ResponseCode_InProcessing)
                                {
                                    response.ResponseCode = response.ResponseCode;
                                    response.ResponseMessage =
                                        "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ. Vui lòng liên hệ CSKH để biết thêm chi tiết";
                                    saleRequest.Status = SaleRequestStatus.WaitForResult;
                                    await _saleService.SaleRequestUpdateStatusAsync(saleRequest.TransCode,
                                        providerCode, SaleRequestStatus.WaitForResult, transCodeProvider);
                                }
                                else
                                {
                                    _logger.LogInformation(
                                        $"Thanh toán cho giao dịch :{saleRequest.TransCode}-{saleRequest.TransRef} không thành công.=> Hoàn tiền");
                                    await context.Publish<PaymentCancelCommand>(new
                                    {
                                        context.Message.CorrelationId,
                                        saleRequest.TransCode,
                                        saleRequest.PaymentTransCode,
                                        TransNote =
                                            $"Hoàn tiền cho giao dịch thanh toán: {saleRequest.TransRef}",
                                        RevertAmount = saleRequest.PaymentAmount,
                                        AccountCode = saleRequest.PartnerCode
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
                                    $"Update topup item payment faild: {saleRequest.TransCode}-{saleRequest.TransRef}-{saleRequest.Status}");

                                await _saleService.PublishConsumerReport(new SaleReponseDto()
                                {
                                    NextStep = 1,
                                    Sale = saleRequest,
                                    Status = SaleRequestStatus.Failed,
                                });

                                await context.Publish<PaymentCancelCommand>(new
                                {
                                    context.Message.CorrelationId,
                                    saleRequest.TransCode,
                                    saleRequest.PaymentTransCode,
                                    TransNote = $"Hoàn tiền cho giao dịch thanh toán: {saleRequest.TransRef}",
                                    RevertAmount = saleRequest.PaymentAmount,
                                    AccountCode = saleRequest.PartnerCode
                                });
                            }
                        }
                        else
                        {
                            _logger.LogInformation(
                                $"Payment fail. {saleRequest.TransCode}-{saleRequest.TransRef} - {paymentResponse.ResponseCode}-{paymentResponse.ResponseMessage}");
                            await _saleService.SaleRequestUpdateStatusAsync(saleRequest.TransCode,
                                string.Empty, SaleRequestStatus.Failed);
                            response.ResponseMessage =
                                "Thanh toán cho giao dịch lỗi. Vui lòng kiểm tra lại số dư";
                            response.ResponseCode = ResponseCodeConst.ResponseCode_Balance_Not_Enough;

                            await _saleService.PublishConsumerReport(new SaleReponseDto()
                            {
                                NextStep = 0,
                                Sale = saleRequest,
                                Status = SaleRequestStatus.Failed,
                            });
                        }
                    }
                    else
                    {
                        response.ResponseMessage = "Khởi tạo giao dịch lỗi";
                        response.ResponseCode = ResponseCodeConst.ResponseCode_00;
                    }
                }
                else
                {
                    _logger.LogInformation("Paybill can not get service config");
                    response.ResponseMessage = "Giao dịch lỗi. Không tìm thấy kênh giao dịch";
                    response.ResponseCode = ResponseCodeConst.ResponseCode_00;
                }


                await context.RespondAsync<MessageResponseBase>(new
                {
                    response.ResponseCode,
                    response.ResponseMessage,
                });
            }
        }
    }
}
