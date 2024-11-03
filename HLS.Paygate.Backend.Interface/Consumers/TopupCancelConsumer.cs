using System;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Configuration;
using NLog;
using HLS.Paygate.Gw.Domain.Entities;
using HLS.Paygate.Gw.Domain.Services;
using HLS.Paygate.Gw.Model.Commands;
using HLS.Paygate.Gw.Model.Dtos;
using HLS.Paygate.Gw.Model.Enums;
using HLS.Paygate.Shared;
using ServiceStack;

namespace HLS.Paygate.Backend.Interface.Consumers
{
    public class TopupCancelConsumer : IConsumer<TopupCancelCommand>
    {
        private readonly IConfiguration _configuration;
        private readonly Logger _logger = LogManager.GetLogger("TopupCancelConsumer");
        private readonly ISaleService _saleService;

        public TopupCancelConsumer(ISaleService saleService, IConfiguration configuration)
        {
            _saleService = saleService;
            _configuration = configuration;
        }

        public async Task Consume(ConsumeContext<TopupCancelCommand> context)
        {
            try
            {
                _logger.LogInformation("TopupCancelConsumer comming request: " + context.Message.ToJson());

                var request = context.Message;
                var response = await _saleService.SaleRequestCancelAsync(request.TransRef, request.PartnerCode);
                _logger.LogInformation(
                    $"TopupCancelConsumer update request done: {response.ResponseCode} - {response.ResponseMessage}" +
                    context.Message.TransRef);

                await context.RespondAsync<MessageResponseBase>(new
                {
                    Id = context.Message.CorrelationId,
                    ReceiveTime = DateTime.Now,
                    response.ResponseCode,
                    response.ResponseMessage
                });
                //Đoạn này xem lại xem có nên đợi kết quả revert không?
                if (response.ResponseCode == "01")
                {
                    _logger.LogInformation("TopupCancelConsumer begin update balance " + context.Message.TransRef);
                    var saleRequest = (SaleRequest) response.Payload;
                    if (saleRequest.RevertAmount <= 0 && !string.IsNullOrEmpty(saleRequest.PaymentTransCode) &&
                        saleRequest.PaymentAmount > 0)
                    {
                        _logger.LogInformation("TopupCancelConsumer process update balance " + context.Message.TransRef);
                        var revertAmount = saleRequest.PaymentAmount;
                        saleRequest.RevertAmount = revertAmount;
                        saleRequest.Status = TopupStatus.Canceled;
                        await _saleService.SaleRequestUpdateAsync(saleRequest.ConvertTo<SaleRequestDto>());

                        await context.Publish<PaymentCancelCommand>(new
                        {
                            context.CorrelationId,
                            saleRequest.TransCode,
                            saleRequest.TransRef,
                            saleRequest.PaymentTransCode,
                            TransNote = $"Hoàn tiền cho giao dịch thanh toán: {saleRequest.TransRef}",
                            RevertAmount = revertAmount,
                            AccountCode = saleRequest.PartnerCode
                        });
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogInformation($"TopupCancelConsumer error: {e}-{context.Message.TransRef}");
            }
        }
    }
}
