using System;
using System.Threading.Tasks;
using GMB.Topup.Stock.Components.StockProcess;
using GMB.Topup.Stock.Contracts.ApiRequests;
using GMB.Topup.Stock.Domains.BusinessServices;
using GMB.Topup.Stock.Domains.Grains;
using MassTransit;
using Microsoft.Extensions.Logging;
using Orleans;
using GMB.Topup.Discovery.Requests.Stocks;
using GMB.Topup.Shared;
using GMB.Topup.Shared.Contracts.Events.Report;
using GMB.Topup.Shared.Helpers;
using GMB.Topup.Shared.Utils;
using ServiceStack;

namespace GMB.Topup.Stock.Components.ApiServices;

public class MainService : Service
{
    private readonly IBus _bus;
    private readonly ICardService _cardService;
    private readonly ICardStockService _cardStockService;
    private readonly IDateTimeHelper _dateTimeHelper;
    private readonly ILogger<MainService> _logger;
    private readonly IStockProcess _stockProcess;
    private readonly IClusterClient _clusterClient;

    public MainService(ICardStockService cardStockService, ICardService cardService,
        IDateTimeHelper dateTimeHelper, IBus bus, IStockProcess stockProcess, ILogger<MainService> logger,
        IClusterClient clusterClient)
    {
        _cardStockService = cardStockService;
        _cardService = cardService;
        _dateTimeHelper = dateTimeHelper;
        _bus = bus;
        _stockProcess = stockProcess;
        _logger = logger;
        _clusterClient = clusterClient;
    }

    private async Task StockInventoryUpdateAsync(ReportCardStockMessage message)
    {
        try
        {
            _logger.LogInformation($"NotifyInventoryUpdate request: {message.ToJson()}");

            await _bus.Publish<ReportCardStockMessage>(new
            {
                message.StockCode,
                message.Id,
                message.Inventory,
                message.Serial,
                message.Vendor,
                message.CardValue,
                message.InventoryType,
                message.Increase,
                message.Decrease,
                CreatedDate = DateTime.Now,
                InventoryAfter = message.Inventory,
                InventoryBefore = message.Increase > 0
                    ? message.Inventory - message.Increase
                    : message.Inventory + message.Decrease
            });
        }
        catch (Exception e)
        {
            _logger.LogInformation("NotifyInventoryUpdate error" + e);
        }
    }

    public async Task<object> GetAsync(PingRouteRequest request)
    {
        return await Task.FromResult("OK");
    }

    #region CardBatch

    // public async Task<object> Post(CardBatchCreateRequest batchCreateRequest)
    // {
    //     _logger.Info($"CardBatchCreateRequest request:{batchCreateRequest.ToJson()}");
    //     var returnMessage = new MessageResponseBase();
    //     var cardBatch = await _cardService.StockBatchGetAsync(new Guid(), batchCreateRequest.BatchCode);
    //     if (null != cardBatch)
    //     {
    //         returnMessage.Payload = batchCreateRequest.BatchCode;
    //         returnMessage.ResponseCode = "Lô: " + batchCreateRequest.BatchCode + " đã tồn tại!";
    //         returnMessage.ResponseCode = ResponseCodeConst.Error;
    //     }
    //
    //     var cardBatchDto = batchCreateRequest.ConvertTo<StockBatchDto>();
    //     cardBatchDto.ImportType = "BASIC";
    //     cardBatchDto = await _cardService.StockBatchInsertAsync(cardBatchDto);
    //
    //     if (null != cardBatchDto)
    //     {
    //         returnMessage.Payload = cardBatchDto;
    //         returnMessage.ResponseCode = "Thành công!";
    //         returnMessage.ResponseCode = "01";
    //     }
    //
    //     _logger.Info($"CardBatchCreateRequest return:{returnMessage.ToJson()}");
    //     return returnMessage;
    // }
    //
    // public async Task<object> Patch(CardBatchUpdateRequest cardBatchUpdateRequest)
    // {
    //     _logger.Info($"CardBatchUpdateRequest request:{cardBatchUpdateRequest.ToJson()}");
    //     var cardBatchDto = cardBatchUpdateRequest.ConvertTo<StockBatchDto>();
    //     var result = await _cardService.StockBatchUpdateAsync(cardBatchDto);
    //     _logger.Info($"CardBatchUpdateRequest return:{result.ToJson()}");
    //     return result;
    // }
    //
    // public async Task<object> Delete(CardBatchDeleteRequest batchDeleteRequest)
    // {
    //     _logger.Info($"CardBatchDeleteRequest request:{batchDeleteRequest.ToJson()}");
    //     var rs = await _cardService.StockBatchDeleteAsync(batchDeleteRequest.Id);
    //     _logger.Info($"CardBatchDeleteRequest return:{rs.ToJson()}");
    //     return rs;
    // }

    public async Task<object> GetAsync(CardBatchGetRequest batchGetRequest)
    {
        _logger.LogInformation($"CardBatchGetRequest request: {batchGetRequest.ToJson()}");
        var returnMessage = new MessageResponseBase
        {
            ResponseCode = ResponseCodeConst.Error,
            ResponseMessage = "Không thành công!"
        };

        var cardBatch = await _cardService.StockBatchGetAsync(batchGetRequest.Id, batchGetRequest.BatchCode);

        if (null != cardBatch)
        {
            returnMessage.Payload = cardBatch;
            returnMessage.ResponseCode = "Thành công!";
            returnMessage.ResponseCode = "01";
        }

        _logger.LogInformation($"CardBatchGetRequest return:{returnMessage.ToJson()}");
        return returnMessage;
    }

    public async Task<object> GetAsync(StockBatchGetListRequest stockBatchGetListRequest)
    {
        _logger.LogInformation($"CardBatchGetListRequest request: {stockBatchGetListRequest.ToJson()}");
        var result = await _cardService.StockBatchListGetAsync(stockBatchGetListRequest);
        _logger.LogInformation($"CardBatchGetListRequest return: {result.ResponseCode}-{result.Total}");
        return result;
    }

    public async Task<object> GetAsync(GetCardBatchSaleProviderRequest request)
    {
        _logger.LogInformation($"GetCardBatchSaleProviderRequest request: {request.ToJson()}");
        var result = await _cardService.GetCardBatchSaleProviderRequest(request.Date, request.Provider);
        _logger.LogInformation($"GetCardBatchSaleProviderRequest return: {result.ResponseCode}-{result.Total}");
        return result;
    }

    #endregion

    #region Card

    // public async Task<object> Post(CardImportRequest request)
    // {
    //     _logger.Info($"CardImportRequest request:{request.ToJson()}");
    //     var returnMessage = new MessageResponseBase()
    //     {
    //         ResponseCode = ResponseCodeConst.Error,
    //         ResponseMessage = "Không thành công!"
    //     };
    //     if (string.IsNullOrEmpty(request.BatchCode))
    //         return MessageResponseBase.Error($"BatchCode not valid");
    //     var batch = await _cardService.StockBatchGetAsync(new Guid(), request.BatchCode);
    //     if (batch == null)
    //         return MessageResponseBase.Error($"Batch {request.BatchCode} does not exist");
    //
    //     try
    //     {
    //         var cardAdd = new CardImportRequest()
    //         {
    //             BatchCode = batch.BatchCode,
    //             CardItem = request.CardItem,
    //         };
    //         var card = await _cardService.CardInsertAsync(cardAdd);
    //
    //         if (null != card)
    //         {
    //             returnMessage.ResponseCode = "01";
    //             returnMessage.ResponseMessage = "Thêm thẻ thành công!";
    //             //Tăng tồn kho khi import thẻ
    //             var id = NewId.NextGuid();
    //             var (accepted, rejected) =
    //                 await _stockImportClient.GetResponse<CardStockCommandSubmitted<int>, CardStockCommandRejected>(
    //                     new
    //                     {
    //                         CorrelationId = id,
    //                         StockCode = StockCodeConst.STOCK_TEMP,
    //                         card.ProductCode,
    //                         card.CardValue,
    //                         Amount = 1
    //                     },
    //                     CancellationToken.None, RequestTimeout.After(m: 1));
    //
    //             var result = await accepted;
    //             // update batch update ton kho ===================================================================================================
    //             if (accepted.IsCompleted && accepted.Status == TaskStatus.RanToCompletion)
    //             {
    //                 var updateBatch =
    //                     await _cardService.StockBatchItemsUpdateAsync(batch, card.ProductCode, 1,  card.CardValue, 0);
    //                 if (updateBatch.ResponseCode != ResponseCodeConst.Success)
    //                     return MessageResponseBase.Error(updateBatch.ResponseMessage);
    //             }
    //
    //             if (accepted.IsCompleted && accepted.Status == TaskStatus.RanToCompletion)
    //             {
    //                 try
    //                 {
    //                     var inventory = result.Message.Payload;
    //                     await StockInventoryUpdate(new ReportCardStockMessage
    //                     {
    //                         Id = id,
    //                         Inventory = int.Parse(inventory.ToJson()),
    //                         Vendor = card.ProductCode,
    //                         Serial = card.Serial,
    //                         CardValue = card.CardValue,
    //                         StockCode = StockCodeConst.STOCK_TEMP,
    //                         Increase = 1,
    //                         InventoryType = CardTransType.Inventory
    //                     });
    //                 }
    //                 catch (Exception e)
    //                 {
    //                     _logger.Error("NotifyInventoryUpdate error:" + e);
    //                 }
    //             }
    //             else
    //             {
    //                 _logger.Error(
    //                     $"CardInsert update Inventory error: {card.Serial}-{card.ProductCode}-{card.CardValue}");
    //             }
    //         }
    //     }
    //     catch (PaygateException ex)
    //     {
    //         _logger.Error("Error insert card: " + ex.Message);
    //         if (ex.Code == "109")
    //         {
    //             returnMessage.ResponseCode = ResponseCodeConst.Error;
    //             returnMessage.ResponseMessage = "Trùng mã thẻ";
    //         }
    //     }
    //
    //     _logger.Info($"CardImportRequest return:{returnMessage.ToJson()}");
    //     return returnMessage;
    // }

    public async Task<object> PatchAsync(CardUpdateRequest cardUpdateRequest)
    {
        _logger.LogInformation($"CardUpdateRequest request:{cardUpdateRequest.ToJson()}");
        var returnMessage = new MessageResponseBase
        {
            ResponseCode = ResponseCodeConst.Error,
            ResponseMessage = "Không thành công!"
        };
        try
        {
            var result = await _cardService.CardUpdateAsync(cardUpdateRequest);

            if (result)
            {
                returnMessage.ResponseCode = "01";
                returnMessage.ResponseMessage = "Cập nhật thẻ thành công!";
            }
        }
        catch (PaygateException ex)
        {
            _logger.LogError("Error update card: " + ex.Message);
            if (ex.Code == "109")
            {
                returnMessage.ResponseCode = ResponseCodeConst.Error;
                returnMessage.ResponseMessage = "Trùng mã thẻ";
            }
        }

        _logger.LogInformation($"CardUpdateRequest return:{returnMessage.ToJson()}");
        return returnMessage;
    }

    public async Task<object> GetAsync(CardGet cardGet)
    {
        _logger.LogInformation($"CardGet request:{cardGet.ToJson()}");
        var rerult = await _cardService.CardGetWithVendorAsync(cardGet.Id, cardGet.Serial, cardGet.Vendor);
        var returnMessage = new MessageResponseBase
        {
            ResponseCode = ResponseCodeConst.Error,
            ResponseMessage = "Không thành công!"
        };
        if (null != rerult)
        {
            returnMessage.ResponseCode = "01";
            returnMessage.ResponseMessage = "Thành công!";
            rerult.CardCode = "... encrypted ...";
            rerult.ExpiredDate = _dateTimeHelper.ConvertToUserTime(rerult.ExpiredDate, DateTimeKind.Utc);
            returnMessage.Payload = rerult;
        }

        _logger.LogInformation($"CardGet return:{returnMessage.ToJson()}");
        return returnMessage;
    }

    public async Task<object> GetAsync(CardGetByCode cardGetByCode)
    {
        _logger.LogInformation($"CardGetByCode request:{cardGetByCode.ToJson()}");
        var rerult = await _cardService.CardGetByCodeAsync(cardGetByCode.Code, cardGetByCode.Vendor);
        var returnMessage = new MessageResponseBase
        {
            ResponseCode = ResponseCodeConst.Error,
            ResponseMessage = "Không thành công!"
        };
        if (null != rerult)
        {
            rerult.CardCode = rerult.CardCode.DecryptTripleDes();
            returnMessage.ResponseCode = "01";
            returnMessage.ResponseMessage = "Thành công!";
            rerult.CardCode = rerult.CardCode;
            rerult.ExpiredDate = _dateTimeHelper.ConvertToUserTime(rerult.ExpiredDate, DateTimeKind.Utc);
            returnMessage.Payload = rerult;
        }

        _logger.LogInformation($"CardGetByCode return:{returnMessage.ToJson()}");
        return returnMessage;
    }

    public async Task<object> GetAsync(CardGetWithClearCode cardGet)
    {
        var rerult = await _cardService.CardGetAsync(cardGet.Id, cardGet.Serial, cardGet.Vendor);
        var returnMessage = new MessageResponseBase
        {
            ResponseCode = ResponseCodeConst.Error,
            ResponseMessage = "Không thành công!"
        };
        if (null != rerult)
        {
            returnMessage.ResponseCode = "01";
            returnMessage.ResponseMessage = "Thành công!";
            rerult.CardCode = rerult.CardCode.DecryptTripleDes();
            rerult.ExpiredDate = _dateTimeHelper.ConvertToUserTime(rerult.ExpiredDate, DateTimeKind.Utc);
            returnMessage.Payload = rerult;
        }

        return returnMessage;
    }

    public async Task<object> GetAsync(CardGetList cardGetList)
    {
        _logger.LogInformation($"CardGetList request:{cardGetList.ToJson()}");
        var rs = await _cardService.CardGetListAsync(cardGetList);
        _logger.LogInformation($"CardGetList return:{rs.ResponseCode}-{rs.Total}");
        return rs;
    }

    public async Task<object> PatchAsync(CardUpdateStatus cardUpdateStatus)
    {
        _logger.LogInformation($"CardUpdateStatus request:{cardUpdateStatus.ToJson()}");
        var result = await _cardService.CardUpdateStatus(cardUpdateStatus.Id, cardUpdateStatus.CardStatus);
        var returnMessage = new MessageResponseBase
        {
            ResponseCode = ResponseCodeConst.Error,
            ResponseMessage = "Không thành công!"
        };
        if (result)
        {
            returnMessage.ResponseCode = "01";
            returnMessage.ResponseMessage = "Thành công!";
        }

        _logger.LogInformation($"CardUpdateStatus return:{returnMessage.ToJson()}");
        return returnMessage;
    }

    #endregion

    #region CardStock

    public async Task<object> PostAsync(CardStockCreateRequest request)
    {
        _logger.LogInformation($"CardStockCreateRequest request:{request.ToJson()}");
        var productCode =
            request.Vendor.ProductCodeGen(request.CardValue); // $"{request.Vendor}_{request.CardValue}";
        var stock = await _cardStockService.StockGetAsync(request.StockCode, productCode);

        if (null == stock)
        {
            //Neu stock null, truy van so du de Orleans tao Stock
            // var (accepted, rejected) =
            //     await _stockInventoryClient.GetResponse<CardStockCommandSubmitted<int>, CardStockCommandRejected>(
            //         new
            //         {
            //             CorrelationId = NewId.NextGuid(),
            //             StockCode = request.StockCode,
            //             ProductCode = productCode,
            //             CardValue = request.CardValue
            //         },
            //         CancellationToken.None, RequestTimeout.After(m: 1));
            // var result = await accepted;
            var result = await _stockProcess.CheckInventoryRequest(new StockCardCheckInventoryRequest
            {
                CardValue = request.CardValue,
                ProductCode = productCode,
                StockCode = request.StockCode
            });
            _logger.LogInformation("Create stock check inventory: " + result.ToJson());
            stock = await _cardStockService.StockGetAsync(request.StockCode, productCode);
        }

        stock.InventoryLimit = request.InventoryLimit;
        stock.ItemValue = request.CardValue;
        stock.MinimumInventoryLimit = request.MinimumInventoryLimit;
        stock.Description = request.Description;
        await _cardStockService.StockUpdateAsync(stock);

        return new MessageResponseBase
        {
            ResponseCode = "01",
            ResponseMessage = "Success"
        };
    }

    public async Task<object> PutAsync(CardStockUpdateRequest cardStockUpdateRequest)
    {
        _logger.LogInformation($"CardStockUpdateRequest request:{cardStockUpdateRequest.ToJson()}");
        var stock = await _cardStockService.StockGetAsync(cardStockUpdateRequest.StockCode,
            cardStockUpdateRequest.ProductCode);

        if (null == stock)
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage =
                    $"Stock with code {cardStockUpdateRequest.StockCode} and stockType {cardStockUpdateRequest.ProductCode} does not exist!"
            };

        stock.InventoryLimit = cardStockUpdateRequest.InventoryLimit;
        stock.MinimumInventoryLimit = cardStockUpdateRequest.MinimumInventoryLimit;
        stock.Description = cardStockUpdateRequest.Description;

        await _cardStockService.StockUpdateAsync(stock);

        return new MessageResponseBase
        {
            ResponseCode = "01",
            ResponseMessage = "Success"
        };
    }

    public async Task<object> PutAsync(UpdateInventoryRequest updateInventoryRequest)
    {
        _logger.LogInformation($"UpdateInventoryRequest request:{updateInventoryRequest.ToJson()}");
        var stock = await _cardStockService.StockGetAsync(updateInventoryRequest.StockCode,
            updateInventoryRequest.KeyCode);

        if (null == stock)
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage =
                    $"Stock with code {updateInventoryRequest.StockCode} and stockType {updateInventoryRequest.KeyCode} does not exist!"
            };

        stock.Inventory = updateInventoryRequest.Inventory;

        var stockGrain = _clusterClient.GetGrain<IStockGrain>(string.Join("|", updateInventoryRequest.StockCode,
            updateInventoryRequest.KeyCode));//xem lại đúng key chưa
        var update = await stockGrain.UpdateInventory(stock.Inventory);
        if (update)
        {
            await _cardStockService.StockUpdateAsync(stock);
            //chỗ này cảnh báo thêm nếu k update dc db
            return new MessageResponseBase
            {
                ResponseCode = "01",
                ResponseMessage = "Success"
            };
        }

        return new MessageResponseBase
        {
            ResponseCode = "00",
            ResponseMessage = "Không thể update tồn kho"
        };
    }

    public async Task<object> GetAsync(CardStockGetRequest cardStockGetRequest)
    {
        var stock = await _cardStockService.StockGetAsync(cardStockGetRequest.StockCode,
            cardStockGetRequest.ProductCode);

        var returnMessage = new MessageResponseBase
        {
            ResponseCode = ResponseCodeConst.Error,
            ResponseMessage = "Không thành công!"
        };
        if (null != stock)
        {
            returnMessage.ResponseCode = "01";
            returnMessage.ResponseMessage = "Thành công!";
            returnMessage.Payload = stock;
        }

        return returnMessage;
    }

    public async Task<object> GetAsync(CardStockGetListRequest cardStockGetListRequest)
    {
        _logger.LogInformation("CardStockGetListRequest reqeust:" + cardStockGetListRequest.ToJson());
        var stocks = await _cardStockService.StockGetPagedAsync(cardStockGetListRequest);
        return stocks;
    }

    public async Task<object> GetAsync(GetProviderConfigRequest request)
    {
        _logger.LogInformation("GetProviderConfigRequest reqeust:" + request.ToJson());
        var config = await _cardService.GetProviderConfigRequest(request.Provider, request.ProductCode);
        return config;
    }

    public async Task<object> PostAsync(CreateProviderConfigRequest request)
    {
        _logger.LogInformation("CreateProviderConfigRequest reqeust:" + request.ToJson());
        var config =
            await _cardService.CreateProviderConfigRequest(request.Provider, request.ProductCode, request.Quantity);
        return config;
    }

    public async Task<object> PutAsync(EditProviderConfigRequest request)
    {
        _logger.LogInformation("EditProviderConfigRequest reqeust:" + request.ToJson());
        var config =
            await _cardService.EditProviderConfigRequest(request.Provider, request.ProductCode, request.Quantity);
        return config;
    }

    /// <summary>
    /// Danh sách lấy dữ liệu hình thức lấy thẻ về kho bằng ApI
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public async Task<object> GetAsync(CardStockTransListRequest request)
    {
        _logger.LogInformation($"CardStockTransListRequest request:{request.ToJson()}");
        var rs = await _cardStockService.CardStockTransListAsync(request);
        _logger.LogInformation($"CardStockTransListRequest return:{rs.ResponseCode}-{rs.Total}");
        return rs;
    }

    #endregion
}