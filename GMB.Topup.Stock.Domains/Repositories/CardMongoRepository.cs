using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using GMB.Topup.Shared;
using GMB.Topup.Shared.CacheManager;
using GMB.Topup.Shared.Utils;
using GMB.Topup.Stock.Contracts.Dtos;
using GMB.Topup.Stock.Contracts.Enums;
using GMB.Topup.Stock.Domains.Entities;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoDbGenericRepository;
using MongoDbGenericRepository.Models;
using ServiceStack;

namespace GMB.Topup.Stock.Domains.Repositories;

public class CardMongoRepository : BaseMongoRepository, ICardMongoRepository
{
    private readonly ICacheManager _cacheManager;

    //private readonly Logger _logger = LogManager.GetLogger("CardMongoRepository");
    private readonly ILogger<CardMongoRepository> _logger;

    public CardMongoRepository(string connectionString, ICacheManager cacheManager,
        ILogger<CardMongoRepository> logger, string databaseName = null) :
        base(connectionString,
            databaseName)
    {
        _cacheManager = cacheManager;
        _logger = logger;
    }

    public CardMongoRepository(IMongoDbContext dbContext, ICacheManager cacheManager,
        ILogger<CardMongoRepository> logger) : base(dbContext)
    {
        _cacheManager = cacheManager;
        _logger = logger;
    }

    public IQueryable<TDocument> GetQueryable<TDocument>()
    {
        return MongoDbContext.GetCollection<TDocument>().AsQueryable();
    }

    public async Task CardCreateUniqueIndexAsync()
    {
        await CreateTextIndexAsync<Card, Guid>(p => p.Hashed, new IndexCreationOptions {Unique = true});
    }

    public async Task<bool> CardInsertListTransAsync(List<Card> cards)
    {
        try
        {
            _logger.LogInformation(
                $"CardInsertListTransAsync request: {cards.Count} - ListCard: {cards.Select(x => x.Serial).ToJson()}");
            using (var session =
                   await MongoDbContext.Client.StartSessionAsync(new ClientSessionOptions {CausalConsistency = true}))
            {
                try
                {
                    session.StartTransaction(new TransactionOptions(
                        ReadConcern.Majority,
                        writeConcern: WriteConcern.WMajority
                    ));
                    await MongoDbContext.GetCollection<Card>().InsertManyAsync(session, cards);
                    await session.CommitTransactionAsync();
                    _logger.LogInformation($"CardInsertListTransAsync success: {cards.Count}");
                    return true;
                }
                catch (Exception e)
                {
                    _logger.LogError($"CardInsertListAsync error: {e}");
                    await session.AbortTransactionAsync();
                    Console.WriteLine(e);
                    return false;
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError($"CardInsertListAsync process error: {e}");
            return false;
        }
    }

    public async Task<List<Card>> CardExportForExchangeAsync(int amount, CardStatus status, string productCode,
        decimal cardValue, string stockCode, string batchCode = null)
    {
        _logger.LogInformation(
            $"CardExportForExchangeAsync request: {amount}-{status}-{productCode}-{cardValue}-{stockCode}-{batchCode}");
        try
        {
            using (var session =
                   await MongoDbContext.Client.StartSessionAsync(new ClientSessionOptions {CausalConsistency = true}))
            {
                try
                {
                    session.StartTransaction(new TransactionOptions(
                        ReadConcern.Majority,
                        writeConcern: WriteConcern.WMajority
                    ));
                    var options = new FindOptions<Card, Card>
                    {
                        Limit = amount,
                        Sort = Builders<Card>.Sort.Ascending(x => x.ExpiredDate)
                    };
                    Expression<Func<Card, bool>> query = p =>
                        p.Status == status && p.ProductCode == productCode && // p.CardValue == cardValue &&
                        p.StockCode == stockCode;

                    if (!string.IsNullOrEmpty(batchCode))
                    {
                        Expression<Func<Card, bool>> newQuery = p => p.BatchCode == batchCode;
                        query = query.And(newQuery);
                    }

                    var search = await MongoDbContext.GetCollection<Card>().FindAsync(session, query, options);
                    var cards = await search.ToListAsync();
                    if (cards.Count < amount)
                    {
                        _logger.LogInformation("CardTake is not enough");
                        await session.AbortTransactionAsync();
                        return null;
                    }

                    var filterUpdate = Builders<Card>.Filter.In(x => x.Id, cards.Select(x => x.Id).ToList());
                    var update = await MongoDbContext.GetCollection<Card>().UpdateManyAsync(session,
                        filterUpdate,
                        Builders<Card>.Update.Set(p => p.Status, CardStatus.OnExchangeMode),
                        new UpdateOptions {IsUpsert = false}
                    );
                    if (update.IsModifiedCountAvailable && update.ModifiedCount == cards.Count)
                    {
                        _logger.LogInformation(
                            $"CardExportForExchangeAsync: {stockCode}-{productCode}-{amount}-{cardValue}-{batchCode}  success. Total card: {cards.Count} - ListCard:{cards.Select(x => x.Serial).ToJson()}");
                        await session.CommitTransactionAsync();
                        return cards;
                    }

                    _logger.LogError($"Update error {stockCode}-{productCode}-{amount}-{cardValue}-{batchCode}");
                    await session.AbortTransactionAsync();
                    return null;
                }
                catch (Exception e)
                {
                    _logger.LogError($"CardExportForExchangeAsync error: {e}");
                    await session.AbortTransactionAsync();
                    return null;
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError($"CardExportForExchangeAsync error: {e}");
            return null;
        }
    }

    public async Task<bool> CardImportFromExchangeAsync(List<Card> cards, CardStatus status, string stockCard)
    {
        try
        {
            _logger.LogInformation(
                $"CardImportFromExchangeAsync request: Total: {cards.Count} - ListCard{cards.Select(x => x.Serial).ToJson()}-{status}-{stockCard} batchCode {cards[0].BatchCode}");
            using (var session =
                   await MongoDbContext.Client.StartSessionAsync(new ClientSessionOptions {CausalConsistency = true}))
            {
                try
                {
                    session.StartTransaction(new TransactionOptions(
                        ReadConcern.Majority,
                        writeConcern: WriteConcern.WMajority
                    ));
                    var filterUpdate = Builders<Card>.Filter.In(x => x.Id, cards.Select(x => x.Id).ToList());
                    var update = await MongoDbContext.GetCollection<Card>().UpdateManyAsync(session,
                        filterUpdate,
                        Builders<Card>.Update.Set(p => p.StockCode, stockCard)
                            .Set(p => p.Status, status),
                        new UpdateOptions {IsUpsert = false}
                    );

                    if (update.IsModifiedCountAvailable && update.ModifiedCount == cards.Count)
                    {
                        _logger.LogInformation(
                            $"CardImportFromExchangeAsync: {stockCard}  success. Total: {cards.Count}. Serial: {cards.Select(x => x.Serial).ToJson()} batchCode {cards[0].BatchCode}");
                        await session.CommitTransactionAsync();
                        return true;
                    }

                    _logger.LogError($"batchCode {cards[0].BatchCode} Update error: {stockCard}");
                    await session.AbortTransactionAsync();
                    return false;
                    // foreach (var card in cards)
                    // {
                    //     card.StockCode = stockCard;
                    //     card.Status = status;
                    //     await UpdateOneAsync(card);
                    //     _logger.LogInformation($"CardImportFromExchangeAsync update success {card.Serial}");
                    // }
                }
                catch (Exception e)
                {
                    _logger.LogError($"batchCode {cards[0].BatchCode} CardImportFromExchangeAsync: {stockCard} error: {e}");
                    await session.AbortTransactionAsync();
                    return false;
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogInformation($"CardImportFromExchangeAsync: {stockCard}  error: {e}");
            return false;
        }
    }

    public async Task<List<Card>> CardExportForSaleAsync(int amount, string productCode, decimal cardValue,
        string stockCode,string transCode)
    {
        try
        {
            _logger.LogInformation($"{transCode} CardExportForSaleAsync request: {amount}-{productCode}-{cardValue}");
            using (var session =
                   await MongoDbContext.Client.StartSessionAsync(new ClientSessionOptions {CausalConsistency = true}))
            {
                try
                {
                    session.StartTransaction(new TransactionOptions(
                        ReadConcern.Majority,
                        writeConcern: WriteConcern.WMajority
                    ));
                    var options = new FindOptions<Card, Card>
                    {
                        Limit = amount,
                        Sort = Builders<Card>.Sort.Ascending(x => x.ExpiredDate)
                    };
                    Expression<Func<Card, bool>> query = p =>
                        p.Status == CardStatus.Active &&
                        p.ProductCode == productCode && // p.CardValue == cardValue &&
                        p.StockCode == stockCode;
                    // if (IsPriorityCardMapping())
                    // {
                    //     Expression<Func<Card, bool>> newQuery = p => p.BatchType == CardBatchType.CardSale;
                    //     query = query.And(newQuery);
                    // }
                    // else
                    // {
                    //     Expression<Func<Card, bool>> newQuery = p =>
                    //         p.BatchType == CardBatchType.CardSale || p.BatchType == CardBatchType.MappingCanSale;
                    //     query = query.And(newQuery);
                    // }

                    var search = await MongoDbContext.GetCollection<Card>().FindAsync(session, query, options);
                    var cards = await search.ToListAsync();
                    if (cards.Any() && cards.Count == amount)
                    {
                        var filterUpdate = Builders<Card>.Filter.In(x => x.Id, cards.Select(x => x.Id).ToList());
                        var update = await MongoDbContext.GetCollection<Card>().UpdateManyAsync(session,
                            filterUpdate,
                            Builders<Card>.Update.Set(p => p.ExportedDate, DateTime.Now)
                                .Set(p => p.Status, CardStatus.Exported)
                                .Set(p => p.TransCode, transCode),
                            new UpdateOptions {IsUpsert = false}
                        );
                        if (update.IsModifiedCountAvailable && update.ModifiedCount == cards.Count)
                        {
                            foreach (var item in cards)
                            {
                                item.CardCode = item.CardCode.DecryptTripleDes();
                                _logger.LogInformation(
                                    $"{transCode} CardExportForSaleAsync success {productCode}-{cardValue} serial: {item.Serial}");
                            }

                            _logger.LogInformation(
                                $"{transCode} CardExportForSaleAsync: {productCode}-{cardValue} success. Total card: {cards.Count}-ListCard: {cards.Select(x => x.Serial).ToJson()}");
                            await session.CommitTransactionAsync();
                            return cards;
                        }

                        await session.AbortTransactionAsync();
                        return null;
                    }

                    _logger.LogInformation($"{transCode} Card not available: {productCode}-{cardValue}-{amount}");
                    await session.AbortTransactionAsync();
                    return null;
                }
                catch (Exception e)
                {
                    _logger.LogInformation(
                        $"{transCode} CardExportForSaleAsync: {productCode}-{cardValue}-{amount}  error: {e}");
                    await session.AbortTransactionAsync();
                    Console.WriteLine(e);
                    return null;
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogInformation($"{transCode} CardExportForSaleAsync: {productCode}-{amount}  error. {e}");
            return null;
        }
    }


    public async Task<Card> CardGetCodeForUsingAsync(decimal cardValue, string productCode, string stockCode)
    {
        try
        {
            _logger.LogInformation($"CardGetCodeForUsingAsync request: {cardValue}-{productCode}-{stockCode}");
            using (var session =
                   await MongoDbContext.Client.StartSessionAsync(new ClientSessionOptions {CausalConsistency = true}))
            {
                var cards = MongoDbContext.GetCollection<Card>();
                try
                {
                    session.StartTransaction(new TransactionOptions(
                        ReadConcern.Majority,
                        writeConcern: WriteConcern.WMajority
                    ));

                    var sort = Builders<Card>.Sort.Ascending("ExpiredDate");

                    // var listFilter = new List<byte>
                    // {
                    //     (byte) CardBatchType.CardMapping, (byte) CardBatchType.MappingCanSale
                    // };
                    var filter = Builders<Card>.Filter.Eq("Status", CardStatus.Active)
                                 & Builders<Card>.Filter.Eq("ProductCode", productCode)
                                 & Builders<Card>.Filter.Eq("StockCode", stockCode)
                                 & Builders<Card>.Filter.Eq("CardValue", cardValue);

                    var options = new FindOneAndUpdateOptions<Card, Card>
                    {
                        IsUpsert = false,
                        ReturnDocument = ReturnDocument.After,
                        Sort = sort
                    };
                    var update = Builders<Card>.Update.Set("Status", CardStatus.Exported)
                        .Set("ExportedDate", DateTime.Now);

                    var result = await cards.FindOneAndUpdateAsync(session, filter, update, options);
                    if (result == null)
                    {
                        var query = Builders<Card>.Filter.Eq("Status", CardStatus.Active)
                                    & Builders<Card>.Filter.Eq("ProductCode", productCode)
                                    & Builders<Card>.Filter.Eq("StockCode", stockCode)
                                    & Builders<Card>.Filter.Lte("CardValue", cardValue);

                        result = await cards.FindOneAndUpdateAsync(session, query, update, options);

                        if (result == null)
                        {
                            await session.AbortTransactionAsync();
                            return null;
                        }
                    }

                    result.CardCode = result.CardCode.DecryptTripleDes();
                    await session.CommitTransactionAsync();
                    _logger.LogInformation(
                        $"CardGetCodeForUsingAsync {productCode}-{cardValue} success: {result.Serial}");
                    return result;
                }
                catch (Exception e)
                {
                    _logger.LogError($"CardGetCodeForUsingAsync error: {e}");
                    await session.AbortTransactionAsync();
                    Console.WriteLine(e);
                    return null;
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogInformation($"CardGetCodeForUsingAsync error: {e}");
            return null;
        }
    }

    public async Task<bool> CardBatchUpdateItemAsync(string batchCode, StockBatchItem item)
    {
        Expression<Func<StockBatch, bool>> query = p => p.BatchCode == batchCode;
        var search = await MongoDbContext.GetCollection<StockBatch>().FindAsync(query);
        var stockBatch = search.FirstOrDefault();
        var index = stockBatch.StockBatchItems.FindIndex(x => x.ProductCode == item.ProductCode);
        var updateCheck = await MongoDbContext.GetCollection<StockBatch>().UpdateManyAsync(
            x => x.BatchCode == batchCode,
            Builders<StockBatch>.Update.Set(x => x.StockBatchItems[index].Amount, item.Amount)
                .Set(x => x.StockBatchItems[index].QuantityImport, item.QuantityImport),
            new UpdateOptions {IsUpsert = false}
        );
        if (updateCheck.IsModifiedCountAvailable)
        {
            _logger.LogInformation($"{batchCode} - {item.Quantity} - CardBatchUpdateItemTransAsync DONE");
            return true;
        }

        _logger.LogError($"{batchCode} - {item.Quantity} - CardBatchUpdateItemTransAsync error");
        return false;
    }

    public async Task<bool> CardUpdateStatusListAsync(List<Card> cards, CardStatus status)
    {
        try
        {
            _logger.LogInformation(
                $"CardUpdateStatusListAsync request: Total: {cards.Count} - ListCard{cards.Select(x => x.Serial).ToJson()}-{status}");
            using (var session =
                   await MongoDbContext.Client.StartSessionAsync(new ClientSessionOptions {CausalConsistency = true}))
            {
                try
                {
                    session.StartTransaction(new TransactionOptions(
                        ReadConcern.Majority,
                        writeConcern: WriteConcern.WMajority
                    ));
                    var filterUpdate = Builders<Card>.Filter.In(x => x.Id, cards.Select(x => x.Id).ToList());
                    var update = await MongoDbContext.GetCollection<Card>().UpdateManyAsync(session,
                        filterUpdate,
                        Builders<Card>.Update.Set(p => p.Status, status),
                        new UpdateOptions {IsUpsert = false}
                    );

                    if (update.IsModifiedCountAvailable && update.ModifiedCount == cards.Count)
                    {
                        _logger.LogInformation(
                            $"CardUpdateStatusListAsync success. Total: {cards.Count}. Serial: {cards.Select(x => x.Serial).ToJson()}");
                        await session.CommitTransactionAsync();
                        return true;
                    }

                    _logger.LogError("CardUpdateStatusListAsync error");
                    await session.AbortTransactionAsync();
                    return false;
                }
                catch (Exception e)
                {
                    _logger.LogError($"CardUpdateStatusListAsync error: {e}");
                    await session.AbortTransactionAsync();
                    return false;
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogInformation($"CardUpdateStatusListAsync error: {e}");
            return false;
        }
    }


    public async Task<List<StockTransferItemInfo>> GetCardQuantityAvailableInStock(string stockCode, string batchCode,
        string categoryCode, string productCode)
    {
        try
        {
            Expression<Func<Card, bool>> query = p => p.Status == CardStatus.Active && p.StockCode == stockCode;
            if (!string.IsNullOrEmpty(batchCode))
            {
                Expression<Func<Card, bool>> newQuery = p => p.BatchCode == batchCode;
                query = query.And(newQuery);
            }

            if (!string.IsNullOrEmpty(categoryCode))
            {
                Expression<Func<Card, bool>> newQuery = p => p.CategoryCode == categoryCode;
                query = query.And(newQuery);
            }

            if (!string.IsNullOrEmpty(productCode))
            {
                Expression<Func<Card, bool>> newQuery = p => p.ProductCode == productCode;
                query = query.And(newQuery);
            }

            var data = await MongoDbContext.GetCollection<Card>()
                .Aggregate()
                .Match(query)
                .Group(k => k.ProductCode,
                    g => new StockTransferItemInfo
                    {
                        ServiceCode = g.First().ServiceCode,
                        CategoryCode = g.First().CategoryCode,
                        ProductCode = g.First().ProductCode,
                        CardValue = g.First().CardValue,
                        QuantityAvailable = g.Count()
                    }
                ).ToListAsync();
            return data;
        }
        catch (Exception e)
        {
            _logger.LogInformation($"CardUpdateStatusListAsync error: {e}");
            return new List<StockTransferItemInfo>();
        }
    }

    // public async Task<CardRequest> GetCardRequestForUsing(int cardValue, string stockType)
    // {
    //     try
    //     {
    //         _logger.LogInformation($"GetCardRequestForUsing request: {cardValue}-{stockType}");
    //         using (var session =
    //             await MongoDbContext.Client.StartSessionAsync(new ClientSessionOptions {CausalConsistency = true}))
    //         {
    //             var cards = MongoDbContext.GetCollection<CardRequest>();
    //             try
    //             {
    //                 session.StartTransaction(new TransactionOptions(
    //                     readConcern: ReadConcern.Majority,
    //                     writeConcern: WriteConcern.WMajority
    //                 ));
    //
    //                 var sort = Builders<CardRequest>.Sort.Ascending(x => x.CreatedTime);
    //
    //                 Expression<Func<CardRequest, bool>> filter = p =>
    //                     p.Status == CardRequestStatus.Init && p.Vendor == stockType && p.RequestValue == cardValue;
    //
    //                 var options = new FindOneAndUpdateOptions<CardRequest, CardRequest>
    //                 {
    //                     IsUpsert = false,
    //                     ReturnDocument = ReturnDocument.After,
    //                     Sort = sort
    //                 };
    //                 var update = Builders<CardRequest>.Update.Set(x => x.Status, CardRequestStatus.InProcessing);
    //                 var result = await cards.FindOneAndUpdateAsync(session, filter, update, options);
    //                 if (result == null)
    //                 {
    //                     var query = Builders<CardRequest>.Filter.Eq(x => x.Status, CardRequestStatus.Init)
    //                                 & Builders<CardRequest>.Filter.Eq(x => x.Vendor, stockType)
    //                                 & Builders<CardRequest>.Filter.Lte(x => x.RequestValue, cardValue);
    //                     result = await cards.FindOneAndUpdateAsync(session, query, update, options);
    //                 }
    //
    //                 if (result == null)
    //                 {
    //                     await session.AbortTransactionAsync();
    //                     return null;
    //                 }
    //
    //                 await session.CommitTransactionAsync();
    //                 _logger.LogInformation($"GetCardRequestForUsing {stockType}-{cardValue} success: {result.Serial}");
    //                 return result;
    //             }
    //             catch (Exception e)
    //             {
    //                 _logger.LogError($"GetCardRequestForUsing error: {e}");
    //                 await session.AbortTransactionAsync();
    //                 Console.WriteLine(e);
    //                 return null;
    //             }
    //         }
    //     }
    //     catch (Exception e)
    //     {
    //         _logger.LogInformation($"CardGetCodeForUsingAsync error: {e}");
    //         return null;
    //     }
    // }
    //
    // public async Task<CardRequest> GetCardRequestTimeOut(int cardValue, string stockType)
    // {
    //     try
    //     {
    //         _logger.LogInformation($"GetCardRequestTimeOut request: {cardValue}-{stockType}");
    //         using (var session =
    //             await MongoDbContext.Client.StartSessionAsync(new ClientSessionOptions {CausalConsistency = true}))
    //         {
    //             var cards = MongoDbContext.GetCollection<CardRequest>();
    //             try
    //             {
    //                 session.StartTransaction(new TransactionOptions(
    //                     readConcern: ReadConcern.Majority,
    //                     writeConcern: WriteConcern.WMajority
    //                 ));
    //
    //                 var sort = Builders<CardRequest>.Sort.Ascending(x => x.CreatedTime);
    //
    //                 Expression<Func<CardRequest, bool>> filter = p =>
    //                     p.Status == CardRequestStatus.Init && p.Vendor == stockType;
    //                 if (cardValue > 0)
    //                 {
    //                     Expression<Func<CardRequest, bool>> newQuery = p => p.RequestValue == cardValue;
    //                     filter = filter.And(newQuery);
    //                 }
    //
    //                 var options = new FindOneAndUpdateOptions<CardRequest, CardRequest>
    //                 {
    //                     IsUpsert = false,
    //                     ReturnDocument = ReturnDocument.After,
    //                     Sort = sort
    //                 };
    //                 var update = Builders<CardRequest>.Update.Set(x => x.Status, CardRequestStatus.InProcessing);
    //                 var result = await cards.FindOneAndUpdateAsync(session, filter, update, options);
    //                 if (result == null)
    //                 {
    //                     await session.AbortTransactionAsync();
    //                     return null;
    //                 }
    //
    //                 //Lấy các thẻ timeout để nạp vào tkc
    //                 if (result.CreatedTime.AddMinutes(ConfigCardTimeout()) <= DateTime.UtcNow)
    //                 {
    //                     await session.CommitTransactionAsync();
    //                     _logger.LogInformation($"GetCardRequestTimeOut {stockType}-{cardValue} success: {result.Serial}");
    //                     return result;
    //                 }
    //
    //                 await session.AbortTransactionAsync();
    //                 return null;
    //             }
    //             catch (Exception e)
    //             {
    //                 _logger.LogError($"GetCardRequestTimeOut error: {e}");
    //                 await session.AbortTransactionAsync();
    //                 Console.WriteLine(e);
    //                 return null;
    //             }
    //         }
    //     }
    //     catch (Exception e)
    //     {
    //         _logger.LogInformation($"GetCardRequestTimeOut error: {e}");
    //         return null;
    //     }
    // }

    // private bool IsPriorityCardMapping()
    // {
    //     try
    //     {
    //         return false;
    //         //var checkPriority = _cacheManager.GetCache(PaygateConst.PriorityGetCardSaleMappingKey);
    //         //return !string.IsNullOrEmpty(checkPriority) && checkPriority == "1";
    //     }
    //     catch (Exception e)
    //     {
    //         _logger.LogError($"IsPriorityCardMapping error: {e}");
    //         return false;
    //     }
    // }

    private int ConfigCardTimeout()
    {
        try
        {
            return 1;
            //var checkPriority = _cacheManager.GetCache(PaygateConst.PriorityGetCardSaleMappingKey);
            //return !string.IsNullOrEmpty(checkPriority) && checkPriority == "1";
        }
        catch (Exception e)
        {
            _logger.LogError($"ConfigCardTimeout error: {e}");
            return 0;
        }
    }

    public async Task<bool> CardBatchUpdateItemTransAsync(string batchCode, StockBatchItem item)
    {
        try
        {
            _logger.LogInformation($"CardBatchUpdateItemTransAsync request: {batchCode}");
            using (var session =
                   await MongoDbContext.Client.StartSessionAsync())
            {
                try
                {
                    session.StartTransaction(new TransactionOptions(
                        ReadConcern.Majority,
                        writeConcern: WriteConcern.WMajority
                    ));
                    Expression<Func<StockBatch, bool>> query = p => p.BatchCode == batchCode;
                    var search = await MongoDbContext.GetCollection<StockBatch>().FindAsync(session, query);
                    var stockBatch = search.FirstOrDefault();
                    var index = stockBatch.StockBatchItems.FindIndex(x => x.ProductCode == item.ProductCode);
                    var updateCheck = await MongoDbContext.GetCollection<StockBatch>().UpdateManyAsync(session,
                        x => x.BatchCode == batchCode,
                        Builders<StockBatch>.Update.Set(x => x.StockBatchItems[index].Amount, item.Amount)
                            .Set(x => x.StockBatchItems[index].QuantityImport, item.QuantityImport),
                        //Builders<StockBatchItem>.Update.Set(p => p.QuantityImport, item.QuantityImport)
                        //    .Set(p => p.Amount, item.Amount),
                        new UpdateOptions {IsUpsert = false}
                    );
                    if (updateCheck.IsModifiedCountAvailable)
                    {
                        _logger.LogInformation(
                            "CardBatchUpdateItemTransAsync DONE");
                        await session.CommitTransactionAsync();
                        return true;
                    }

                    _logger.LogError("CardBatchUpdateItemTransAsync error");
                    await session.AbortTransactionAsync();
                    return false;
                }
                catch (Exception e)
                {
                    _logger.LogError($"CardBatchUpdateItemTransAsync error: {e}");
                    await session.AbortTransactionAsync();
                    return false;
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError($"CardInsertListAsync process error: {e}");
            return false;
        }
    }
}