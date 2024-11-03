using System;
using System.Threading.Tasks;
using HLS.Paygate.Backend.Interface.Connectors;
using MassTransit;
using MassTransit.Definition;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using NLog;
using HLS.Paygate.Gw.Domain.Services;
using HLS.Paygate.Gw.Model.Commands;
using HLS.Paygate.Gw.Model.Enums;
using HLS.Paygate.Shared;

namespace HLS.Paygate.Backend.Interface.Consumers
{
    public class TopupRequestCommandConsumer : IConsumer<TopupRequestCommand>
    {
        readonly IRequestClient<PaymentProcessCommand> _requestClient;
        private readonly IConfiguration _configuration;
        private readonly Logger _logger = LogManager.GetLogger("TopupRequestConsumer");
        private readonly ISaleService _saleService;
        private readonly ExternalServiceConnector _externalServiceConnector;

        public TopupRequestCommandConsumer(ISaleService saleService, IConfiguration configuration,
            ExternalServiceConnector externalServiceConnector, IRequestClient<PaymentProcessCommand> requestClient)
        {
            _saleService = saleService;
            _configuration = configuration;
            _externalServiceConnector = externalServiceConnector;
            _requestClient = requestClient;
        }

        public async Task Consume(ConsumeContext<TopupRequestCommand> context)
        {
            _logger.LogInformation("Topup request is comming request: " + context.Message.SaleRequest.ToJson());

            var saleRequestCommand = context.Message;
            var checkExist = await
                _saleService.SaleRequestCheckAsync(saleRequestCommand.SaleRequest.TransRef,
                    saleRequestCommand.SaleRequest.PartnerCode);

            var response = new MessageResponseBase();
            if (checkExist.ResponseCode != "07")
            {
                response.ResponseMessage =
                    $"Giao dịch của tài khoản: {saleRequestCommand.SaleRequest.PartnerCode} có mã giao dịch: {saleRequestCommand.SaleRequest.TransRef} đã tồn tại";
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
                var saleRequest = await _saleService.SaleRequestCreateAsync(context.Message.SaleRequest);
                if (null != saleRequest)
                {
                    _logger.LogInformation($"Create topup request success: {saleRequest.TransCode}-{saleRequest.TransRef}");
                    response.ResponseMessage = "Tiếp nhận giao dịch thành công";
                    response.ResponseCode = ResponseCodeConst.ResponseCode_TopupReceived;

                    #region Payment

                    if (string.IsNullOrEmpty(saleRequest.PartnerCode))
                        throw new ArgumentNullException(nameof(saleRequest.PartnerCode));

                    if (string.IsNullOrEmpty(saleRequest.CurrencyCode))
                        //throw new ArgumentNullException(nameof(payment.CurrencyCode));
                        saleRequest.CurrencyCode = CurrencyCode.VND.ToString("G");

                    if (saleRequest.Amount <= 0)
                        throw new ArgumentOutOfRangeException(nameof(saleRequest.Amount));

                    decimal paymentAmount = saleRequest.Amount;
                    var discountObject = await
                        _externalServiceConnector.DiscountPolicyGetAsync(saleRequest.PartnerCode,
                            saleRequest.CategoryCode,
                            saleRequest.Amount);

                    if (discountObject != null)
                    {
                        paymentAmount = saleRequest.Amount -
                                        saleRequest.Amount * discountObject.DiscountValue / 100;
                        saleRequest.DiscountRate = discountObject.DiscountValue; //gán discount lại
                    }

                    saleRequest.PaymentAmount = paymentAmount; //gán số tiền payment lại
                    var paymentResponse = await _requestClient.GetResponse<MessageResponseBase>(new
                    {
                        context.CorrelationId,
                        AccountCode = saleRequest.PartnerCode,
                        PaymentAmount = paymentAmount,
                        saleRequest.CurrencyCode,
                        saleRequest.TransRef,
                        saleRequest.ServiceCode,
                        saleRequest.CategoryCode,
                        TransNote = $"Thanh toán cho giao dịch: {saleRequest.TransRef}"
                    });

                    #endregion

                    _logger.LogInformation(
                        $"Paymeny topup request return: {paymentResponse.Message.ResponseCode}-{paymentResponse.Message.ResponseMessage} {saleRequest.TransCode}-{saleRequest.TransRef}");
                    if (paymentResponse.Message.ResponseCode == "01")
                    {
                        saleRequest.Status = TopupStatus.Paid;
                        saleRequest.PaymentTransCode = paymentResponse.Message.ResponseMessage;
                        var topupUpdate = await _saleService.SaleRequestUpdateAsync(saleRequest);
                        if (topupUpdate != null)
                        {
                            _logger.LogInformation(
                                $"Update topup item payment success: {topupUpdate.TransCode}-{topupUpdate.TransRef}-{topupUpdate.Status}");
                            await context.Publish<TopupFulfillRequestCommand>(new
                            {
                                TopupRequest = saleRequest
                            });
                        }
                        else
                        {
                            _logger.LogInformation(
                                $"Update topup item payment faild: {saleRequest.TransCode}-{saleRequest.TransRef}-{saleRequest.Status}");
                        }
                    }
                    else
                    {
                        _logger.LogInformation(
                            $"Payment fail. {saleRequest.TransCode}-{saleRequest.TransRef} - {paymentResponse.Message.ResponseCode}-{paymentResponse.Message.ResponseMessage}");
                        await _saleService.SaleRequestUpdateStatusAsync(saleRequest.TransCode, TopupStatus.Failed);
                        response.ResponseMessage = "Thanh toán cho giao dịch lỗi. Vui lòng kiểm tra lại số dư";
                        response.ResponseCode = ResponseCodeConst.ResponseCode_Balance_Not_Enough;
                    }
                }
                else
                {
                    response.ResponseMessage = "Khởi tạo giao dịch lỗi";
                    response.ResponseCode = ResponseCodeConst.ResponseCode_00;
                }

                await context.RespondAsync<MessageResponseBase>(new
                {
                    Id = context.Message.CorrelationId,
                    ReceiveTime = DateTime.Now,
                    response.ResponseCode,
                    response.ResponseMessage
                });
            }
        }
    }

    public partial class TopupConsumerDefinition :
        ConsumerDefinition<TopupRequestCommandConsumer>
    {
        public TopupConsumerDefinition()
        {
            ConcurrentMessageLimit = 10;
        }
    }
}
