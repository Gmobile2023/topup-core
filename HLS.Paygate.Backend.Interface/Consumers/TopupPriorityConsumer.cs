using System;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using NLog;
using ServiceStack;
using HLS.Paygate.Gw.Domain.Entities;
using HLS.Paygate.Gw.Domain.Services;
using HLS.Paygate.Gw.Model.Commands;
using HLS.Paygate.Gw.Model.Dtos;
using HLS.Paygate.Shared;

namespace HLS.Paygate.Backend.Interface.Consumers
{
    public class TopupPriorityConsumer : IConsumer<TopupPriorityCommand>
    {
        private readonly IConfiguration _configuration;
        private readonly Logger _logger = LogManager.GetLogger("TopupPriorityConsumer");
        private readonly ISaleService _saleService;
        readonly IRequestClient<PaymentPriorityCommand> _requestClient;

        public TopupPriorityConsumer(ISaleService saleService, IConfiguration configuration,
            IRequestClient<PaymentPriorityCommand> requestClient)
        {
            _saleService = saleService;
            _configuration = configuration;
            _requestClient = requestClient;
        }

        public async Task Consume(ConsumeContext<TopupPriorityCommand> context)
        {
            //Đoạn này xem lại có thể xử lý ở GW luôn. (Từ gw send message sang balance luôn). K cần phải send message qua backend
            _logger.LogInformation("TopupPriorityConsumer comming request: " + BsonExtensionMethods.ToJson(context.Message));
            var response = new MessageResponseBase();
            var request = context.Message;
            var checkRequest =
                await _saleService.SaleRequestPriorityAsync(request.TransRef, request.PartnerCode,
                    request.DiscountPriority);
            if (checkRequest.ResponseCode != "01")
            {
                await context.RespondAsync<MessageResponseBase>(new
                {
                    Id = context.Message.CorrelationId,
                    ReceiveTime = DateTime.Now,
                    checkRequest.ResponseCode,
                    checkRequest.ResponseMessage
                });
            }
            else
            {
                //payment
                var topupItem = (SaleRequest) checkRequest.Payload;
                var paymentResponse = await _requestClient.GetResponse<MessageResponseBase>(new
                {
                    context.Message.CorrelationId,
                    topupItem.TransCode,
                    topupItem.TransRef,
                    topupItem.PaymentTransCode,
                    Amount = topupItem.PriorityFee,
                    TransNote = $"Phí ưu tiên cho giao dịch: {topupItem.TransRef}",
                    AccountCode = topupItem.PartnerCode
                });
                if (paymentResponse.Message.ResponseCode == "01")
                {
                    response.ResponseMessage = "Thành công";
                    response.ResponseCode = "01";
                    await _saleService.SaleRequestUpdateAsync(topupItem.ConvertTo<SaleRequestDto>());
                }
                else
                {
                    response.ResponseMessage = "Giao dịch ưu tiên không thành công";
                    response.ResponseCode = "00";
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
}
