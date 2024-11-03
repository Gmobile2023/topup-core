using System;
using System.Threading.Tasks;
using GMB.Topup.Commission.Domain.Services;
using MassTransit;
using Microsoft.Extensions.Logging;
using GMB.Topup.Contracts.Commands.Commissions;
using ServiceStack;

namespace GMB.Topup.Commission.Interface.Consumers;

public class CommissionTransactionConsumer : IConsumer<CommissionTransactionCommand>
{
    private readonly ICommissionService _commissionService;
    private readonly ILogger<CommissionTransactionConsumer> _logger;

    public CommissionTransactionConsumer(ILogger<CommissionTransactionConsumer> logger,
        ICommissionService commissionService)
    {
        _logger = logger;
        _commissionService = commissionService;
    }

    public async Task Consume(ConsumeContext<CommissionTransactionCommand> context)
    {
        try
        {
            var request = context.Message;
            _logger.LogInformation(
                $"CommissionTransactionConsumer request:{request.TransRef}-{request.ParentCode}--{request.PartnerCode}");
            var rs = await _commissionService.CommissionRequest(request);
            _logger.LogInformation($"CommissionTransactionConsumer return:{rs.ToJson()}-{request.TransRef}");
        }
        catch (Exception e)
        {
            _logger.LogInformation(
                $"{context.Message.TransRef}-{context.Message.ParentCode}--{context.Message.PartnerCode}CommissionTransactionConsumer error:{e}");
        }
    }
}