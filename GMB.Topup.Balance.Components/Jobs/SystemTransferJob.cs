using System;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using GMB.Topup.Balance.Domain.Services;
using GMB.Topup.Balance.Models.Grains;
using GMB.Topup.Balance.Models.Requests;
using GMB.Topup.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Orleans;
using Quartz;
using ServiceStack;

namespace GMB.Topup.Balance.Components.Jobs;

public class SystemTransferJob : IJob
{
    private IBalanceService _balanceService;
    private ILogger<SystemTransferJob> _logger;
    private IClusterClient _clusterClient;

    private IServiceGateway _gateway;
    

    public SystemTransferJob(IBalanceService balanceService, ILogger<SystemTransferJob> logger, IClusterClient clusterClient, IConfiguration configuration)
    {
        _balanceService = balanceService;
        _logger = logger;
        _clusterClient = clusterClient;
        _gateway = HostContext.AppHost?.GetServiceGateway();
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("Start AUTO_SYSTEM_TRANSFER for Date: {Date}", DateTime.Now.ToString("yy/MM/dd HH:mm:ss"));
        var accountGrain = _clusterClient.GetGrain<IBalanceGrain>(BalanceConst.PAYMENT_ACCOUNT + "|" + CurrencyCode.VND.ToString("G"));

        var paymentAccount = await accountGrain.GetBalanceAccount();

        if (paymentAccount.ShardCounter > 0)
        {
            for (int i = 1; i <= paymentAccount.ShardCounter; i++)
            {
                var shardAccountGrain = _clusterClient.GetGrain<IBalanceGrain>(BalanceConst.PAYMENT_ACCOUNT + "*" + i + "|" + CurrencyCode.VND.ToString("G"));
                var balance = await shardAccountGrain.GetBalance();
                _logger.LogInformation("AUTO_SYSTEM_TRANSFER {acc} balance {bal}", BalanceConst.PAYMENT_ACCOUNT + "*" + i, balance);
                if (await shardAccountGrain.GetBalance() > 0)
                {
                    var transResult = await _gateway.SendAsync<MessageResponseBase>(new TransferSystemRequest()
                    {
                        DesAccount = BalanceConst.PAYMENT_ACCOUNT,
                        SrcAccount = BalanceConst.PAYMENT_ACCOUNT + "*" + i,
                        CurrencyCode = CurrencyCode.VND.ToString("G"),
                        TransNote = "Auto trans " + DateTime.Now.ToString("yyMMddHHmmss"),
                        TransRef = "Autotrans",
                        Amount = balance
                    });
                    
                    _logger.LogInformation("AUTO_SYSTEM_TRANSFER {acc} result {result}", BalanceConst.PAYMENT_ACCOUNT + "*" + i, transResult.ToJson());
                }
            }
        }
        
        _logger.LogInformation("End AUTO_SYSTEM_TRANSFER for Date: {Date}", DateTime.Now.ToString("yy/MM/dd HH:mm:ss"));
    }
}