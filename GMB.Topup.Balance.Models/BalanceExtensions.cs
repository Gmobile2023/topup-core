using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GMB.Topup.Balance.Models.Dtos;
using GMB.Topup.Shared;
using Orleans.Sagas;
using ServiceStack;
using ServiceStack.Script;

namespace GMB.Topup.Balance.Models;

public static class BalanceExtensions
{
    /// <summary>
    /// Waits for a saga to complete by periodically querying it's status.
    /// </summary>
    /// <param name="that"></param>
    /// <param name="queryFrequency">How often to query the saga.</param>
    /// <returns></returns>
    public static async Task<List<SettlementDto>> WaitForTransferResult(this ISagaGrain that, List<SettlementDto> settlements, int queryFrequency = 1000)
    {
        return await WaitForTransferResult(new ISagaGrain[] { that }, settlements, queryFrequency);
    }

    /// <summary>
    /// Waits for an IEnumerable of sagas to complete by periodically querying
    /// their status.
    /// </summary>
    /// <param name="that"></param>
    /// <param name="settlements"></param>
    /// <param name="queryFrequency">How often to query the sagas.</param>
    /// <returns></returns>
    public static async Task<List<SettlementDto>> WaitForTransferResult(this IEnumerable<ISagaGrain> that, List<SettlementDto> settlements, int queryFrequency = 1000)
    {
        var sagas = that.ToList();
        var result = new List<SettlementDto>();
        while (sagas.Count > 0)
        {
            var completed = new List<ISagaGrain>();

            foreach (var saga in sagas)
            {
                if (await saga.HasCompleted())
                {
                    completed.Add(saga);
                    
                    foreach (var settlement in settlements)
                    {
                        var r = (await saga.GetResult(settlement.TransCode)).ToString().FromJson<SettlementDto>();
                        result.Add(r);
                    }
                }
            }

            sagas.RemoveAll(l => completed.Contains(l));

            await Task.Delay(queryFrequency);
        }

        return result;
    }
}