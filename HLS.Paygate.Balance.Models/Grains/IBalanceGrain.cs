using System.Threading.Tasks;
using HLS.Paygate.Balance.Models.Dtos;
using Orleans;
using Orleans.Concurrency;

namespace HLS.Paygate.Balance.Models.Grains;

public interface IBalanceGrain : IGrainWithStringKey
{
    Task<SettlementDto> Withdraw(SettlementDto settlement);
    Task<SettlementDto> Deposit(SettlementDto settlement);
    Task RevertBalanceModification(string transCode);
    [AlwaysInterleave]
    [ReadOnly]
    Task<decimal> GetBalance();
    //[AlwaysInterleave]
    Task<string> GetShardAccount();
    // [AlwaysInterleave]
    Task AddShardAccount(string shardAccount);
    [AlwaysInterleave]
    [ReadOnly]
    Task<decimal> GetBlockMoney();
    [AlwaysInterleave]
    [ReadOnly]
    Task<AccountBalanceDto> GetBalanceAccount();
    Task<bool> BlockBalance(decimal amount, string transcode);
    Task<bool> UnBlockBalance(decimal amount, string transCode);
}