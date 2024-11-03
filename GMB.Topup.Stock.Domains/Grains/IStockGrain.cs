using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GMB.Topup.Shared.Contracts.Events.Report;
using GMB.Topup.Stock.Contracts.Dtos;
using Orleans;

namespace GMB.Topup.Stock.Domains.Grains;

public interface IStockGrain : IGrainWithStringKey
{
    [Transaction(TransactionOption.Create)]
    Task<List<CardDto>> ExportCard(int quantity, Guid correlationId, string batchCode);

    [Transaction(TransactionOption.Create)]
    Task<bool> ImportCard(List<CardDto> cards, Guid correlationId);

    Task<Tuple<bool, int>> ImportInitCard(int itemValue, int amount, Guid correlationId);

    [Transaction(TransactionOption.Create)]
    Task<int> CheckAvailableInventory();

    [Transaction(TransactionOption.Create)]
    Task<List<CardDto>> Sale(int amount, Guid correlationId,string transCode=null);

    Task Allocate(int quantity, Guid correlationId);
    Task UnAllocate(List<ProviderCardStockItem> providerItem, int quantity, Guid correlationId);
    Task InitStock(decimal cardValue);
    Task ImportAirtime(int amount, Guid correlationId);
    Task ExportAirtime(int amount, Guid correlationId);
    Task<bool> UpdateInventory(int inventory);
}