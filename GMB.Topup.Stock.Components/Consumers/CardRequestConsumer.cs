using System;
using System.Threading.Tasks;
using HLS.Paygate.Gw.Model.Commands;
using HLS.Paygate.Shared;
using MassTransit;
using NLog;
using HLS.Paygate.Stock.Domains.BusinessServices;
using ServiceStack;

namespace HLS.Paygate.Stock.Components.Consumers
{
    public class CardRequestConsumer : IConsumer<CardRequestCommand>
    {
        private readonly Logger _logger = LogManager.GetLogger("CardRequestConsumer");
        private readonly ICardRequestService _cardRequestService;

        public CardRequestConsumer(ICardRequestService cardRequestService)
        {
            _cardRequestService = cardRequestService;
        }

        public async Task Consume(ConsumeContext<CardRequestCommand> context)
        {
            try
            {
                var response = new MessageResponseBase();
                var cardRequest = context.Message.CardRequest;
                _logger.LogInformation("CardRequestConsumer is comming request: " + context.Message.ToJson());
                var checkExist = await
                    _cardRequestService.CardRequestCheckAsync(cardRequest.TransRef, cardRequest.ProviderCode);
                if (checkExist.ResponseCode != "10")
                {
                    response.ResponseMessage =
                        $"Giao dịch của tài khoản: {cardRequest.ProviderCode} có mã giao dịch: {cardRequest.TransRef} đã tồn tại";
                    response.ResponseCode = ResponseCodeConst.ResponseCode_RequestAlreadyExists;
                }
                else
                {
                    var check = await _cardRequestService.CardRequestCheckDuplicate(cardRequest.Serial,
                        cardRequest.CardCode);
                    if (!check)
                    {
                        response.ResponseMessage = "Serial hoặc mã thẻ đã tồn tại";
                        response.ResponseCode = ResponseCodeConst.ResponseCode_CardDuplicate;
                    }
                    else
                    {
                        var cardInsert = await _cardRequestService.CardRequestCreateAsync(cardRequest);
                        if (cardInsert != null)
                        {
                            response.ResponseMessage = "Tiếp nhận thẻ thành công";
                            response.ResponseCode = ResponseCodeConst.ResponseCode_TopupReceived;
                        }
                        else
                        {
                            response.ResponseMessage = "Lỗi. Tiếp nhận thẻ không thành công";
                            response.ResponseCode = ResponseCodeConst.ResponseCode_00;
                        }
                    }
                }

                await context.RespondAsync<MessageResponseBase>(new
                {
                    context.Message.CorrelationId,
                    ReceiveTime = DateTime.Now,
                    response.ResponseCode,
                    response.ResponseMessage
                });
            }
            catch (Exception e)
            {
                _logger.LogError("CardRequestConsumer error:" + e);
                await context.RespondAsync<MessageResponseBase>(new
                {
                    context.Message.CorrelationId,
                    ReceiveTime = DateTime.Now,
                    ResponseCode = "00",
                    ResponseMessage = "Lỗi. Tiếp nhận thẻ không thành công"
                });
            }
        }
    }
}
