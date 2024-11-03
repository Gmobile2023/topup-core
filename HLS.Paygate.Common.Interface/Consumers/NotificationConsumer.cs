using System.Threading.Tasks;
using HLS.Paygate.Common.Domain.Services;
using HLS.Paygate.Common.Model.Dtos.RequestDto;
using MassTransit;
using Microsoft.Extensions.Logging;
using Paygate.Contracts.Commands.Commons;
using ServiceStack;

namespace HLS.Paygate.Common.Interface.Consumers;

public class NotificationConsumer : IConsumer<NotificationSendCommand>
{
    private readonly ILogger<NotificationConsumer> _logger;
    private readonly INotificationSevice _notification;

    public NotificationConsumer(INotificationSevice notification, ILogger<NotificationConsumer> logger)
    {
        _notification = notification;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<NotificationSendCommand> context)
    {
        //_logger.LogInformation("SendNotificationRequest: " + context.Message.ToJson());

        await _notification.SendNotification(new SendNotificationRequest
        {
            Body = context.Message.Body,
            Data = context.Message.Data,
            Link = context.Message.Url,
            Title = context.Message.Title,
            AccountCode = context.Message.ReceivingAccount,
            AppNotificationName = context.Message.AppNotificationName,
            Severity = context.Message.Severity
        });
    }
}