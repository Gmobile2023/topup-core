using System;
using System.Linq;
using System.Threading.Tasks;
using GMB.Topup.Balance.Models.Dtos;
using GMB.Topup.Balance.Models.Enums;
using GMB.Topup.Balance.Models.Exceptions;
using GMB.Topup.Balance.Models.Grains;
using GMB.Topup.Shared;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Sagas;

namespace GMB.Topup.Balance.Domain.Activities;

public class BalanceDepositActivity : IActivity
{
    private readonly ILogger<BalanceDepositActivity> _logger;

    public BalanceDepositActivity(ILogger<BalanceDepositActivity> logger)
    {
        _logger = logger;
    }
    public const string SETTLEMENT = "Settlement";
    private static bool ReturnResult { get; set; }

    public async Task Execute(IActivityContext context)
    {
        var settlement = context.SagaProperties.Get<SettlementDto>(SETTLEMENT);
        var transCode = settlement.TransCode;
        _logger.LogInformation(
            "BalanceDepositActivity:{SettlementPaymentTransCode}-{SettlementTransRef}-{SettlementTransCode}",
            settlement.PaymentTransCode, settlement.TransRef, settlement.TransCode);
        if (context.SagaProperties.ContainsKey(transCode))
        {
            var result = context.SagaProperties.Remove(transCode, out settlement);
            if (!result)
                throw new BalanceException(6007, $"Exception when process {transCode}");
        }

        if (settlement.Status == SettlementStatus.Error)
        {
            throw new BalanceException(6007, $"Settlement error on Withdraw phase {transCode}");
        }
        
        var destinationAccount =
            context.GrainFactory.GetGrain<IBalanceGrain>(settlement.DesAccountCode + "|" + settlement.CurrencyCode);
        
        if (!new[] { TransactionType.MasterTopup, TransactionType.SystemTransfer }.Contains(settlement.TransType))
        {
            var shard = await destinationAccount.GetShardAccount();
        
            if (shard != destinationAccount.GetPrimaryKeyString()) //Check nếu là tk Shard thì tạo grain mới
            {
                destinationAccount = context.GrainFactory.GetGrain<IBalanceGrain>(shard);
                settlement.DesShardAccountCode = shard;
                _logger.LogTrace(
                    "BalanceDepositActivity create new shared account:{Shard}-{SettlementPaymentTransCode}-{SettlementTransRef}-{SettlementTransCode}",
                    shard, settlement.PaymentTransCode, settlement.TransRef, settlement.TransCode);
            }
        }

        settlement.ModifiedDate = DateTime.Now;
        try
        {
            settlement = await destinationAccount.Deposit(settlement);
            context.SagaProperties.Add(transCode, settlement);
            _logger.LogInformation($"BalanceDepositActivity Deposit done:{settlement.PaymentTransCode}-{settlement.TransRef}-{settlement.TransCode}");
        }
        catch (Exception e)
        {
            _logger.LogError($"BalanceDepositActivity Deposit error:{settlement.PaymentTransCode}-{settlement.TransRef}-{settlement.TransCode}-{e.Message}");
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
        
        _logger.LogInformation($"BalanceDepositActivity RevertBalanceModification: {settlement.PaymentTransCode}-{settlement.TransRef}-{settlement.TransCode} reason: " + context.GetSagaError());

        var sourceAccount =
            context.GrainFactory.GetGrain<IBalanceGrain>(settlement.DesShardAccountCode + "|" + settlement.CurrencyCode);
        await sourceAccount.RevertBalanceModification(settlement.TransCode);
        
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