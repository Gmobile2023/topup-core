using System.Collections.Generic;
using System.Threading.Tasks;
using GMB.Topup.Discovery.Requests.Stocks;
using GMB.Topup.Shared;
using GMB.Topup.Shared.Dtos;

namespace GMB.Topup.Stock.Components.StockProcess;

public interface IStockProcess
{
    Task<NewMessageResponseBase<object>> ExchangeRequest(StockCardExchangeRequest request);
    Task<NewMessageResponseBase<object>> InportRequest(StockCardImportRequest request);
    Task<NewMessageResponseBase<int>> CheckInventoryRequest(StockCardCheckInventoryRequest request);

    Task<NewMessageResponseBase<List<CardRequestResponseDto>>> ExportCardToSaleRequest(
        StockCardExportSaleRequest request);

    Task<NewMessageResponseBase<string>> InportListRequest(StockCardImportListRequest request);

    Task<NewMessageResponseBase<List<NewMessageResponseBase<string>>>> CardImportStockFromProvider(
        StockCardImportApiRequest request);

    Task<NewMessageResponseBase<string>> CheckTransCardFromProvider(
       StockCardApiCheckTransRequest request);
}