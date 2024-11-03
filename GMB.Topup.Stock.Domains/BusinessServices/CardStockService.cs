using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using GMB.Topup.Shared;
using GMB.Topup.Shared.Helpers;
using GMB.Topup.Stock.Contracts.ApiRequests;
using GMB.Topup.Stock.Contracts.Dtos;
using GMB.Topup.Stock.Contracts.Enums;
using GMB.Topup.Stock.Domains.Entities;
using GMB.Topup.Stock.Domains.Repositories;
using Microsoft.Extensions.Logging;
using ServiceStack;

namespace GMB.Topup.Stock.Domains.BusinessServices;

public class CardStockService : BusinessServiceBase, ICardStockService
{
    private readonly ICardMongoRepository _cardMongoRepository;
    private readonly IDateTimeHelper _dateTimeHelper;

    //private readonly Logger _logger = LogManager.GetLogger("CardStockService");
    private readonly ILogger<CardStockService> _logger;

    public CardStockService(ICardMongoRepository cardMongoRepository,
        IDateTimeHelper dateTimeHelper,
        ILogger<CardStockService> logger)
    {
        _cardMongoRepository = cardMongoRepository;
        _dateTimeHelper = dateTimeHelper;
        _logger = logger;
    }

    public async Task<bool> CardExchangeAsync(int amount, string productCode, string srcStockCode,
        string desStockCode)
    {
        var cards = await _cardMongoRepository.GetSortedPaginatedAsync<Card, Guid>(
            p => p.Status == CardStatus.Active && p.ProductCode == productCode && p.StockCode == srcStockCode, s =>
                new
                {
                    s.ExpiredDate,
                    s.ImportedDate
                }, true, 0, amount);

        if (cards.Count < amount)
        {
            _logger.LogInformation("CardTake is not enough");
            return false;
        }

        foreach (var card in cards)
        {
            card.StockCode = desStockCode;
            await _cardMongoRepository.UpdateOneAsync(card);
        }

        // Parallel.ForEach(cards, async (card) =>
        // {
        //     card.StockCode = desStockCode;
        //     await _cardMongoRepository.UpdateOneAsync(card);
        // });
        return true;
    }

    public async Task<bool> CardExchangeBatchAsync(string batchCode, string desStockCode)
    {
        var cards = await _cardMongoRepository.GetAllAsync<Card>(p => p.BatchCode == batchCode);
        foreach (var card in cards)
        {
            card.StockCode = desStockCode;
            await _cardMongoRepository.UpdateOneAsync(card);
        }

        // Parallel.ForEach(cards, async (card) =>
        // {
        //     card.StockCode = desStockCode;
        //     await _cardMongoRepository.UpdateOneAsync(card);
        // });
        return true;
    }


    public async Task<List<CardDto>> CardExportForExchangeAsync(int amount, CardStatus status, string stockType,
        decimal cardValue, string stockCode, string batchCode = null)
    {
        var cards = await _cardMongoRepository.CardExportForExchangeAsync(amount, status, stockType, cardValue,
            stockCode, batchCode);
        // var cards = await _cardMongoRepository.GetSortedPaginatedAsync<Card, Guid>(
        //     p => p.Status == status && p.Vendor == stockType && p.CardValue == cardValue && p.StockCode == stockCode,
        //     s => s.ExpiredDate, true, 0, amount);

        if (cards == null || cards.Count < amount)
        {
            _logger.LogInformation("CardTake is not enough");
            return null;
        }

        _logger.LogInformation(
            $"CardExportForExchangeAsync: {stockCode}-{stockType}-{amount}  success. Total card: {cards.Count}");
        return cards.ConvertTo<List<CardDto>>();
    }

    /// <summary>
    /// </summary>
    /// <param name="cardDtos"></param>
    /// <param name="status"></param>
    /// <param name="stockCard"></param>
    /// <returns></returns>
    public async Task<bool> CardImportFromExchangeAsync(List<CardDto> cardDtos, CardStatus status, string stockCard)
    {
        try
        {
            _logger.LogInformation($"{cardDtos[0].BatchCode} CardImportFromExchangeAsync");
            var cards = cardDtos.ConvertTo<List<Card>>();
            var rs = await _cardMongoRepository.CardImportFromExchangeAsync(cards, status, stockCard);
            _logger.LogInformation($"{cardDtos[0].BatchCode} CardImportFromExchangeAsync return: {rs}");
            return rs;
        }
        catch (Exception e)
        {
            _logger.LogInformation($"CardImportFromExchangeAsync: {stockCard}  error: {e}");
            return false;
        }
    }


    public async Task<List<CardDto>> CardExportForSaleAsync(int amount, string productCode, decimal cardValue,
        string stockCode, string transCode)
    {
        try
        {
            var cards = await _cardMongoRepository.CardExportForSaleAsync(amount, productCode, cardValue,
                stockCode, transCode);
            if (cards == null || cards.Count < amount)
            {
                _logger.LogInformation($"{transCode} CardTake is not enough");
                return null;
            }

            _logger.LogInformation(
                $"{transCode} CardExportForSaleAsync: {productCode}-{amount}  success. Total card: {cards.Count}");
            return cards.ConvertTo<List<CardDto>>();
        }
        catch (Exception e)
        {
            _logger.LogInformation($"{transCode} CardExportForSaleAsync: {productCode}-{amount}  error. {e}");
            return null;
        }
    }

    public async Task StockInsertAsync(StockDto stock)
    {
        await _cardMongoRepository.AddOneAsync(stock
            .ConvertTo<Entities.Stock>());
    }

    /// <summary>
    /// </summary>
    /// <param name="stockCode">Mã kho</param>
    /// <param name="keyCode">Kho max thẻ thì là ProductCode, kho Airtime thì là CategoryCode</param>
    /// <returns></returns>
    public async Task<StockDto> StockGetAsync(string stockCode, string keyCode)
    {
        var stock = await _cardMongoRepository.GetOneAsync<Entities.Stock>(p =>
            p.StockCode == stockCode && p.KeyCode == keyCode);

        return stock?.ConvertTo<StockDto>();
    }

    public async Task StockUpdateAsync(StockDto stock)
    {
        await _cardMongoRepository.UpdateOneAsync(stock
            .ConvertTo<Entities.Stock>());
    }

    public async Task<bool> StockUpdateInventoryAsync(string stockCode, string keyCode, int amount)
    {
        var stock = await _cardMongoRepository.GetOneAsync<Entities.Stock>(p =>
            p.StockCode == stockCode && p.KeyCode == keyCode);

        if (stock == null)
            return false;
        stock.Inventory += amount;
        return await _cardMongoRepository.UpdateOneAsync(stock);
    }

    public async Task<bool> StockSetInventoryAsync(string stockCode, string keyCode, int inventory)
    {
        var stock = await _cardMongoRepository.GetOneAsync<Entities.Stock>(p =>
            p.StockCode == stockCode && p.KeyCode == keyCode);

        if (stock == null)
            return false;
        stock.Inventory = inventory;
        return await _cardMongoRepository.UpdateOneAsync(stock);
    }

    public async Task<bool> CardUpdateStatusListAsync(List<CardDto> cards, CardStatus status)
    {
        return await _cardMongoRepository.CardUpdateStatusListAsync(cards.ConvertTo<List<Card>>(), status);
    }

    public async Task<List<StockDto>> StockGetListAsync(CardStockGetListRequest cardStockGetListRequest)
    {
        List<Entities.Stock> list;
        if (!string.IsNullOrEmpty(cardStockGetListRequest.StockCode))
            list = await _cardMongoRepository.GetPaginatedAsync<Entities.Stock>(
                p => p.StockCode == cardStockGetListRequest.StockCode,
                cardStockGetListRequest.Offset, cardStockGetListRequest.Limit);
        else
            list = await _cardMongoRepository.GetPaginatedAsync<Entities.Stock>(
                p => p.StockCode != string.Empty,
                cardStockGetListRequest.Offset, cardStockGetListRequest.Limit);

        if (list.Any())
            return list.ConvertTo<List<StockDto>>(); //.Select(p => p.CardStock.ConvertTo<CardStockDto>()).ToList();

        return new List<StockDto>();
    }

    public async Task<MessagePagedResponseBase> StockGetPagedAsync(CardStockGetListRequest request)
    {
        Expression<Func<Entities.Stock, bool>> query = p =>
            p.Status != (byte)CardStockStatus.Delete &&
            p.StockType == "PINCODE"; // keyCode.Split('_').Length > 1 ? "PINCODE" : "AIRTIME;
        if (!string.IsNullOrEmpty(request.Filter))
        {
            Expression<Func<Entities.Stock, bool>> newQuery = p => p.StockCode.Contains(request.Filter) ||
                                                                   p.ServiceCode.Contains(request.Filter) ||
                                                                   p.CategoryCode.Contains(request.Filter) ||
                                                                   p.KeyCode.Contains(request.Filter);
            query = query.And(newQuery);
        }

        if (!string.IsNullOrEmpty(request.StockCode))
        {
            Expression<Func<Entities.Stock, bool>> newQuery = p => p.StockCode == request.StockCode;
            query = query.And(newQuery);
        }

        if (!string.IsNullOrEmpty(request.ServiceCode))
        {
            Expression<Func<Entities.Stock, bool>> newQuery = p => p.ServiceCode == request.ServiceCode;
            query = query.And(newQuery);
        }

        if (!string.IsNullOrEmpty(request.CategoryCode))
        {
            Expression<Func<Entities.Stock, bool>> newQuery = p => p.CategoryCode == request.CategoryCode;
            query = query.And(newQuery);
        }

        if (!string.IsNullOrEmpty(request.ProductCode))
        {
            Expression<Func<Entities.Stock, bool>> newQuery = p => p.KeyCode == request.ProductCode;
            query = query.And(newQuery);
        }

        if (request.MinCardValue > 0)
        {
            Expression<Func<Entities.Stock, bool>> newQuery = p => p.ItemValue >= request.MinCardValue;
            query = query.And(newQuery);
        }

        if (request.MaxCardValue > 0)
        {
            Expression<Func<Entities.Stock, bool>> newQuery = p => p.ItemValue <= request.MaxCardValue;
            query = query.And(newQuery);
        }

        if (request.Status != (byte)CardStockStatus.Undefined)
        {
            Expression<Func<Entities.Stock, bool>> newQuery = p => p.Status == request.Status;
            query = query.And(newQuery);
        }

        var total = await _cardMongoRepository.CountAsync(query);

        if (request.SearchType == SearchType.Export)
        {
            request.Offset = 0;
            request.Limit = int.MaxValue;
        }

        var listSum = _cardMongoRepository.GetAll(query);

        var totalSum = new StockDto
        {
            Inventory = listSum.Sum(c => c.Inventory)
        };
        var lst = await _cardMongoRepository.GetSortedPaginatedAsync(query,
            s => s.StockCode, false,
            request.Offset, request.Limit);
        var stocks = lst
            // .OrderBy(x => x.Vendor)
            //.OrderBy(x => x.ItemValue)
            .OrderBy(x => x.StockCode)
            .ThenBy(x => x.ItemValue);
        return new MessagePagedResponseBase
        {
            ResponseCode = "01",
            ResponseMessage = "Thành công",
            Total = (int)total,
            SumData = totalSum,
            Payload = stocks.ConvertTo<List<StockDto>>()
        };
    }

    public async Task<int> StockGetListCountAsync(CardStockGetListRequest cardStockGetListRequest)
    {
        long count = 0;
        if (!string.IsNullOrEmpty(cardStockGetListRequest.StockCode))
            count = await _cardMongoRepository.CountAsync<Entities.Stock>(p =>
                p.StockCode == cardStockGetListRequest.StockCode);
        else
            count = await _cardMongoRepository.CountAsync<Entities.Stock>(p =>
                p.StockCode != string.Empty);

        return (int)count;
    }

    /// <summary>
    /// Lấy danh sách chi tiết giao dịch nhập về kho thông qua hình thức lấy thẻ API
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public async Task<MessagePagedResponseBase> CardStockTransListAsync(CardStockTransListRequest input)
    {
        try
        {
            Expression<Func<StockTransRequest, bool>> query = p => true;

            if (!string.IsNullOrEmpty(input.Provider))
            {
                Expression<Func<StockTransRequest, bool>> newQuery = p => p.Provider == input.Provider;
                query = query.And(newQuery);
            }

            if (!string.IsNullOrEmpty(input.TransCode))
            {
                Expression<Func<StockTransRequest, bool>> newQuery = p => p.TransCode == input.TransCode;
                query = query.And(newQuery);
            }

            if (!string.IsNullOrEmpty(input.TransCodeProvider))
            {
                Expression<Func<StockTransRequest, bool>>
                    newQuery = p => p.TransCodeProvider == input.TransCodeProvider;
                query = query.And(newQuery);
            }

            if (!string.IsNullOrEmpty(input.CategoryCode))
            {
                Expression<Func<StockTransRequest, bool>> newQuery = p => p.ServiceCode.Contains(input.CategoryCode);
                query = query.And(newQuery);
            }

            if (!string.IsNullOrEmpty(input.ProductCode))
            {
                Expression<Func<StockTransRequest, bool>> newQuery = p => p.ProductCode.Contains(input.ProductCode);
                query = query.And(newQuery);
            }

            if (!string.IsNullOrEmpty(input.BatchCode))
            {
                Expression<Func<StockTransRequest, bool>> newQuery = p => p.BatchCode == input.BatchCode;
                query = query.And(newQuery);
            }

            if (input.Status != StockBatchStatus.Undefined)
            {
                Expression<Func<StockTransRequest, bool>> newQuery = p => p.Status == input.Status;
                query = query.And(newQuery);
            }


            if (input.FromDate.HasValue)
            {
                Expression<Func<StockTransRequest, bool>> newQuery = p =>
                    p.CreatedDate >= input.FromDate.Value.ToUniversalTime();
                query = query.And(newQuery);
            }

            if (input.ToDate.HasValue)
            {
                var date = input.ToDate.Value.Date.AddDays(1).AddSeconds(-1).ToUniversalTime();
                Expression<Func<StockTransRequest, bool>> newQuery = p => p.CreatedDate <= date;
                query = query.And(newQuery);
            }

            var total = await _cardMongoRepository.CountAsync(query);
            var list = await _cardMongoRepository.GetSortedPaginatedAsync<StockTransRequest, Guid>(query,
                s => s.CreatedDate, false,
                input.Offset, input.Limit);


            var data = list.Select(x =>
            {
                var s = x.ConvertTo<StockTransRequestDto>();
                s.CreatedDate = _dateTimeHelper.ConvertToUserTime(x.CreatedDate, DateTimeKind.Utc);
                if(x.ExpiredDate!=null)
                    s.ExpiredDate = _dateTimeHelper.ConvertToUserTime(x.ExpiredDate.Value, DateTimeKind.Utc);
                return s;
            }).ToList();

            return new MessagePagedResponseBase
            {
                ResponseCode = "01",
                ResponseMessage = "Thành công",
                Total = (int)total,
                Payload = data
            };
        }
        catch (Exception ex)
        {
            _logger.LogError("CardStockTransListAsync error: " + ex.Message);
            return new MessagePagedResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Thất bại",
                Total = 0
            };
        }
    }
}