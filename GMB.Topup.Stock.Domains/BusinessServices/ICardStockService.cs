using System.Collections.Generic;
using System.Threading.Tasks;
using GMB.Topup.Shared;
using GMB.Topup.Stock.Contracts.ApiRequests;
using GMB.Topup.Stock.Contracts.Dtos;
using GMB.Topup.Stock.Contracts.Enums;

namespace GMB.Topup.Stock.Domains.BusinessServices;

public interface ICardStockService
{
    Task<bool> CardExchangeAsync(int amount, string productCode, string srcStockCode, string desStockCode);
    Task<bool> CardExchangeBatchAsync(string batchCode, string desStockCode);
    Task<List<CardDto>> CardExportForSaleAsync(int amount, string productCode, decimal cardValue, string stockCode,string transCode);

    Task<List<CardDto>> CardExportForExchangeAsync(int amount, CardStatus status, string productCode, decimal cardValue,
        string stockCode, string batchCode = null);

    Task<bool> CardImportFromExchangeAsync(List<CardDto> cardDtos, CardStatus status, string stockCard);
    Task<StockDto> StockGetAsync(string stockCode, string keyCode);
    Task StockUpdateAsync(StockDto stock);
    Task StockInsertAsync(StockDto stock);
    Task<List<StockDto>> StockGetListAsync(CardStockGetListRequest cardStockGetListRequest);
    Task<int> StockGetListCountAsync(CardStockGetListRequest cardStockGetListRequest);
    Task<MessagePagedResponseBase> StockGetPagedAsync(CardStockGetListRequest request);
    Task<bool> StockUpdateInventoryAsync(string stockCode, string keyCode, int amount);
    Task<bool> CardUpdateStatusListAsync(List<CardDto> cards, CardStatus status);

    Task<MessagePagedResponseBase> CardStockTransListAsync(CardStockTransListRequest input);
    Task<bool> StockSetInventoryAsync(string stockCode, string keyCode, int inventory);
}