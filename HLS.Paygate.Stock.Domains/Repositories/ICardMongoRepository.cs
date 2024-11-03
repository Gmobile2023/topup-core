using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HLS.Paygate.Stock.Contracts.Dtos;
using HLS.Paygate.Stock.Contracts.Enums;
using HLS.Paygate.Stock.Domains.Entities;
using MongoDbGenericRepository;

// using CardRequest = HLS.Paygate.Stock.Domains.Entities.CardRequest;

namespace HLS.Paygate.Stock.Domains.Repositories;

public interface ICardMongoRepository : IBaseMongoRepository
{
    IQueryable<TDocument> GetQueryable<TDocument>();
    Task CardCreateUniqueIndexAsync();
    Task<bool> CardInsertListTransAsync(List<Card> cards);

    Task<List<Card>> CardExportForExchangeAsync(int amount, CardStatus status, string stockType,
        decimal cardValue, string stockCode, string batchCode = null);

    Task<bool> CardImportFromExchangeAsync(List<Card> cards, CardStatus status, string stockCard);

    Task<List<Card>> CardExportForSaleAsync(int amount, string productCode, decimal cardValue,
        string stockCode,string transCode);

    Task<Card> CardGetCodeForUsingAsync(decimal cardValue, string productCode, string stockCode);
    // Task<Entities.CardRequest> GetCardRequestForUsing(int cardValue, string stockType);
    // Task<Entities.CardRequest> GetCardRequestTimeOut(int cardValue, string stockType);

    //Task<bool> CardBatchUpdateItemTransAsync(string batchCode, StockBatchItem item);
    Task<bool> CardBatchUpdateItemAsync(string batchCode, StockBatchItem item);
    Task<bool> CardUpdateStatusListAsync(List<Card> cards, CardStatus status);

    Task<List<StockTransferItemInfo>> GetCardQuantityAvailableInStock(string stockCode,
        string batchCode, string categoryCode, string productCode);
}