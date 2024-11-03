using System;
using System.Threading.Tasks;
using GMB.Topup.Balance.Models.Dtos;
using GMB.Topup.Balance.Models.Enums;
using Microsoft.Extensions.Logging;
using GMB.Topup.Balance.Models.Grains;
using Orleans.Sagas;

namespace GMB.Topup.Balance.Domain.Activities
{
    public class BalanceWithdrawActivity : IActivity
    {
        private readonly ILogger<BalanceWithdrawActivity> _logger;

        public BalanceWithdrawActivity(ILogger<BalanceWithdrawActivity> logger)
        {
            _logger = logger;
        }

        public const string SETTLEMENT = "Settlement";
        private static bool ReturnResult { get; set; }

        public async Task Execute(IActivityContext context)
        {
            var settlement = context.SagaProperties.Get<SettlementDto>(SETTLEMENT);
            _logger.LogInformation($"BalanceWithdrawActivity:{settlement.PaymentTransCode}-{settlement.TransRef}-{settlement.TransCode}");
            var sourceAccount = context.GrainFactory.GetGrain<IBalanceGrain>(settlement.SrcAccountCode + "|" + settlement.CurrencyCode);
            settlement = await sourceAccount.Withdraw(settlement);
            // ReturnResult = settlement.ReturnResult;
            // if (settlement.ReturnResult)
            settlement.ModifiedDate = DateTime.Now;
            context.SagaProperties.Add(settlement.TransCode, settlement);
            _logger.LogInformation($"BalanceWithdrawActivity done:{settlement.PaymentTransCode}-{settlement.TransRef}-{settlement.TransCode}");
        }

        public async Task Compensate(IActivityContext context)
        {
            var settlement = context.SagaProperties.Get<SettlementDto>(SETTLEMENT);
            var sourceAccount = context.GrainFactory.GetGrain<IBalanceGrain>(settlement.SrcShardAccountCode + "|" + settlement.CurrencyCode);
            await sourceAccount.RevertBalanceModification(settlement.TransCode);
            _logger.LogInformation($"BalanceWithdrawActivity RevertBalanceModification:{settlement.PaymentTransCode}-{settlement.TransRef}-{settlement.TransCode} reason: " + context.GetSagaError());
            if (settlement.Status != SettlementStatus.Error)
            {
                settlement.Status = SettlementStatus.Rollback;
                context.SagaProperties.Add(settlement.TransCode, settlement);
            }
            _logger.LogInformation($"BalanceWithdrawActivity RevertBalanceModification done:{settlement.PaymentTransCode}-{settlement.TransRef}-{settlement.TransCode}");
        }

        public Task<bool> HasResult()
        {
            return Task.FromResult(ReturnResult);
        }
    }
}
