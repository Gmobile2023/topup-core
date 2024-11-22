using System;
using System.Threading.Tasks;
using Topup.Balance.Models.Dtos;
using Topup.Balance.Models.Enums;
using Topup.Balance.Models.Exceptions;
using Topup.Balance.Models.Grains;
using Microsoft.Extensions.Logging;
using Orleans.Sagas;

namespace Topup.Balance.Domain.Activities;

public class BalanceDepositNoShardActivity : IActivity
{
    private readonly ILogger<BalanceDepositNoShardActivity> _logger;

    public BalanceDepositNoShardActivity(ILogger<BalanceDepositNoShardActivity> logger)
    {
        _logger = logger;
    }
    public const string SETTLEMENT = "Settlement";
    private static bool ReturnResult { get; set; }

    public async Task Execute(IActivityContext context)
    {

        var settlement = context.SagaProperties.Get<SettlementDto>(SETTLEMENT);
        var transCode = settlement.TransCode;
        _logger.LogInformation($"BalanceDepositActivity:{settlement.PaymentTransCode}-{settlement.TransRef}-{settlement.TransCode}");
        if (context.SagaProperties.ContainsKey(transCode))
        {
            var result = context.SagaProperties.Remove(transCode, out settlement);
            if (!result)
                throw new BalanceException(6007, $"Exception when process {transCode}");
        }
        var destinationAccount =
            context.GrainFactory.GetGrain<IBalanceGrain>(settlement.DesAccountCode + "|" + settlement.CurrencyCode);
        _logger.LogInformation($"BalanceDepositActivity GetGrain:{settlement.PaymentTransCode}-{settlement.TransRef}-{settlement.TransCode}");

        settlement.ModifiedDate = DateTime.Now;
        try
        {
        _logger.LogInformation($"BalanceDepositActivity ModifyBalance:{settlement.PaymentTransCode}-{settlement.TransRef}-{settlement.TransCode}");
            settlement = await destinationAccount.Deposit(settlement);
            context.SagaProperties.Add(transCode, settlement);
            _logger.LogInformation($"BalanceDepositActivity ModifyBalance done:{settlement.PaymentTransCode}-{settlement.TransRef}-{settlement.TransCode}");
        }
        catch (Exception e)
        {
            _logger.LogError($"BalanceDepositActivity error:{settlement.PaymentTransCode}-{settlement.TransRef}-{settlement.TransCode}-{e.Message}");
            settlement.Description = e.Message;
            settlement.Status = SettlementStatus.Error;
            context.SagaProperties.Add(transCode, settlement);
            throw;
        }
    }

    public async Task Compensate(IActivityContext context)
    {
        var settlement = context.SagaProperties.Get<SettlementDto>(SETTLEMENT);

        var transCode = settlement.TransCode;

        if (context.SagaProperties.ContainsKey(transCode))
        {
            var result = context.SagaProperties.Remove(transCode, out settlement);
            if (!result)
                throw new BalanceException(6007, $"Exception when process {transCode}");
        }

        var sourceAccount =
            context.GrainFactory.GetGrain<IBalanceGrain>(settlement.DesShardAccountCode + "|" + settlement.CurrencyCode);
        await sourceAccount.RevertBalanceModification(settlement.TransCode);
        _logger.LogInformation($"BalanceDepositActivity RevertBalanceModification:{settlement.PaymentTransCode}-{settlement.TransRef}-{settlement.TransCode}");
        if (settlement.Status != SettlementStatus.Error)
        {
            settlement.Status = SettlementStatus.Rollback;
            context.SagaProperties.Add(settlement.TransCode, settlement);
        }
    }

    public Task<bool> HasResult()
    {
        return Task.FromResult(ReturnResult);
    }
}