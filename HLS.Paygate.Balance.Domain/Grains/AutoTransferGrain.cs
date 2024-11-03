using System;
using System.Threading;
using System.Threading.Tasks;
using HLS.Paygate.Balance.Domain.Services;
using HLS.Paygate.Balance.Models.Grains;
using HLS.Paygate.Balance.Models.Requests;
using HLS.Paygate.Shared;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;
using ServiceStack;

namespace HLS.Paygate.Balance.Domain.Grains;

public class AutoTransferGrain(IBalanceService balanceService, ILogger<AutoTransferGrain> logger) : Grain, IAutoTransferGrain
{
    private const string ReminderName = "AutoTransferReminder";

    public Task Start()
    {
        return Task.CompletedTask;
    }
    
    public async Task ReceiveReminder(string reminderName, TickStatus status)
    {
        logger.LogInformation("Start AUTO_SYSTEM_TRANSFER for Date: {Date}",
            DateTime.Now.ToString("yy/MM/dd HH:mm:ss"));
        
        var accountGrain = GrainFactory.GetGrain<IBalanceGrain>(BalanceConst.PAYMENT_ACCOUNT + "|" + CurrencyCode.VND.ToString("G"));

        var paymentAccount = await accountGrain.GetBalanceAccount();

        if (paymentAccount.ShardCounter > 0)
        {
            for (int i = 1; i <= paymentAccount.ShardCounter; i++)
            {
                var shardAccountGrain = GrainFactory.GetGrain<IBalanceGrain>(BalanceConst.PAYMENT_ACCOUNT + "*" + i + "|" + CurrencyCode.VND.ToString("G"));
                var balance = await shardAccountGrain.GetBalance();
                logger.LogInformation("AUTO_SYSTEM_TRANSFER {acc} balance {bal}", BalanceConst.PAYMENT_ACCOUNT + "*" + i, balance);
                if (await shardAccountGrain.GetBalance() > 0)
                {
                    var transResult = await balanceService.TransferSystemAsync(new TransferSystemRequest()
                    {
                        DesAccount = BalanceConst.PAYMENT_ACCOUNT,
                        SrcAccount = BalanceConst.PAYMENT_ACCOUNT + "*" + i,
                        CurrencyCode = CurrencyCode.VND.ToString("G"),
                        TransNote = "Auto trans " + DateTime.Now.ToString("yyMMddHHmmss"),
                        TransRef = "Autotrans",
                        Amount = balance
                    });
                    
                    logger.LogInformation("AUTO_SYSTEM_TRANSFER {acc} result {result}", BalanceConst.PAYMENT_ACCOUNT + "*" + i, transResult.ToJson());
                }
            }
        }
    }
    
    private async Task SetupReminder(string prefixName)
    {
        string reminderName = prefixName + "_" + ReminderName;
    
        var firstTick = DateTime.Today.AddHours(4); // start the reminder at 4am LOCAL TIME
        if (firstTick < DateTime.Now)
        {
            // if the next start time has already passed, increase the startTime by a day
            firstTick = firstTick.AddDays(1);
        }

        var nextFirstTick = firstTick - DateTime.Now; // aka dueTime
        var interval = TimeSpan.FromDays(1); // aka period

        await this.RegisterOrUpdateReminder(reminderName, nextFirstTick, interval);
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        await SetupReminder("VND");
    }
}