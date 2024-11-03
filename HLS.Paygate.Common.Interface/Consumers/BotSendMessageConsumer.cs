using System.Threading.Tasks;
using HLS.Paygate.Common.Domain.Services;
using HLS.Paygate.Common.Model.Dtos;
using MassTransit;
using Microsoft.Extensions.Logging;
using Paygate.Contracts.Commands.Commons;
using Paygate.Contracts.Requests.Commons;
using ServiceStack;

namespace HLS.Paygate.Common.Interface.Consumers;

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