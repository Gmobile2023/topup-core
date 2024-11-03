using System.Threading.Tasks;
using HLS.Paygate.Balance.Models.Dtos;
using HLS.Paygate.Balance.Models.Grains;
using Microsoft.Extensions.Logging;
using Orleans.Sagas;

namespace HLS.Paygate.Balance.Domain.Activities;

public class ModifyBalanceActivity : IActivity
{

    private readonly ILogger<ModifyBalanceActivity> _logger;

    public ModifyBalanceActivity(ILogger<ModifyBalanceActivity> logger)
    {
        _logger = logger;
    }
    public const string SETTLEMENT = "Settlement";
    private static bool ReturnResult { get; set; }

    public async Task Execute(IActivityContext context)
    {
        var settlement = context.SagaProperties.Get<SettlementDto>(SETTLEMENT);
        var sourceAccount =
            context.GrainFactory.GetGrain<IBalanceGrain>(settlement.DesAccountCode + "|" + settlement.CurrencyCode);
        _logger.LogInformation($"ModifyBalanceActivity:{settlement.PaymentTransCode}-{settlement.TransRef}-{settlement.TransCode}");
        settlement = await sourceAccount.ModifyBalance(settlement);
        _logger.LogInformation($"ModifyBalanceActivity ModifyBalance:{settlement.PaymentTransCode}-{settlement.TransRef}-{settlement.TransCode}");
        // ReturnResult = settlement.ReturnResult;
        // if (settlement.ReturnResult)
        context.SagaProperties.Add(SETTLEMENT, settlement);
    }

    public async Task Compensate(IActivityContext context)
    {
        var settlement = context.SagaProperties.Get<SettlementDto>(SETTLEMENT);
        var sourceAccount =
            context.GrainFactory.GetGrain<IBalanceGrain>(settlement.DesAccountCode + "|" + settlement.CurrencyCode);
        await sourceAccount.RevertBalanceModification(settlement.TransCode);
                _logger.LogInformation($"ModifyBalanceActivity RevertBalanceModification:{settlement.PaymentTransCode}-{settlement.TransRef}-{settlement.TransCode}");
    }

    public Task<bool> HasResult()
    {
        return Task.FromResult(ReturnResult);
    }
}