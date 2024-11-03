using System.Threading.Tasks;
using GMB.Topup.Common.Domain.Services;
using GMB.Topup.Common.Model.Dtos.RequestDto;
using MassTransit;
using Microsoft.Extensions.Logging;
using GMB.Topup.Contracts.Commands.Commons;

namespace GMB.Topup.Common.Interface.Consumers;

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