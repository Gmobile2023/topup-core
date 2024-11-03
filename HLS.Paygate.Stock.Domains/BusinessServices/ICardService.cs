using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HLS.Paygate.Shared;
using HLS.Paygate.Stock.Contracts.ApiRequests;
using HLS.Paygate.Stock.Contracts.Dtos;
using HLS.Paygate.Stock.Contracts.Enums;
using HLS.Paygate.Stock.Domains.Entities;

namespace HLS.Paygate.Stock.Domains.BusinessServices;

public interface ICardService
{
    Task<bool> CardUpdateStatus(Guid id, CardStatus cardStatus);
    Task<bool> CardUpdateAsync(CardUpdateRequest cardUpdateRequest);
    Task<CardDto> CardGetAsync(Guid id, string serial, string productCode);
    Task<CardDto> CardGetWithVendorAsync(Guid id, string serial, string vendor);
    Task<CardDto> CardGetByCodeAsync(string code, string stockType);
    Task<MessagePagedResponseBase> CardGetListAsync(CardGetList cardGetList);
    Task<StockBatchDto> StockBatchInsertAsync(StockBatchDto stockBatchDto);
    Task<MessageResponseBase> StockBatchUpdateAsync(StockBatchDto stockBatchDto);

    Task<MessageResponseBase> StockBatchItemsUpdateAsync(string batchCode, string productCode, decimal cardValue,
        int quantity);

    Task<MessageResponseBase> StockBatchDeleteAsync(Guid id);

    Task<MessageResponseBase> StockBatchGuidDeleteAsync(Guid id);
    Task<StockBatchDto> StockBatchGetAsync(Guid id, string batchCode);
    Task<MessagePagedResponseBase> StockBatchListGetAsync(StockBatchGetListRequest batchGetListRequest);
    Task<StockTransDto> StockTransInsertAsync(StockTransDto dto);
    Task<StockTransDto> StockTransUpdateAsync(StockTransDto dto);


    Task<long> GetStockItemAvailable(string stockCode, string productCode, string batchCode);

    /// <summary>
    ///     CardsInsertAsync
    ///     Insert 1 list cùng mệnh giá cùng productCode
    /// </summary>
    /// <param name="batchCode"></param>
    /// <param name="cardItems"></param>
    /// <returns></returns>
    Task<MessageResponseBase> CardsInsertAsync(string batchCode, List<CardSimpleDto> cardItems);

    Task<MessageResponseBase> StockBatchInfoUpdateAsync();
    Task<MessageResponseBase> CardsInfoUpdateAsync();
    Task<MessageResponseBase> StockInfoUpdateAsync();

    Task<List<StockTransferItemInfo>> GetCardQuantityAvailableInStock(string stockCode, string batchCode,
        string categoryCode, string productCode);

    Task<StockTransRequest> StockTransRequestInsertAsync(StockTransRequest dto);

    Task<ResponseMesssageObject<string>> GetCardBatchSaleProviderRequest(DateTime date, string provider);

    Task<StockProviderConfig> GetProviderConfigBy(string provider, string productCode);

    Task<ResponseMesssageObject<string>> GetProviderConfigRequest(string provider, string productCode);

    Task<ResponseMesssageObject<string>> CreateProviderConfigRequest(string provider, string productCode, int quantity);

    Task<ResponseMesssageObject<string>> EditProviderConfigRequest(string provider, string productCode, int quantity);

    Task<bool> StockTransRequestUpdateAsync(string provider, string transCodeProvider, StockBatchStatus status,bool isSyncCard);

    Task<StockTransRequest> StockTransRequestGetAsync(string provider, string transCodeProvider);
}