using System;
using System.Threading.Tasks;
using HLS.Paygate.Balance.Models.Grains;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Concurrency;
using Orleans.Providers;

namespace HLS.Paygate.Balance.Domain.Grains;

[StatelessWorker]
[StorageProvider(ProviderName = "balance-grains-storage")]
public class TransferGrain : Grain, ITransferGrain
{
    private readonly ILogger<TransferGrain> _logger;

    public TransferGrain(ILogger<TransferGrain> logger)
    {
        _logger = logger;
    }

    async Task<(decimal, decimal)> ITransferGrain.Transfer(string fromAccount, string toAccount,
        decimal amountToTransfer, string transCode)
    {
        _logger.LogInformation(
            $"{transCode} Transfer from: {fromAccount} to {toAccount} with amount: {amountToTransfer}");
        var result1 = 0m;
        try
        {
            result1 = await GrainFactory.GetGrain<IBalanceGrain>(fromAccount)
                .Withdraw(amountToTransfer, transCode);
        }
        catch (Exception e)
        {
            _logger.LogError($"TransferGrain withdraw error: {e.Message}");
            return (-1, -1);
        }

        try
        {
            var result2 = await GrainFactory.GetGrain<IBalanceGrain>(toAccount).Deposit(amountToTransfer, transCode);

            return (result1, result2);
        }
        catch (Exception e)
        {
            _logger.LogError($"TransferGrain deposit error: {e.Message}");
            try
            {
                //refund
                await GrainFactory.GetGrain<IBalanceGrain>(fromAccount)
                    .Deposit(amountToTransfer, transCode);
            }
            catch (Exception ex)
            {
                _logger.LogError($"TransferGrain refund error: {ex.Message}");
            }

            return (-1, -1);
        }
    }
}