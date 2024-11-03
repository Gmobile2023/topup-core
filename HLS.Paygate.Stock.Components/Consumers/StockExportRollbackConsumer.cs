using System;
using System.Linq;
using System.Threading.Tasks;
using HLS.Paygate.Shared;
using HLS.Paygate.Stock.Contracts.Enums;
using HLS.Paygate.Stock.Contracts.Events;
using HLS.Paygate.Stock.Domains.BusinessServices;
using MassTransit;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using Paygate.Contracts.Commands.Commons;
using Paygate.Contracts.Requests.Commons;

namespace HLS.Paygate.Stock.Components.Consumers;

public class StockExportRollbackConsumer : IConsumer<CardExportRollbackEvent>
{
    private readonly ILogger<StockExportRollbackConsumer> _logger;
    private readonly ICardStockService _cardStockService;

    public StockExportRollbackConsumer(ILogger<StockExportRollbackConsumer> logger, ICardStockService cardStockService)
    {
        _logger = logger;
        _cardStockService = cardStockService;
    }

    public async Task Consume(ConsumeContext<CardExportRollbackEvent> context)
    {
        var logCode = DateTime.Now.ToString("ddmmyyyyhhmmsstt");
        var lstSerial = context.Message.Cards?.Select(x => x.Serial).ToJson();
        
        await context.Publish<SendBotMessage>(new
        {
            Title = "Cập nhật tồn kho khi bán lỗi!",
            Message =
                $"Serial: {lstSerial}\nProduct:{context.Message.ProductCode} - Stock:{context.Message.StockCode}\nError:{context.Message.ErrorDetail}\nBắt đầu trả thẻ về kho...",
            Module = "STOCK",
            MessageType = BotMessageType.Error,
            Code = logCode,
            BotType = BotType.Dev,
            TimeStamp = DateTime.Now,
            CorrelationId = Guid.NewGuid()
        });
        
        _logger.LogInformation(
            $"Process back serial:{lstSerial} - Product:{context.Message.ProductCode} - Stock:{context.Message.StockCode}  to stock...");
        var isBack = await _cardStockService.CardUpdateStatusListAsync(context.Message.Cards, CardStatus.Active);
        _logger.LogInformation(
            $"Process back serial:{lstSerial} - Product:{context.Message.ProductCode} - Stock:{context.Message.StockCode} return {isBack}");
        var msm = isBack ? " thành công" : " thất bại";
        
        await context.Publish<SendBotMessage>(new
        {
            Title = $"Trả thẻ lại kho {msm}",
            Message =
                $"Serial: {lstSerial}.\nProduct:{context.Message.ProductCode} - Stock:{context.Message.StockCode}\nVui lòng kiểm tra lại thông tin kho thẻ",
            Module = "STOCK",
            MessageType = isBack ? BotMessageType.Wraning : BotMessageType.Error,
            Code = logCode,
            BotType = BotType.Dev
        });
    }
}