using System;
using System.Threading.Tasks;
using HLS.Paygate.Stock.Domains.Grains;
using Orleans;
using Orleans.Concurrency;

namespace HLS.Paygate.StockGrains;

[StatelessWorker]
public class StockManageGrain : Grain, IStockManageGrain
{
    public async Task Exchange(string srcStockCode, string desStockCode, string productCode, int amount,
        Guid correlationId, string batchCode)
    {
        var cards = await GrainFactory.GetGrain<IStockGrain>(string.Join("|", srcStockCode, productCode))
            .ExportCard(amount, correlationId, batchCode);
        await GrainFactory.GetGrain<IStockGrain>(string.Join("|", desStockCode, productCode))
            .ImportCard(cards, correlationId);
    }
}