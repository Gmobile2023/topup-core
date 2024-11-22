using System;
using System.Threading.Tasks;
using Topup.Balance.Models.Dtos;
using Topup.Balance.Models.Exceptions;
using Topup.Balance.Models.Grains;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Orleans.Configuration;
using Orleans.Runtime;
using Orleans.Storage;
using ServiceStack;
using Topup.Balance.Domain.Repositories;
using Topup.Balance.Domain.Services;

namespace Topup.Balance.Domain;

public class AccountBalanceStorage(
    string name,
    MemoryGrainStorageOptions options,
    ILogger<MemoryGrainStorage> logger,
    IGrainFactory grainFactory,
    IGrainStorageSerializer defaultGrainStorageSerializer,
    IBalanceService balanceService,
    IBalanceMongoRepository balanceRepository)
    : MemoryGrainStorage(name, options, logger, grainFactory,
        defaultGrainStorageSerializer) // Orleans.Storage.IGrainStorage
{
    //This is Specific to AccountBalance. State will still store in Memory but also store in Postgres
    
    public override async Task ReadStateAsync<T>(string stateName, GrainId grainId, IGrainState<T> grainState)
    {
        var state = grainState.State as (IdempotencyShield shield, AccountBalanceDto account)?;
        if (state == null)
            throw new Exception("Invalid state in AccountBalanceStorage::ReadStateAsync");
        
        await base.ReadStateAsync(stateName, grainId, grainState);
        
        var keys = grainId.Key.ToString().Split('|');
        if (keys != null && state.Value.account == null)
        {
            var accountCode = keys[0];
            var currencyCode = keys[1];
            var accountBalanceTemp = await balanceService.AccountBalanceGetAsync(accountCode, currencyCode);
        
            AccountBalanceDto accountBalance;

            if (accountBalanceTemp != null)
            {
                if (accountBalanceTemp.ToCheckSum() != accountBalanceTemp.CheckSum)
                    throw new BalanceException(6001,
                        $"Account {accountBalanceTemp.AccountCode} has been modified outside the system");
            
                accountBalance = accountBalanceTemp.ConvertTo<AccountBalanceDto>();
            }
            else
            {
                accountBalance = new AccountBalanceDto { AccountCode = accountCode, CurrencyCode = currencyCode };
                accountBalance.CheckSum = accountBalance.ToCheckSum();
                accountBalance = await balanceService.AccountBalanceCreateAsync(accountBalance);
            }

            var shield = new IdempotencyShield(50);
            grainState.State = (shield, accountBalance) as dynamic;
        }
    }

    public override async Task WriteStateAsync<T>(string stateName, GrainId grainId, IGrainState<T> grainState)
    {
        var state = grainState.State as (IdempotencyShield shield, AccountBalanceDto account)?;
        if (state == null)
            throw new Exception("Invalid state in AccountBalanceStorage::WriteStateAsync");

        var result = await balanceService.AccountBalanceUpdateAsync(state.Value.account);
        if (result)
        {
            state.Value.account.CheckSum = state.Value.account.ToCheckSum();
            await base.WriteStateAsync(stateName, grainId, grainState);   
        }
        else
        {
            await this.ReadStateAsync(stateName, grainId, grainState);
            throw new BalanceException(6001, $"{grainId.Key.ToString()} update fail");   
        }
    }
    
}

public static class AccountBalanceStorageFactory
{
    /// <summary>
    ///     Creates a new <see cref="AccountBalanceStorage" /> instance.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="name">The name.</param>
    /// <returns>The storage.</returns>
    public static AccountBalanceStorage Create(IServiceProvider services, string name)
    {
        return ActivatorUtilities.CreateInstance<AccountBalanceStorage>(services,
            services.GetRequiredService<IOptionsMonitor<MemoryGrainStorageOptions>>().Get(name), name);
    }
}