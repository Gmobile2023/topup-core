using System.Threading.Tasks;
using HLS.Paygate.Common.Domain.Services;
using HLS.Paygate.Common.Model.Dtos.RequestDto;
using HLS.Paygate.Shared;
using MassTransit;
using Microsoft.Extensions.Logging;
using Paygate.Contracts.Commands.Commons;
using ServiceStack;

namespace HLS.Paygate.Common.Interface.Consumers;

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