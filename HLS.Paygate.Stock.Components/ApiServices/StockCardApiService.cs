using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HLS.Paygate.Gw.Model.Events;
using HLS.Paygate.Shared;
using HLS.Paygate.Shared.Contracts.Events.Report;
using HLS.Paygate.Shared.Dtos;
using HLS.Paygate.Shared.Utils;
using HLS.Paygate.Stock.Components.StockProcess;
using HLS.Paygate.Stock.Contracts.ApiRequests;
using HLS.Paygate.Stock.Contracts.Dtos;
using HLS.Paygate.Stock.Contracts.Enums;
using HLS.Paygate.Stock.Domains.BusinessServices;
using MassTransit;
using Microsoft.Extensions.Logging;
using Paygate.Discovery.Requests.Stocks;
using ServiceStack;

namespace HLS.Paygate.Stock.Components.ApiServices;

public class CardStockApiService : Service
{
    private readonly IBus _bus;
    private readonly ICardService _cardService;

    private readonly ILogger<CardStockApiService> _logger;
    private readonly IStockProcess _stockProcess;

    public CardStockApiService(ICardService cardService,
        IBus bus, IStockProcess stockProcess, ILogger<CardStockApiService> logger)
    {
        _cardService = cardService;
        _bus = bus;
        _stockProcess = stockProcess;
        _logger = logger;
    }

    #region update data info

    public async Task<object> PostAsync(StockInfoUpdateRequest request)
    {
        _logger.LogInformation(
            $"CardAutoImportRequest: RemoteIP: {Request.RemoteIp} - RequestURL: {Request.AbsoluteUri} - Data - {request.ToJson()}");
        // if (string.IsNullOrEmpty(request.Vendor))
        //     return MessageResponseBase.Error("Vendor not valid");
        if (string.IsNullOrEmpty(request.Command))
            return MessageResponseBase.Error("Command not valid");
        var dateLog = $"{DateTime.Now:yyyyMMddHHmmssfff}";
        try
        {
            if (request.Command == "hls_update_batch")
                return await _cardService.StockBatchInfoUpdateAsync();
            if (request.Command == "hls_update_card")
                return await _cardService.CardsInfoUpdateAsync();
            if (request.Command == "hls_update_stock")
                return await _cardService.StockInfoUpdateAsync();
            return MessageResponseBase.Error($"Command: {request.Command}; not found");
        }
        catch (PaygateException ex)
        {
            _logger.LogError("Error Exception: " + ex.Message);
            return MessageResponseBase.Error($"Command: {request.Command}");
        }
    }

    #endregion

    private async Task PublishReportAsync(ReportCardStockMessage message)
    {
        try
        {
            _logger.LogInformation($"PublishReport request: {message.ToJson()}");

            await _bus.Publish<ReportCardStockMessage>(new
            {
                message.ProviderItem,
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
            _logger.LogError("PublishReport error" + e);
        }
    }

    #region Import Stock

    // import tự động từ email
    public async Task<object> PostAsync(CardAutoImportRequest request)
    {
        _logger.LogInformation(
            $"CardAutoImportRequest: RemoteIP: {Request.RemoteIp} - RequestURL: {Request.AbsoluteUri} - Data_rows: {request?.CardItems?.Count()}");
        if (string.IsNullOrEmpty(request.Provider))
            return MessageResponseBase.Error("provider not valid");
        if (string.IsNullOrEmpty(request.Vendor))
            return MessageResponseBase.Error("Vendor not valid");
        if (!request.CardItems.Any())
            return MessageResponseBase.Error("CardItems not valid");
        var dateLog = $"{DateTime.Now:yyyyMMddHHmmssfff}";
        var fileLog = request.FileName.Split(".")[0];
        var category = request.Vendor;
        try
        {
            var batchDto = new StockBatchDto
            {
                Id = NewId.NextGuid(),
                ImportType = "EMAIL",
                BatchCode = $"{request.Vendor}_{request.Provider}_Auto_{dateLog}_{fileLog}",
                Description = "Auto Import By Email|" + request.FileName,
                //Vendor = request.Vendor,
                Provider = request.Provider,
                Status = StockBatchStatus.Active,
                CreatedDate = DateTime.Now,
                StockBatchItems = new List<StockBatchItemDto>()
            };

            var cardsAll = request.CardItems.Select(x => new CardItemsImport
            {
                CardCode = x.Pin,
                Serial = x.Serial,
                ExpiredDate = x.ExpiredDate.ToString("dd/MM/yyyy"),
                CardValue = (int)x.Value
            }).ToList();
            var cardsAllValue = cardsAll.GroupBy(x => x.CardValue).Select(x => x.Key).ToList();
            foreach (var val in cardsAllValue)
            {
                var cards = cardsAll.Where(x => x.CardValue == val).ToList();
                if (cards.Count == 0) continue;
                batchDto.StockBatchItems.Add(new StockBatchItemDto
                {
                    ServiceCode = "PIN_CODE",
                    CategoryCode = category,
                    ProductCode = category.ProductCodeGen(val),
                    ItemValue = (int)val,
                    Quantity = cards.Count,
                    QuantityImport = 0,
                    Discount = 0,
                    Amount = 0
                });
            }

            var batch = await _cardService.StockBatchInsertAsync(batchDto);
            if (null == batch)
                return MessageResponseBase.Error("Batch create error");

            foreach (var val in cardsAllValue)
            {
                var cards = cardsAll.Where(x => x.CardValue == val).ToList();
                if (!cards.Any())
                    continue;
                await CardsImportStockAsync(batch.BatchCode, category.ProductCodeGen(val), cards);
            }

            return MessageResponseBase.Error();
        }
        catch (PaygateException ex)
        {
            _logger.LogError("Error Exception: " + ex.Message);
            return MessageResponseBase.Error("Exception");
        }
    }

    // import file excel card
    public async Task<object> PostAsync(CardImportFileRequest request)
    {
        _logger.LogInformation(
            $"CardImportFileRequest: RemoteIP: {Request.RemoteIp} - RequestURL: {Request.AbsoluteUri} - Data_rows: {request?.Data?.Count()}");
        if (string.IsNullOrEmpty(request.Provider))
            return MessageResponseBase.Error("Provider not valid");
        if (request.Data == null || !request.Data.Any())
            return MessageResponseBase.Error("Data import not valid");
        var first = request.Data.FirstOrDefault();
        if (first != null && !first.Cards.Any())
            return MessageResponseBase.Error("Cards import not valid");

        var dateLog = $"{DateTime.Now:yyyyMMddHHmmssfff}";
        var batchDto = new StockBatchDto
        {
            ImportType = "FILE",
            BatchCode = $"FileImport_{dateLog}",
            Description = request.Description,
            // Vendor = string.Empty,
            Provider = request.Provider,
            Status = StockBatchStatus.Active,
            CreatedDate = DateTime.Now,
            StockBatchItems = new List<StockBatchItemDto>()
        };

        foreach (var x in request.Data)
        {
            if (x.Quantity <= 0) continue;
            batchDto.StockBatchItems.Add(new StockBatchItemDto
            {
                ServiceCode = x.ServiceCode,
                CategoryCode = x.CategoryCode,
                ProductCode = x.CategoryCode.ProductCodeGen(x.CardValue),
                ItemValue = (int)x.CardValue,
                Quantity = x.Quantity,
                QuantityImport = 0,
                Discount = x.Discount,
                Amount = 0
            });
        }

        batchDto.Id = Guid.NewGuid();
        var batch = await _cardService.StockBatchInsertAsync(batchDto);
        if (null == batch)
            return MessageResponseBase.Error("Batch create error");

        try
        {
            foreach (var cardsRequest in request.Data)
            {
                var cardsRequestsItems = cardsRequest.Cards.Select(x => new CardItemsImport
                {
                    CardCode = x.CardCode,
                    Serial = x.Serial,
                    ExpiredDate = x.ExpiredDate.ToString("dd/MM/yyyy"),
                    CardValue = (int)cardsRequest.CardValue
                }).ToList();

                var reponse = await CardsImportStockAsync(batch.BatchCode,
                    cardsRequest.CategoryCode.ProductCodeGen(cardsRequest.CardValue), cardsRequestsItems);

                _logger.LogInformation(
                    $"CardImportFileRequest reponse :  {reponse.ResponseStatus.ErrorCode}|{reponse.ResponseStatus.Message}");

                if (reponse.ResponseStatus.ErrorCode == ResponseCodeConst.Success) continue;

                await _cardService.StockBatchGuidDeleteAsync(batch.Id);
                return MessageResponseBase.Error(
                    reponse.ResponseStatus.ErrorCode == ResponseCodeConst.ResponseCode_Failed
                        ? "Quý khách kiểm tra lại danh sách thẻ. Đã có thẻ tồn tại trong hệ thống."
                        : reponse.ResponseStatus.Message);
            }

            return MessageResponseBase.Success();
        }
        catch (PaygateException ex)
        {
            _logger.LogError("Error Exception: " + ex.Message);
            return MessageResponseBase.Error("Exception");
        }
    }

    /// <summary>
    ///     Insert cards
    ///     Tăng tồn kho
    ///     publish báo cáo
    /// </summary>
    /// <param name="batchCode"></param>
    /// <param name="productCode"></param>
    /// <param name="cardItem"></param>
    /// <returns></returns>
    private async Task<NewMessageReponseBase<string>> CardsImportStockAsync(string batchCode, string productCode,
        List<CardItemsImport> cardItem)
    {
        try
        {
            // var obj = new
            // {
            //     BatchCode = batchCode,
            //     ProductCode = productCode,
            //     CardItems = cardItem,
            // };
            //_logger.Error("CardsImportStock Send: " + obj.ToJson());
            // var response = await _stockCardsImportCommand.GetResponse<MessageResponseBase>(obj);
            // return response.Message;
            //_logger.Error("CardsImportStock Send: " + obj.ToJson());
            return await _stockProcess.InportListRequest(new StockCardImportListRequest
            {
                BatchCode = batchCode,
                CardItems = cardItem,
                ProductCode = productCode
            });
        }
        catch (Exception ex)
        {
            _logger.LogError("CardsImportStock Exception: " + ex);
            return new NewMessageReponseBase<string>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error, ex.Message)
            };
        }
    }

    // import từ API
    public async Task<object> Post(StockCardImportApiRequest request)
    {
        _logger.LogInformation($"CardsApiImportRequest:{request.ToJson()}");

        if (string.IsNullOrEmpty(request.Provider))
            return new NewMessageReponseBase<List<NewMessageReponseBase<string>>>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error, "Provider not valid")
            };
        if (!request.CardItems.Any())
            return new NewMessageReponseBase<List<NewMessageReponseBase<string>>>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error, "CardItems not valid")
            };

        var rs = _stockProcess.CardImportStockFromProvider(request).Result;
        _logger.LogInformation($"CardsApiImportRequest return :{rs.ResponseStatus.ToJson()}");
        return rs;
    }


    /// <summary>
    /// Kiểm tra mã thẻ bằng Api
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public async Task<object> Post(StockCardApiCheckTransRequest request)
    {
        _logger.LogInformation($"StockCardApiCheckTransRequest: {request.ToJson()}");
        if (string.IsNullOrEmpty(request.Provider))
            return new NewMessageReponseBase<string>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error, "Provider not valid")
            };


        var rs = _stockProcess.CheckTransCardFromProvider(request).Result;
        _logger.LogInformation($"StockCardApiCheckTransRequest return :{rs.ResponseStatus.ToJson()}");
        return rs;
    }

    #endregion

    #region StockExchange - chuyển kho - cũ

    private class CardTransferBlock
    {
        public string Vendor { get; set; }
        public string ProductCode { get; set; }
        public int CardValue { get; set; }
        public int Quantity { get; set; }
        public StockBatchDto Batch { get; set; }
    }

    public async Task<object> PostAsync(ExchangeRequest request)
    {
        _logger.LogInformation($"CardExchangeRequest request:{request.ToJson()}");
        if (string.IsNullOrEmpty(request.Vendor))
            return MessageResponseBase.Error("Vui lòng chọn nhà cung cấp");
        if (string.IsNullOrEmpty(request.SrcStockCode))
            return MessageResponseBase.Error("Vui lòng chọn kho chuyển");
        if (string.IsNullOrEmpty(request.DesStockCode))
            return MessageResponseBase.Error("Vui lòng chọn kho nhập");
        if (request.SrcStockCode == request.DesStockCode)
            return MessageResponseBase.Error("Kho nhập và kho chuyển không được phép trùng nhau");

        var transferBlock = new List<CardTransferBlock>();
        // chuyển 20 thẻ, giá trị 100k trong lô thẻ bất kỳ
        if (string.IsNullOrEmpty(request.BatchCode) && request.CardValue.HasValue && request.Quantity > 0)
        {
            var productCode =
                request.Vendor.ProductCodeGen(request.CardValue.Value); // $"{request.Vendor}_{request.CardValue}";
            var cardQty = await _cardService.GetStockItemAvailable(request.SrcStockCode, productCode, null);
            if (cardQty < request.Quantity)
                return MessageResponseBase.Error("Vui lòng nhập lô hoặc mệnh giá thẻ cần chuyển");
            transferBlock.Add(new CardTransferBlock
            {
                Vendor = request.Vendor,
                ProductCode = productCode,
                CardValue = request.CardValue.Value,
                Quantity = request.Quantity,
                Batch = null
            });
        }
        // chuyển 10 thẻ, giá trị 100k trong lô thẻ a
        else if (!string.IsNullOrEmpty(request.BatchCode) && request.CardValue.HasValue && request.Quantity > 0)
        {
            var batch = await _cardService.StockBatchGetAsync(new Guid(), request.BatchCode);
            if (batch == null)
                return MessageResponseBase.Error($"Lô {request.BatchCode} không tồn tại");

            var productCode =
                request.Vendor.ProductCodeGen(request.CardValue.Value); // $"{request.Vendor}_{request.CardValue}";
            var cardQty = await _cardService.GetStockItemAvailable(request.SrcStockCode, productCode, null);
            if (cardQty < request.Quantity)
                return MessageResponseBase.Error(
                    $"Lô {request.BatchCode} không có thẻ mệnh giá {request.CardValue} đ");

            transferBlock.Add(new CardTransferBlock
            {
                Vendor = request.Vendor,
                ProductCode = productCode,
                CardValue = request.CardValue.Value,
                Quantity = request.Quantity,
                Batch = batch
            });
        }
        // chuyển thẻ cùng 1 mệnh giá trong lô thẻ a
        else if (!string.IsNullOrEmpty(request.BatchCode) && request.CardValue.HasValue)
        {
            var batch = await _cardService.StockBatchGetAsync(new Guid(), request.BatchCode);
            if (batch == null)
                return MessageResponseBase.Error($"Lô {request.BatchCode} không tồn tại");
            var productCode =
                request.Vendor.ProductCodeGen(request.CardValue.Value); //  $"{request.Vendor}_{request.CardValue}";
            var cardQty =
                await _cardService.GetStockItemAvailable(request.SrcStockCode, productCode, request.BatchCode);
            if (cardQty <= 0)
                return MessageResponseBase.Error(
                    $"Lô {request.BatchCode} không có thẻ mệnh giá {request.CardValue} đ");
            transferBlock.Add(new CardTransferBlock
            {
                Vendor = request.Vendor,
                ProductCode = productCode,
                CardValue = request.CardValue.Value,
                Quantity = (int)cardQty,
                Batch = batch
            });
        }
        // chuyển tất cả thẻ trong lô thẻ a
        else if (!string.IsNullOrEmpty(request.BatchCode))
        {
            var batch = await _cardService.StockBatchGetAsync(new Guid(), request.BatchCode);
            if (batch == null)
                return MessageResponseBase.Error($"Lô {request.BatchCode} không tồn tại");
            foreach (var x in batch.StockBatchItems)
            {
                var productCode = request.Vendor.ProductCodeGen(x.ItemValue); // $"{request.Vendor}_{x.CardValue}";
                var cardQty =
                    await _cardService.GetStockItemAvailable(request.SrcStockCode, productCode, request.BatchCode);
                if (cardQty == 0)
                    continue;
                transferBlock.Add(new CardTransferBlock
                {
                    Vendor = request.Vendor,
                    ProductCode = productCode,
                    CardValue = x.ItemValue,
                    Quantity = (int)cardQty,
                    Batch = batch
                });
            }
        }

        if (!transferBlock.Any())
            return MessageResponseBase.Error("Số lượng chuyển không phù hợp, vui lòng kiểm tra lại");

        try
        {
            var dateLog = $"{DateTime.Now:yyyyMMddHHmmssfff}";
            var stockTrans = request.ConvertTo<StockTransDto>();
            stockTrans.StockTransCode = $"{request.SrcStockCode}_{request.DesStockCode}_{dateLog}";
            stockTrans.StockItemsRequest = transferBlock.Select(x => new StockTransItemDto
            {
                ItemValue = x.CardValue,
                Quantity = x.Quantity,
                ProductCode = x.Vendor.ProductCodeGen(x.CardValue)
            })
                .ToList();
            stockTrans.StockTransItems = new List<StockTransItemDto>();
            stockTrans = await _cardService.StockTransInsertAsync(stockTrans);
            if (null == stockTrans)
                return MessageResponseBase.Error("Lỗi tạo giao dịch chuyển kho");
            foreach (var block in transferBlock)
            {
                var rs = await _stockProcess.ExchangeRequest(new StockCardExchangeRequest
                {
                    SrcStockCode = stockTrans.SrcStockCode,
                    DesStockCode = stockTrans.DesStockCode,
                    ProductCode = block.ProductCode,
                    Amount = block.Quantity,
                    BatchCode = block.Batch != null ? block.Batch.BatchCode : "",
                    Description = request.Description
                });
                // var (accepted, rejected) =
                //     await _stockExchangeClient
                //         .GetResponse<CardStockCommandSubmitted<string>, CardStockCommandRejected>(
                //             new StockCardExchangeRequest
                //             {
                //                 SrcStockCode = stockTrans.SrcStockCode,
                //                 DesStockCode = stockTrans.DesStockCode,
                //                 ProductCode = block.ProductCode,
                //                 Amount = block.Quantity,
                //                 BatchCode = block.Batch != null ? block.Batch.BatchCode : "",
                //                 Description = request.Description
                //             });
                // if (accepted.IsCompleted)
                //     continue;
                // var response = await rejected;
                //return MessageResponseBase.Success(response.Message.Reason);

                if (rs.ResponseStatus.ErrorCode == ResponseCodeConst.Success)
                    continue;
                //var response = await rejected;
                return MessageResponseBase.Success(rs.ResponseStatus.Message);
            }

            return MessageResponseBase.Success(stockTrans);
        }
        catch (Exception ex)
        {
            _logger.LogInformation("Error Exception: " + ex.Message);
            return MessageResponseBase.Error("Exception");
        }
    }

    #endregion

    #region transfer - chuyển kho

    public async Task<object> GetAsync(GetCardInfoTransferRequest request)
    {
        try
        {
            _logger.LogInformation(
                $"GetCardInfoTransferRequest: RemoteIP: {Request.RemoteIp} - RequestURL: {Request.AbsoluteUri} - Data - {request.ToJson()}");
            var data = await _cardService.GetCardQuantityAvailableInStock(request.SrcStockCode, request.BatchCode,
                request.CategoryCode, request.ProductCode);
            _logger.LogInformation($"GetCardInfoTransferRequest: Data.count - {data?.Count ?? 0}");
            return MessageResponseBase.Success(data);
        }
        catch (Exception ex)
        {
            _logger.LogInformation("GetCardInfoTransferRequest Exception: " + ex);
            return MessageResponseBase.Error(ex.Message);
        }
    }

    public async Task<object> PostAsync(StockTransferCardRequest request)
    {
        try
        {
            _logger.LogInformation(
                $"StockTransferCardRequest: RemoteIP: {Request.RemoteIp} - RequestURL: {Request.AbsoluteUri} - Data - {request.ToJson()}");
            if (string.IsNullOrEmpty(request.SrcStockCode))
                return MessageResponseBase.Error("Vui lòng chọn kho chuyển");
            if (string.IsNullOrEmpty(request.DesStockCode))
                return MessageResponseBase.Error("Vui lòng chọn kho nhập");
            if (request.SrcStockCode == request.DesStockCode)
                return MessageResponseBase.Error("Kho nhập và kho chuyển không được phép trùng nhau");
            if (request.ProductList == null || !request.ProductList.Any())
                return MessageResponseBase.Error("Vui lòng chọn sản phẩm cần chuyển");

            var dateLog = $"{DateTime.Now:yyyyMMddHHmmssfff}";
            var stockTrans = new StockTransDto();
            stockTrans.StockTransCode = $"{request.SrcStockCode}_{request.DesStockCode}_{dateLog}";
            stockTrans.Status = StockTransStatus.Active;
            stockTrans.StockTransType = request.TransferType;
            stockTrans.SrcStockCode = request.SrcStockCode;
            stockTrans.DesStockCode = request.DesStockCode;
            stockTrans.ProviderCode = "";
            stockTrans.Quantity = request.ProductList.Sum(x => x.Quantity);
            stockTrans.Description = $"Chuyển kho {request.SrcStockCode} sang kho {request.DesStockCode} ";
            stockTrans.StockItemsRequest = request.ProductList.ConvertTo<List<StockTransItemDto>>();
            stockTrans.StockTransItems = new List<StockTransItemDto>();
            stockTrans = await _cardService.StockTransInsertAsync(stockTrans);

            if (null == stockTrans)
                return MessageResponseBase.Error("Lỗi tạo giao dịch chuyển kho");

            foreach (var block in request.ProductList)
            {
                var quantityAvailableInStock =
                    await _cardService.GetCardQuantityAvailableInStock(request.SrcStockCode, "", "",
                        block.ProductCode);
                if (quantityAvailableInStock == null || !quantityAvailableInStock.Any())
                    return MessageResponseBase.Error(
                        $"Tồn kho của sản phẩm {block.ProductName} lỗi, vui lòng kiểm tra lại");
                var quantityAvailable = quantityAvailableInStock.FirstOrDefault()?.QuantityAvailable;
                if (quantityAvailable < block.Quantity)
                    return MessageResponseBase.Error(
                        $"Tồn kho của sản phẩm {block.ProductName} không đủ, vui lòng kiểm tra lại");


                // var (accepted, rejected) =
                //     await _stockExchangeClient
                //         .GetResponse<CardStockCommandSubmitted<string>, CardStockCommandRejected>(
                //             new StockCardExchangeRequest
                //             {
                //                 SrcStockCode = stockTrans.SrcStockCode,
                //                 DesStockCode = stockTrans.DesStockCode,
                //                 ProductCode = block.ProductCode,
                //                 Amount = block.Quantity,
                //                 BatchCode = request.TransferType == "batch" ? request.BatchCode : "",
                //                 Description = stockTrans.Description
                //             });

                var rs = await _stockProcess.ExchangeRequest(new StockCardExchangeRequest
                {
                    SrcStockCode = stockTrans.SrcStockCode,
                    DesStockCode = stockTrans.DesStockCode,
                    ProductCode = block.ProductCode,
                    Amount = block.Quantity,
                    BatchCode = request.TransferType == "batch" ? request.BatchCode : "",
                    Description = stockTrans.Description
                });
                if (rs.ResponseStatus.ErrorCode == ResponseCodeConst.Success)
                {
                    stockTrans.StockTransItems.Add(block.ConvertTo<StockTransItemDto>());
                }
                else
                {
                    await _cardService.StockTransUpdateAsync(stockTrans);
                    return MessageResponseBase.Error(rs.ResponseStatus.Message);
                }
            }

            await _cardService.StockTransUpdateAsync(stockTrans);
            return MessageResponseBase.Success(stockTrans);
        }
        catch (Exception ex)
        {
            _logger.LogError("StockTransferCardRequest Exception: " + ex);
            return MessageResponseBase.Error(ex.Message);
        }
    }

    #endregion
}