using System.Threading.Tasks;
using Topup.Common.Domain.Services;
using Topup.Common.Model.Dtos.RequestDto;
using MassTransit;
using Microsoft.Extensions.Logging;
using Topup.Contracts.Commands.Commons;
using Topup.Shared;
using ServiceStack;

namespace Topup.Common.Interface.Consumers;

public class AuditLogConsumer : IConsumer<AccountActivitiesCommand>
{
    private readonly IAuditLogService _auditLog;

    //private readonly Logger _logger = LogManager.GetLogger("BotSendMessageConsumer");
    private readonly ILogger<AuditLogConsumer> _logger;

    public AuditLogConsumer(ILogger<AuditLogConsumer> logger, IAuditLogService auditLog)
    {
        _logger = logger;
        _auditLog = auditLog;
    }

    public async Task Consume(ConsumeContext<AccountActivitiesCommand> context)
    {
        //_logger.LogInformation($"AccountActivityHistoryMessage request:{context.Message.ToJson()}");
        var request = context.Message.ConvertTo<AccountActivityHistoryRequest>();
        request.AccountActivityType = (AccountActivityType) context.Message.AccountActivityType;
        var rs = await _auditLog.AddAccountActivityHistory(request);
        //_logger.LogInformation($"AccountActivityHistoryMessage resoponse:{rs}");
    }
}