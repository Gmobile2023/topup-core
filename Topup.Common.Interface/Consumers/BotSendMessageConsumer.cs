using System.Threading.Tasks;
using Topup.Common.Domain.Services;
using Topup.Common.Model.Dtos;
using MassTransit;
using Microsoft.Extensions.Logging;
using Topup.Contracts.Commands.Commons;
using ServiceStack;

namespace Topup.Common.Interface.Consumers;

public class BotSendMessageConsumer : IConsumer<SendBotMessage>, IConsumer<SendBotMessageToGroup>
{
    private readonly IBotMessageService _botMessageService;

    //private readonly Logger _logger = LogManager.GetLogger("BotSendMessageConsumer");
    private readonly ILogger<BotSendMessageConsumer> _logger;

    public BotSendMessageConsumer(IBotMessageService botMessageService, ILogger<BotSendMessageConsumer> logger)
    {
        _botMessageService = botMessageService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<SendBotMessage> context)
    {
        _logger.LogInformation($"SendBotMessage received: {context.Message.ToJson()}");
        var input = context.Message.ConvertTo<SendAlarmMessageInput>();
        if (!string.IsNullOrEmpty(context.Message.ChatId))
            input.ChatId = long.Parse(context.Message.ChatId);
        await _botMessageService.SendAlarmMessage(input);
    }

    public async Task Consume(ConsumeContext<SendBotMessageToGroup> context)
    {
        var input = context.Message.ConvertTo<SendAlarmMessageInput>();
        if (!string.IsNullOrEmpty(context.Message.ChatId))
            input.ChatId = long.Parse(context.Message.ChatId);
        await _botMessageService.SendAlarmMessage(input);
    }
}