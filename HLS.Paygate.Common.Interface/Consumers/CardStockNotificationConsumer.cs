using System;
using System.Linq;
using System.Threading.Tasks;
using HLS.Paygate.Common.Domain.Services;
using HLS.Paygate.Common.Interface.Configs;
using HLS.Paygate.Common.Model.Dtos;
using HLS.Paygate.Shared;
using HLS.Paygate.Shared.CacheManager;
using HLS.Paygate.Shared.Emailing;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Paygate.Contracts.Commands.Commons;
using Paygate.Contracts.Requests.Commons;
using ServiceStack;

namespace HLS.Paygate.Common.Interface.Consumers;

public class CardStockNotificationConsumer : IConsumer<StockInventoryNotificationCommand>
{
    private readonly IBotMessageService _botMessageService;
    private readonly ICacheManager _cacheManager;
    private readonly IConfiguration _configuration;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<CardStockNotificationConsumer> _logger;

    public CardStockNotificationConsumer(IEmailSender emailSender, IConfiguration configuration,
        ICacheManager cacheManager, IBotMessageService botMessageService,
        ILogger<CardStockNotificationConsumer> logger)
    {
        _emailSender = emailSender;
        _configuration = configuration;
        _cacheManager = cacheManager;
        _botMessageService = botMessageService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<StockInventoryNotificationCommand> context)
    {
        try
        {
            _logger.LogInformation($"CardStockNotificationConsumer request: {context.Message.ToJson()}");
            var config = _configuration.GetSendMailStockMinInventoryConfig();
            if (config.IsSendMail || config.IsBotMessage)
            {
                var key =
                    $"PayGate_StockMinInventory:Items:{string.Join("_", context.Message.StockCode, context.Message.ProductCode)}";
                var lastSend =
                    await _cacheManager.GetEntity<CacheSendEmailLimitMinStockInventoryDto>(key) ??
                    new CacheSendEmailLimitMinStockInventoryDto
                    {
                        TotalSend = 0,
                        LastTimeSend = DateTime.Now
                    };
                //Sau khoảng x phút. Nếu hết hạn thì reset lại CountSend. Bắt đầu cảnh báo lại. Khi đạt CountSend thì dừng gửi
                var totalMinute = (DateTime.Now - lastSend.LastTimeSend).TotalMinutes;
                if (totalMinute > config.TimeReSend)
                    lastSend.TotalSend = 0;

                if (lastSend.TotalSend < config.SendCount)
                {
                    lastSend.TotalSend += 1;
                    lastSend.LastTimeSend = DateTime.Now;
                    var emails = config.EmailReceive.Split(",").ToList();
                    if (context.Message.NotifiType == CardStockNotificationType.MinimumInventoryLimit)
                    {
                        if (config.IsSendMail)
                        {
                            var rs = _emailSender.SendEmailNotificationInventoryStock(emails,
                                context.Message.StockCode,
                                context.Message.ProductCode, context.Message.ProductCode,
                                context.Message.Inventory);
                            _logger.LogInformation($"CardStockNotificationConsumer return:{rs}");
                        }
                        else
                        {
                            if (config.IsBotMessage)
                                await _botMessageService.SendAlarmMessage(new SendAlarmMessageInput
                                {
                                    Title = "Cảnh báo kho mã thẻ",
                                    Message =
                                        $"Kho thẻ: {context.Message.StockCode}\n" +
                                        $"Sản phẩm: {context.Message.ProductCode}\n" +
                                        $"Tồn kho hiện tại: {context.Message.Inventory}\n" +
                                        "Vui lòng bổ sung thêm thẻ vào kho",
                                    Module = "STOCK",
                                    MessageType = BotMessageType.Wraning,
                                    BotType = BotType.Provider
                                });
                        }
                    }

                    await _cacheManager.AddEntity(key, lastSend, TimeSpan.FromDays(300));
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogInformation($"CardStockNotificationConsumer error: {e}");
        }
    }
}