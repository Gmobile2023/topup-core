using System;
using System.Threading.Tasks;
using GMB.Topup.Balance.Domain.Services;
using GMB.Topup.Balance.Models.Dtos;
using GMB.Topup.Balance.Models.Enums;
using GMB.Topup.Balance.Models.Exceptions;
using GMB.Topup.Balance.Models.Grains;
using GMB.Topup.Shared;
using GMB.Topup.Shared.Helpers;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Concurrency;
using Orleans.Providers;

namespace GMB.Topup.Balance.Domain.Grains;

[StorageProvider(ProviderName = "balance-grains-storage")]
public class BalanceGrain(
    ILogger<BalanceGrain> logger,
    IBalanceService balanceService)
    : Grain<(IdempotencyShield shield, AccountBalanceDto account)>, IBalanceGrain
{
    
    //private static readonly string ReminderName = nameof(BalanceGrain);
    async Task<SettlementDto> IBalanceGrain.Withdraw(SettlementDto settlement)
    {
        logger.LogInformation(
            "Withdraw request: {StateAccountCode} -{SettlementPaymentTransCode}-{SettlementTransRef}-{SettlementTransCode} - {SettlementAmount}",
            State.account.AccountCode, settlement.PaymentTransCode, settlement.TransRef, settlement.TransCode,
            settlement.Amount);

        if (State.shield.CheckIdempotency(settlement.TransCode)) return null;

        if (State.account.AvailableBalance - settlement.Amount < 0)
            throw new BalanceException(31, $"{this.GetPrimaryKeyString()}'s balance is not enough");
            
        State.account.LastTransCode = settlement.TransCode;
        State.account.ModifiedDate = DateTime.Now;
        
        if (State.account.AccountType == BalanceAccountTypeConst.SYSTEM)
        {
            settlement.SrcAccountBalanceBeforeTrans = await GrainFactory
                .GetGrain<IBalanceGrain>(
                    State.account.AccountCode.GetOriginAccountCode() + "|" + State.account.CurrencyCode)
                .GetBalance();
        }
        else
        {
            settlement.SrcAccountBalanceBeforeTrans = State.account.Balance;    
        }
        
        State.account.Balance -= settlement.Amount;
        settlement.SrcAccountBalance = settlement.SrcAccountBalanceBeforeTrans - settlement.Amount;
        if (string.IsNullOrEmpty(settlement.DesAccountCode)) // Nếu DesAccount = null thì thì đánh dấu trạng thái settment về Done luôn
            settlement.Status = SettlementStatus.Done;
        
        await WriteStateAsync();
        
        State.shield.CommitTransaction(new RecentTrans
        {
            TransCode = settlement.TransCode,
            Amount = -settlement.Amount,
            CreatedDate = DateTime.Now
        });

        logger.LogInformation("Withdraw done:{PaymentTransCode}-{TransRef}-{TransCode}",
            settlement.PaymentTransCode, settlement.TransRef, settlement.TransCode);
        return settlement;
    }

    async Task<SettlementDto> IBalanceGrain.Deposit(SettlementDto settlement)
    {
        logger.LogInformation(
            "Deposit request: {AccountCode} -{PaymentTransCode}-{TransRef}-{TransCode} - {Amount}",
            State.account.AccountCode, settlement.PaymentTransCode, settlement.TransRef, settlement.TransCode,
            settlement.Amount);

        if (State.shield.CheckIdempotency(settlement.TransCode)) return null;
        
        State.account.LastTransCode = settlement.TransCode;
        State.account.ModifiedDate = DateTime.Now;
            
        if (State.account.AccountType == BalanceAccountTypeConst.SYSTEM)
        {
            settlement.DesAccountBalanceBeforeTrans = await GrainFactory
                .GetGrain<IBalanceGrain>(
                    State.account.AccountCode.GetOriginAccountCode() + "|" + State.account.CurrencyCode)
                .GetBalance();
        }
        else
        {
            settlement.DesAccountBalanceBeforeTrans = State.account.Balance;    
        }
        State.account.Balance += settlement.Amount;
        
        settlement.DesAccountBalance = settlement.DesAccountBalanceBeforeTrans + settlement.Amount;
        
        settlement.Status = SettlementStatus.Done;
        
        await WriteStateAsync();
        
        State.shield.CommitTransaction(new RecentTrans
        {
            TransCode = settlement.TransCode,
            Amount = settlement.Amount,
            CreatedDate = DateTime.Now
        });

        logger.LogInformation("Deposit done: {PaymentTransCode}-{TransRef}-{TransCode}", settlement.PaymentTransCode,
            settlement.TransRef, settlement.TransCode);
        
        if (State.account.AccountType == BalanceAccountTypeConst.SYSTEM)
        {
            if (State.account.AccountCode.Contains("*"))
            {
                await GrainFactory
                    .GetGrain<IBalanceGrain>(State.account.AccountCode.GetOriginAccountCode() + "|" + State.account.CurrencyCode)
                    .AddShardAccount(this.GetPrimaryKeyString());
            }
            else
            {
                if (State.account.ShardCounter > 0)
                    await this.AddShardAccount(this.GetPrimaryKeyString());
            }
        }
        
        return settlement;
    }

    async Task IBalanceGrain.RevertBalanceModification(string transCode)
    {
        logger.LogInformation("RevertBalanceModification request: {TransCode}", transCode);
        if (State.shield.CheckIdempotency(transCode))
        {
            var amount = State.shield.RollbackTransaction(transCode);
            
            if (amount == 0) return;
            
            State.account.LastTransCode = transCode;
            State.account.ModifiedDate = DateTime.Now;
            State.account.Balance -= amount;
            await WriteStateAsync();
        }
    }

    [ReadOnly]
    async Task<decimal> IBalanceGrain.GetBalance()
    {
        if (State.account.AccountCode.Contains("*") || State.account.AccountType == BalanceAccountTypeConst.CUSTOMER)
            return State.account.AvailableBalance;

        var ba = State.account.AvailableBalance;

        for (var i = 1; i <= State.account.ShardCounter; i++)
        {
            var grain = GrainFactory.GetGrain<IBalanceGrain>(State.account.AccountCode + "*" + i + "|" + State.account.CurrencyCode);
            ba += await grain.GetBalance();
        }

        return ba;
    }

    [ReadOnly]
    Task<decimal> IBalanceGrain.GetBlockMoney()
    {
        // await ReadStateAsync();
        return Task.FromResult(State.account.BlockedMoney);
    }

    // [ReadOnly]
    // Task<ushort> IBalanceGrain.GetShardCounter()
    // {
    //     // await ReadStateAsync();
    //     return Task.FromResult(State.account.ShardCounter);
    // }

    [ReadOnly]
    Task<AccountBalanceDto> IBalanceGrain.GetBalanceAccount()
    {
        return Task.FromResult(State.account);
    }

    async Task<string> IBalanceGrain.GetShardAccount()
    {
        if (State.account.AccountCode.Contains("*") || State.account.AccountType != BalanceAccountTypeConst.SYSTEM)
            return this.GetPrimaryKeyString();

        if (State.account.ShardQueue.TryDequeue(out var acc))
        {
            //await WriteStateLocalAsync($"GetShard: {acc}"); // lấy trong ShardQueue, update lại state
            return acc;
        }
        
        //Nếu ko lấy đc, tăng CurrentShardCounter lên 1
        ushort shardId = ++State.account.CurrentShardCounter;
        
        if (shardId > State.account.ShardCounter)
        {
            State.account.ShardCounter = shardId;
            await WriteStateAsync();
        }
       
        return State.account.AccountCode + "*" + shardId + "|" + State.account.CurrencyCode;
    }

    public async Task AddShardAccount(string shardAccount)
    {
        // bool updateStateResult;
        // var retry = 0;
        // do
        // {
            State.account.ShardQueue.Enqueue(shardAccount);
            //logger.LogTrace("AddShard {TransCode}, retry: {Retry}", shardAccount, retry);
        //     retry++;
        //     updateStateResult = await WriteStateLocalAsync($"AddShard:{shardAccount}");
        // } while (!updateStateResult && retry < 3);
        
    }

    async Task<bool> IBalanceGrain.BlockBalance(decimal amount, string transCode)
    {
        logger.LogInformation("BlockBalance request: {AccountCode}-{Amount}", State.account.AccountCode, amount);
        
        State.account.ModifiedDate = DateTime.Now;
        if (State.account.AvailableBalance - amount < 0)
            throw new BalanceException(6001, $"{State.account.AccountCode} Balance is not enough");
        State.account.BlockedMoney += amount;

        var updateStateResult = true;
        var retry = 0;

        do
        {
            
            var result = true;
            try
            {
                result = await balanceService.AccountBalanceUpdateAsync(State.account);

                if (result)
                {
                    State.account.CheckSum = State.account.ToCheckSum();
                }
                else
                {
                    throw new BalanceException(6001, $"{State.account.AccountCode} update fail");
                }
            }
            catch (Exception e) //InconsistentStateException
            {
                await ReadStateAsync();
                retry++;
                logger.LogError("BlockBalance update data error: {Ex}", e);
                throw;
            }
            finally
            {
                logger.LogTrace("BlockBalance {TransCode}, retry: {Retry}", transCode, retry);
                if (result) //update db thành công thì update state
                {
                    var tempCheckSum = State.account.CheckSum;
                    updateStateResult = await WriteStateLocalAsync(transCode);
                    if (!updateStateResult)
                    {
                        State.account.CheckSum = tempCheckSum;
                        result = await balanceService.AccountBalanceUpdateAsync(State.account);
                        if (result)
                            State.account.CheckSum = State.account.ToCheckSum();
                        else
                        {
                            logger.LogError("BlockBalance {TransCode} - {AccountCode} revert update data error", State.account.AccountCode,
                                transCode);
                            //Hủy State hiện tại
                            this.DeactivateOnIdle();
                            
                            throw new BalanceException(6001, $"{State.account.AccountCode} update fail");
                        }

                        retry++;
                    }
                }
            }
        } while (!updateStateResult && retry < 3);

        return updateStateResult;
    }

    public async Task<bool> UnBlockBalance(decimal amount, string transCode)
    {
        logger.LogInformation("UnBlockBalance request: {AccountCode}-{Amount}", State.account.AccountCode, amount);
        var updateStateResult = true;
        var retry = 0;

        do
        {
            State.account.ModifiedDate = DateTime.Now;
            //State.LastTransCode = transCode;
            if (State.account.AvailableBalance - amount < 0)
                throw new BalanceException(6001, $"{State.account.AccountCode} Balance is not enough");
            State.account.BlockedMoney -= amount;
            var result = true;
            try
            {
                result = await balanceService.AccountBalanceUpdateAsync(State.account);

                if (result)
                {
                    State.account.CheckSum = State.account.ToCheckSum();
                }
                else
                {
                    throw new BalanceException(6001, $"{State.account.AccountCode} update fail");
                }
            }
            catch (Exception e) //InconsistentStateException
            {
                await ReadStateAsync();
                retry++;
                logger.LogError("BlockBalance update data error: {Ex}", e);
                throw;
            }
            finally
            {
                logger.LogTrace("UnBlockBalance {TransCode}, retry: {Retry}", transCode, retry);
                if (result) //update db thành công thì update state
                {
                    var tempCheckSum = State.account.CheckSum;
                    updateStateResult = await WriteStateLocalAsync(transCode);
                    if (!updateStateResult)
                    {
                        State.account.CheckSum = tempCheckSum;
                        result = await balanceService.AccountBalanceUpdateAsync(State.account);
                        if (result)
                            State.account.CheckSum = State.account.ToCheckSum();
                        else
                        {
                            logger.LogError("UnBlockBalance {TransCode} - {AccountCode} revert update data error", State.account.AccountCode,
                                transCode);
                            //Hủy State hiện tại
                            this.DeactivateOnIdle();
                            
                            throw new BalanceException(6001, $"{State.account.AccountCode} update fail");
                        }

                        retry++;
                    }
                }
            }
        } while (!updateStateResult && retry < 3);

        return updateStateResult;
    }

    // public override async Task OnActivateAsync(CancellationToken cancellationToken)
    // {
    //     logger.LogInformation("On activate: {Account}", this.GetPrimaryKeyString());
    //     
    //     await base.OnActivateAsync(cancellationToken);
    //     // RegisterTimer(async s =>
    //     // {
    //     //     State.account.RecentTrans.RemoveWhere(p => p.CreatedDate.AddMinutes(3) < DateTime.Now);
    //     //     await WriteStateAsync();
    //     // }, State, TimeSpan.FromMinutes(5), TimeSpan.FromHours(1));
    //
    //     // if (accountCode == "PAYMENT")
    //     // {
    //     //     await SetupReminder(accountCode);
    //     // }
    // }

    // private async Task SetupReminder(string prefixName)
    // {
    //     string reminderName = prefixName + "_" + ReminderName;
    //
    //     var firstTick = DateTime.Today.AddHours(4); // start the reminder at 4pm EST
    //     if (firstTick < DateTime.Now)
    //     {
    //         // if the next start time has already passed, increase the startTime by a day
    //         firstTick = firstTick.AddDays(1);
    //     }
    //
    //     var nextFirstTick = firstTick - DateTime.Now; // aka dueTime
    //     var interval = TimeSpan.FromDays(1); // aka period
    //
    //     await this.RegisterOrUpdateReminder(reminderName, nextFirstTick, interval);
    // }

    private async Task<bool> WriteStateLocalAsync(string transCode)
    {
        try
        {
            await WriteStateAsync();
            return true;
        }
        catch (Exception e)
        {
            logger.LogError("WriteStateLocalAsync error: {TransCode}: {Error}", transCode, e.Message);
            // if (e.Message.Contains("e-Tag mismatch in Memory Storage") ||
            //     e.Message.Contains("Version conflict (WriteStateAsync)"))
                await ReadStateAsync();
            return false;
        }
    }

    // public async Task ReceiveReminder(string reminderName, TickStatus status)
    // {
    //     if (State.ShardCounter > 0)
    //         for (var i = 1; i <= State.ShardCounter; i++)
    //         {
    //             var shardAccountGrain = GrainFactory.GetGrain<IBalanceGrain>(State.AccountCode + "*" + i +
    //                                                                          "|" + State.CurrencyCode);
    //             var balance = await shardAccountGrain.GetBalance();
    //             logger.LogInformation("AUTO_SYSTEM_TRANSFER {acc} balance {bal}",
    //                 State.AccountCode + "*" + i, balance);
    //             if (await shardAccountGrain.GetBalance() > 0)
    //             {
    //                 var transResult = await TransferSystemAsync(new TransferSystemRequest
    //                 {
    //                     DesAccount = accountCode,
    //                     SrcAccount = accountCode + "*" + i,
    //                     CurrencyCode = currencyCode.ToString("G"),
    //                     TransNote = "Auto trans " + DateTime.Now.ToString("yyMMddHHmmss"),
    //                     TransRef = "Autotrans",
    //                     Amount = balance
    //                 });
    //
    //                 logger.LogInformation("AUTO_SYSTEM_TRANSFER {acc} result {result}",
    //                     accountCode + "*" + i, transResult.ToJson());
    //             }
    //         }
    // }
}