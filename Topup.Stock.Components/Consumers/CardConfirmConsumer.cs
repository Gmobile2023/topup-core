using System;
using System.Threading.Tasks;
using HLS.Paygate.Gw.Model.Commands;
using HLS.Paygate.Gw.Model.Enums;
using HLS.Paygate.Gw.Model.Events;
using HLS.Paygate.Shared;
using MassTransit;
using MongoDB.Bson;
using NLog;
using Orleans;
using HLS.Paygate.Stock.Domains.BusinessServices;
using HLS.Paygate.Stock.Domains.Grains;

namespace HLS.Paygate.Stock.Components.Consumers
{
    public class CardConfirmConsumer : IConsumer<CardConfirmCommand>
    {
        private readonly Logger _logger = LogManager.GetLogger("CardConfirmConsumer");
        private readonly ICardRequestService _cardRequestService;
        private readonly ICardService _cardService;
        private readonly IClusterClient _clusterClient;

        public CardConfirmConsumer(ICardRequestService cardRequestService, IClusterClient clusterClient,
            ICardService cardService)
        {
            _cardRequestService = cardRequestService;
            _clusterClient = clusterClient;
            _cardService = cardService;
        }

        public async Task Consume(ConsumeContext<CardConfirmCommand> context)
        {
            try
            {
                _logger.LogInformation($"CardConfirmConsumer request:{context.Message.ToJson()}");
                var mess = context.Message;
                var response = new MessageResponseBase();
                if (!string.IsNullOrEmpty(mess.StockCode))
                {
                    if (mess.ConfirmType == CardConfirmType.CardRefund ||
                        mess.ConfirmType == CardConfirmType.CardInvalid) //Nếu lấy thẻ ở kho nhà thì Trả thẻ lại kho
                    {
                        var card = await _cardService.CardGetAsync(mess.CardId, null, null);
                        if (card != null)
                        {
                            //Gán stock cần back lại
                            card.StockCode = mess.StockCode;
                            var srcStock = _clusterClient.GetGrain<IStockGrain>(string.Join("|",
                                card.StockCode, card.ProductCode));
                            var result = await srcStock.CardBackStock(card, Guid.NewGuid());
                            if (result)
                            {
                                response.ResponseCode = ResponseCodeConst.Success;
                                response.ResponseMessage = "Trả thẻ lại kho thành công";
                                _logger.LogInformation(
                                    $"BACK_TO_INVENTORY success: {card.ProductCode}-{card.CardValue}");
                            }
                            else
                            {
                                response.ResponseCode = ResponseCodeConst.Error;
                                response.ResponseMessage = "Trả thẻ lại kho thất bại";
                                _logger.LogInformation(
                                    $"BACK_TO_INVENTORY fail: {card.ProductCode}-{card.CardValue}");
                            }
                        }
                        else
                        {
                            response.ResponseCode = ResponseCodeConst.Error;
                            response.ResponseMessage = "Không tìm thấy thông tin thẻ. Trả thẻ lại kho thất bại";
                        }
                    }
                }
                else
                {
                    var cardResponse = new CardResponseMesssage();
                    var cardRequest = await _cardRequestService.CardRequestGetAsync(mess.CardId);
                    if (cardRequest != null)
                    {
                        //var cardRequestStatus = CardRequestStatus.Init;
                        if (mess.ConfirmType == CardConfirmType.CardSucess)
                        {
                            cardResponse.ResponseCode = "01";
                            cardResponse.ResponseMessage = "Thành công!";
                            //Todo chỗ này xem tính toàn chiết khấu tiền nhận được của đối tác=>Publish message cộng tiền cho KH
                            cardRequest.Status = CardRequestStatus.Success;
                            cardRequest.ExportedDate = DateTime.Now;

                            await context.Publish<BalanceChanged>(new
                            {
                                AccountCode = cardRequest.ProviderCode,
                                Amount = GetCardDiscount(cardRequest.Vendor,
                                    cardRequest
                                        .RequestValue), //Chỗ này phải tính số tiền nhận được trên mệnh giá thẻ. để cộng số dư cho đối tác
                                cardRequest.TransRef,
                                cardRequest.TransCode,
                                TransNote = $"Cộng tiền cho giao dịch gạch thẻ. Mã giao dịch: {cardRequest.TransRef}",
                                TransactionType = TransactionType.CardCharges
                            });
                        }

                        if (mess.ConfirmType == CardConfirmType.CardRefund)
                        {
                            cardRequest.Status = CardRequestStatus.Init;
                        }

                        if (mess.ConfirmType == CardConfirmType.CardInvalid)
                        {
                            //cardResponse.ResponseCode = "02";
                            //cardResponse.ResponseMessage = "Thẻ lỗi";
                            cardRequest.Status = CardRequestStatus.Failed;
                        }
                        if (mess.ConfirmType == CardConfirmType.CardWaiting)
                        {
                            //cardResponse.ResponseCode = "02";
                            //cardResponse.ResponseMessage = "Giao dịch chưa có kết quả";
                            cardRequest.Status = CardRequestStatus.WaitForResult;
                        }

                        if (mess.ConfirmType == CardConfirmType.CardInvalidAmount)
                        {
                            //Todo chỗ này vẫn ghi nhận là thành công. Nhưng đánh dấu thẻ sai mệnh giá
                            cardRequest.Status = CardRequestStatus.InvalidCardValue;
                            //cardResponse.ResponseCode = "08";
                            //cardResponse.ResponseMessage = "Thẻ sai mệnh giá";
                        }

                        //Callback thông tin thẻ cho khách hàng
                        await _cardRequestService.CardRequestUpdateAsync(cardRequest);
                        if (mess.ConfirmType != CardConfirmType.CardRefund)
                        {
                            await context.Publish<CardCallBackCommand>(new
                            {
                                mess.CorrelationId,
                                cardRequest.Serial,
                                cardRequest.CardCode,
                                CardValue = cardRequest.RealValue,
                                cardResponse.ResponseMessage,
                                cardResponse.ResponseCode,
                                cardRequest.RequestValue,
                                cardRequest.TransCode,
                                RequestCode = cardRequest.TransRef
                            });
                        }
                    }
                    else
                    {
                        response.ResponseCode = ResponseCodeConst.Error;
                        response.ResponseMessage = "Không tìm thấy thông tin cardRequest";
                    }
                }

                await context.RespondAsync<MessageResponseBase>(new
                {
                    context.Message.CorrelationId,
                    ReceiveTime = DateTime.Now,
                    ResponseCode="01",
                    ResponseMessage="Thành công"
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
                    ResponseMessage = "Lỗi. Confirm thẻ"
                });
            }
        }

        private decimal GetCardDiscount(string stockType, int cardvalue)
        {
            //todo chỗ này sẽ lấy ck gạch thẻ
            const int discount = 30;
            return cardvalue - cardvalue * 30 / 100;
        }
    }
}
