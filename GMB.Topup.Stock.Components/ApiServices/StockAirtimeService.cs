using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GMB.Topup.Gw.Model.Commands.Stock;
using GMB.Topup.Gw.Model.Events.Stock;
using GMB.Topup.Shared;
using GMB.Topup.Stock.Contracts.ApiRequests;
using GMB.Topup.Stock.Contracts.Dtos;
using GMB.Topup.Stock.Contracts.Enums;
using GMB.Topup.Stock.Domains.BusinessServices;
using MassTransit;
using Microsoft.Extensions.Logging;
using ServiceStack;

namespace GMB.Topup.Stock.Components.ApiServices;

public class StockAirtimeService : Service
{
    private readonly IBus _bus;
    private readonly ICardService _cardService;
    private readonly ILogger<StockAirtimeService> _logger;

    private readonly IRequestClient<StockAirtimeInventoryCommand> _requestClient;
    private readonly IStockAirtimeService _stockAirtimeService;

    public StockAirtimeService(ICardService cardService,
        IStockAirtimeService stockAirtimeService,
        IBus bus, IRequestClient<StockAirtimeInventoryCommand> requestClient, ILogger<StockAirtimeService> logger)
    {
        _cardService = cardService;
        _stockAirtimeService = stockAirtimeService;
        _bus = bus;
        _requestClient = requestClient;
        _logger = logger;
    }

    #region kho Airtime

    /// <summary>
    ///     danh sach kho Airtime
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public async Task<object> GetAsync(GetAllStockAirtimeRequest request)
    {
        return await _stockAirtimeService.GetAllStockAirtimePagedAsync(request);
    }

    /// <summary>
    ///     get kho Airtime by provider code
    /// </summary>
    /// <param name="request"></param>
    /// <param name="stockAirtimeRequest"></param>
    /// <returns></returns>
    public async Task<object> GetAsync(GetStockAirtimeRequest stockAirtimeRequest)
    {
        var stock = await _stockAirtimeService.StockGetAsync(stockAirtimeRequest.ProviderCode);
        return MessageResponseBase.Success(stock);
    }

    /// <summary>
    ///     tao kho Airtime
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public async Task<MessageResponseBase> PostAsync(CreateStockAirtimeRequest request)
    {
        _logger.LogInformation($"CreateStockAirtimeRequest request:{request.ToJson()}");
        if (string.IsNullOrEmpty(request.ProviderCode))
            return MessageResponseBase.Error("Lỗi không có thông tin nhà cung cấp!");
        var stock = await _stockAirtimeService.StockGetAsync(request.ProviderCode);
        if (null == stock)
        {
            var result = await _requestClient.GetResponse<MessageResponseBase>(new
            {
                StockCode = StockCodeConst.STOCK_SALE, request.ProviderCode
            }, CancellationToken.None, RequestTimeout.After(s: 10));
            if (result.Message.ResponseCode == ResponseCodeConst.Success)
                stock = await _stockAirtimeService.StockGetAsync(request.ProviderCode);
            if (stock == null)
                return result.Message;
        }
        else
        {
            return MessageResponseBase.Error($"Stock with provider code {request.ProviderCode} exist!");
        }

        stock.Status = request.Status;
        stock.InventoryLimit = (int) request.MaxLimitAirtime;
        stock.MinimumInventoryLimit = (int) request.MinLimitAirtime;
        stock.ItemValue = 0;
        stock.Description = request.Description;
        await _stockAirtimeService.StockUpdateAsync(stock);
        return new MessageResponseBase
        {
            ResponseCode = "01",
            ResponseMessage = "Success"
        };
    }

    /// <summary>
    ///     chinh sua kho Airtime
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public async Task<MessageResponseBase> PutAsync(UpdateStockAirtimeRequest request)
    {
        _logger.LogInformation($"UpdateStockAirtimeRequest request:{request.ToJson()}");
        if (string.IsNullOrEmpty(request.ProviderCode))
            return MessageResponseBase.Error("Lỗi không có thông tin nhà cung cấp!");
        var stock = await _stockAirtimeService.StockGetAsync(request.ProviderCode);
        if (null == stock)
            return MessageResponseBase.Error($"Stock with provider code {request.ProviderCode} does not exist!");
        stock.Status = request.Status;
        stock.InventoryLimit = (int) request.MaxLimitAirtime;
        stock.MinimumInventoryLimit = (int) request.MinLimitAirtime;
        stock.ItemValue = 0;
        stock.Description = request.Description;
        await _stockAirtimeService.StockUpdateAsync(stock);
        return new MessageResponseBase
        {
            ResponseCode = "01",
            ResponseMessage = "Success"
        };
    }

    #endregion

    #region Batch Airtime

    /// <summary>
    ///     danh sach BatchAirtime
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public async Task<object> GetAsync(GetAllBatchAirtimeRequest request)
    {
        return await _stockAirtimeService.GetAllBatchAirtimePagedAsync(request);
    }

    /// <summary>
    ///     Get BatchAirtime
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public async Task<MessageResponseBase> GetAsync(GetBatchAirtimeRequest request)
    {
        var batch = await _cardService.StockBatchGetAsync(new Guid(), request.BatchCode);
        if (batch == null)
            return MessageResponseBase.Error($"Batch {request.BatchCode} does not exist");
        return MessageResponseBase.Success(batch);
    }

    /// <summary>
    ///     create BatchAirtime
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public async Task<MessageResponseBase> PostAsync(CreateBatchAirtimeRequest request)
    {
        if (string.IsNullOrEmpty(request.ProviderCode))
            return MessageResponseBase.Error("Lỗi không có thông tin nhà cung cấp!");
        if (request.Airtime == 0)
            return MessageResponseBase.Error("Lỗi số tiền GD phải lớn hơn 0đ!");
        var dateLog = $"{DateTime.Now:yyyyMMddHHmmssfff}";
        var batch = new StockBatchDto
        {
            Id = NewId.NextGuid(),
            ImportType = "AIRTIME",
            BatchCode = $"{request.ProviderCode}_Airtime_{dateLog}",
            Description = request.Description,
            Provider = request.ProviderCode,
            Status = (StockBatchStatus) request.Status,
            Amount = request.Amount,
            Discount = request.Discount,
            TotalValue = request.Airtime,

            CreatedDate = DateTime.Now,
            CreatedAccount = request.CreatedAccount
        };
        batch.StockBatchItems = new List<StockBatchItemDto>();
        batch.StockBatchItems.Add(new StockBatchItemDto
        {
            Amount = request.Amount,
            Airtime = request.Airtime,
            Discount = request.Discount,
            ProductCode = request.ProviderCode,
            ItemValue = 1,
            Quantity = 1
        });

        batch = await _stockAirtimeService.BatchInsertAsync(batch);
        if (null == batch)
            return MessageResponseBase.Error("Batch create error");

        await ProcessAirtimeImportAsync(batch);

        return MessageResponseBase.Success(batch);
    }

    /// <summary>
    ///     update BatchAirtime
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public async Task<MessageResponseBase> PutAsync(UpdateBatchAirtimeRequest request)
    {
        if (string.IsNullOrEmpty(request.BatchCode))
            return MessageResponseBase.Error($"Batch {request.BatchCode} does not exist");

        var batch = await _cardService.StockBatchGetAsync(new Guid(), request.BatchCode);
        if (batch == null)
            return MessageResponseBase.Error($"Batch {request.BatchCode} does not exist");
        if (batch.Status == StockBatchStatus.Active)
            return MessageResponseBase.Error($"Batch {request.BatchCode} has completed");
        batch.Provider = request.ProviderCode;
        //batch.Vendor = request.ProviderCode;
        batch.Status = (StockBatchStatus) request.Status;
        batch.Amount = request.Amount;
        batch.Discount = request.Discount;
        batch.TotalValue = request.Airtime;
        batch.Description = request.Description;
        batch.ModifiedAccount = request.ModifiedAccount;
        batch.ModifiedDate = DateTime.Now;
        batch.StockBatchItems = new List<StockBatchItemDto>();
        batch.StockBatchItems.Add(new StockBatchItemDto
        {
            Amount = request.Amount,
            Airtime = request.Airtime,
            Discount = request.Discount,
            ProductCode = request.ProviderCode,
            ItemValue = 1,
            Quantity = 1
        });
        await _stockAirtimeService.BatchUpdateAsync(batch);
        await ProcessAirtimeImportAsync(batch);
        return MessageResponseBase.Success(batch);
    }

    public async Task<MessageResponseBase> DeleteAsync(DeleteBatchAirtimeRequest request)
    {
        if (string.IsNullOrEmpty(request.BatchCode))
            return MessageResponseBase.Error("Batch empty does not exist");

        var batch = await _cardService.StockBatchGetAsync(new Guid(), request.BatchCode);
        if (batch == null)
            return MessageResponseBase.Error($"Batch {request.BatchCode} does not exist");
        if (batch.Status == StockBatchStatus.Active)
            return MessageResponseBase.Error($"Batch {request.BatchCode} has completed");
        await _stockAirtimeService.BatchDeleteAsync(batch.BatchCode);
        return MessageResponseBase.Success();
    }

    private async Task ProcessAirtimeImportAsync(StockBatchDto batch)
    {
        try
        {
            if (batch.Status == StockBatchStatus.Active)
            {
                _logger.LogInformation("ProcessAirtimeImport: " + batch.ToJson());
                await _bus.Publish<StockAirtimeImported>(new
                {
                    CorrelationId = batch.Id,
                    StockCode = StockCodeConst.STOCK_SALE,
                    ProviderCode = batch.Provider,
                    Amount = (int) batch.TotalValue
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("ProcessImportApi: " + ex);
        }
    }

    #endregion
}