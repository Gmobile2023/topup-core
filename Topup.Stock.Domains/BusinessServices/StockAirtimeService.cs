using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Topup.Shared;
using Topup.Shared.Helpers;
using Topup.Stock.Contracts.ApiRequests;
using Topup.Stock.Contracts.Dtos;
using Topup.Stock.Contracts.Enums;
using Microsoft.Extensions.Logging;
using ServiceStack;
using Topup.Stock.Domains.Entities;
using Topup.Stock.Domains.Repositories;

namespace Topup.Stock.Domains.BusinessServices;

public interface IStockAirtimeBusinessServices
{
    // search
    Task<MessagePagedResponseBase> GetAllStockAirtimePagedAsync(GetAllStockAirtimeRequest request);
    Task<StockDto> StockGetAsync(string providerCode);
    Task StockUpdateAsync(StockDto dto);
    Task<StockBatchDto> BatchInsertAsync(StockBatchDto dto);
    Task BatchUpdateAsync(StockBatchDto dto);
    Task BatchDeleteAsync(string code);


    Task<MessagePagedResponseBase> GetAllBatchAirtimePagedAsync(GetAllBatchAirtimeRequest request);
}

public class StockAirtimeBusinessServices : BusinessServiceBase, IStockAirtimeBusinessServices
{
    private readonly ICardMongoRepository _cardMongoRepository;

    private readonly IDateTimeHelper _dateTimeHelper;

    //private readonly Logger _logger = LogManager.GetLogger("StockAirtimeService");
    private readonly ILogger<StockAirtimeBusinessServices> _logger;


    public StockAirtimeBusinessServices(ICardMongoRepository cardMongoRepository, IDateTimeHelper dateTimeHelper,
        ILogger<StockAirtimeBusinessServices> logger)
    {
        _cardMongoRepository = cardMongoRepository;
        _logger = logger;
        _dateTimeHelper = dateTimeHelper;
    }

    public async Task<MessagePagedResponseBase> GetAllStockAirtimePagedAsync(GetAllStockAirtimeRequest request)
    {
        _logger.LogInformation("GetStockAirtimePagedAsync: " + request.ToJson());
        Expression<Func<Entities.Stock, bool>> query = p =>
            p.Status != (byte) CardStockStatus.Delete &&
            p.StockType == "AIRTIME"; // keyCode.Split('_').Length > 1 ? "PINCODE" : "AIRTIME;
        if (!string.IsNullOrEmpty(request.Filter))
        {
            Expression<Func<Entities.Stock, bool>> newQuery = p => p.StockCode.Contains(request.Filter) ||
                                                                   p.KeyCode.Contains(request.Filter);
            query = query.And(newQuery);
        }

        // if (!string.IsNullOrEmpty(request.StockCode))
        // {
        //     Expression<Func<Entities.Stock, bool>> newQuery = p => p.StockCode == request.StockCode;
        //     query = query.And(newQuery);
        // }
        if (!string.IsNullOrEmpty(request.ProviderCode))
        {
            Expression<Func<Entities.Stock, bool>> newQuery = p => p.KeyCode == request.ProviderCode;
            query = query.And(newQuery);
        }

        if (request.Status != (byte) CardStockStatus.Undefined)
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

        var lst = await _cardMongoRepository.GetSortedPaginatedAsync(query,
            s => s.StockCode, false,
            request.Offset, request.Limit);
        var stocks = lst
            .OrderBy(x => x.StockCode)
            .ThenBy(x => x.ItemValue);
        _logger.LogInformation("GetAllStockAirtimePagedAsync: total " + total);
        return new MessagePagedResponseBase
        {
            ResponseCode = ResponseCodeConst.Success,
            ResponseMessage = "Thành công",
            Total = (int) total,
            Payload = stocks.ConvertTo<List<StockDto>>()
        };
    }

    public async Task<StockDto> StockGetAsync(string providerCode)
    {
        var stock = await _cardMongoRepository.GetOneAsync<Entities.Stock>(p =>
            p.StockType == "AIRTIME" && p.KeyCode == providerCode);
        return stock?.ConvertTo<StockDto>();
    }

    public async Task StockUpdateAsync(StockDto dto)
    {
        await _cardMongoRepository.UpdateOneAsync(dto.ConvertTo<Entities.Stock>());
    }

    public async Task<StockBatchDto> BatchInsertAsync(StockBatchDto dto)
    {
        var cardBatch = await _cardMongoRepository.GetOneAsync<StockBatch>(p =>
            p.BatchCode == dto.BatchCode);
        if (cardBatch != null) return cardBatch.ConvertTo<StockBatchDto>();
        cardBatch = dto.ConvertTo<StockBatch>();
        if (dto.Status == StockBatchStatus.Undefined) cardBatch.Status = StockBatchStatus.Init;
        cardBatch.CreatedDate = DateTime.Now;

        if (dto.StockBatchItems == null)
            cardBatch.StockBatchItems = new List<StockBatchItem>();
        else
            cardBatch.StockBatchItems = cardBatch.StockBatchItems;
        try
        {
            await _cardMongoRepository.AddOneAsync(cardBatch);
            return dto;
        }
        catch (Exception ex)
        {
            _logger.LogError("CardBatch insert error: " + ex.Message);
            return null;
        }
    }

    public async Task BatchUpdateAsync(StockBatchDto dto)
    {
        await _cardMongoRepository.UpdateOneAsync(dto.ConvertTo<StockBatch>());
    }

    public async Task BatchDeleteAsync(string code)
    {
        var data = await _cardMongoRepository.GetOneAsync<StockBatch>(p =>
            p.BatchCode == code && p.ImportType == "AIRTIME");
        if (data.Status != StockBatchStatus.Active)
            await _cardMongoRepository.DeleteOneAsync(data);
    }

    public async Task<MessagePagedResponseBase> GetAllBatchAirtimePagedAsync(GetAllBatchAirtimeRequest request)
    {
        _logger.LogInformation("GetAllBatchAirtimePagedAsync: " + request.ToJson());
        Expression<Func<StockBatch, bool>> query = p => p.ImportType == "AIRTIME";
        if (!string.IsNullOrEmpty(request.Filter))
        {
            Expression<Func<StockBatch, bool>> newQuery = p => p.BatchCode.Contains(request.Filter) ||
                                                               p.Provider.Contains(request.Filter);
            query = query.And(newQuery);
        }

        if (!string.IsNullOrEmpty(request.BatchCode))
        {
            Expression<Func<StockBatch, bool>> newQuery = p => p.BatchCode.Contains(request.BatchCode);
            query = query.And(newQuery);
        }

        if (!string.IsNullOrEmpty(request.ProviderCode))
        {
            Expression<Func<StockBatch, bool>> newQuery = p => p.Provider.Contains(request.ProviderCode);
            query = query.And(newQuery);
        }

        if (request.Status != (byte) StockBatchStatus.Undefined)
        {
            Expression<Func<StockBatch, bool>> newQuery = p =>
                p.Status == (StockBatchStatus) request.Status;
            query = query.And(newQuery);
        }

        if (request.FormDate.HasValue)
        {
            Expression<Func<StockBatch, bool>> newQuery = p => p.CreatedDate >= request.FormDate;
            query = query.And(newQuery);
        }

        if (request.ToDate.HasValue)
        {
            Expression<Func<StockBatch, bool>> newQuery = p => p.CreatedDate <= request.ToDate;
            query = query.And(newQuery);
        }

        var total = await _cardMongoRepository.CountAsync(query);
        if (request.SearchType == SearchType.Export)
        {
            request.Offset = 0;
            request.Limit = int.MaxValue;
        }

        var lst = await _cardMongoRepository.GetSortedPaginatedAsync(query,
            s => s.BatchCode, false,
            request.Offset, request.Limit);
        var stocks = lst
            .OrderByDescending(x => x.CreatedDate);
        _logger.LogInformation("GetAllBatchAirtimePagedAsync: total " + total);


        var data = stocks.Select(x =>
        {
            var s = x.ConvertTo<StockBatchDto>();
            s.CreatedDate = _dateTimeHelper.ConvertToUserTime(x.CreatedDate, DateTimeKind.Utc);
            if (x.ModifiedDate.HasValue)
                s.ModifiedDate = _dateTimeHelper.ConvertToUserTime(x.ModifiedDate.Value, DateTimeKind.Utc);
            return s;
        }).ToList();

        return new MessagePagedResponseBase
        {
            ResponseCode = ResponseCodeConst.Success,
            ResponseMessage = "Thành công",
            Total = (int) total,
            Payload = data
        };
    }
}