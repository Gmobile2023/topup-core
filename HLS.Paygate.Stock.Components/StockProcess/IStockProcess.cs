using System.Collections.Generic;
using System.Threading.Tasks;
using HLS.Paygate.Shared;
using HLS.Paygate.Shared.Dtos;
using Paygate.Discovery.Requests.Stocks;

namespace HLS.Paygate.Stock.Components.StockProcess;

public interface IStockProcess
{
    Task<NewMessageReponseBase<object>> ExchangeRequest(StockCardExchangeRequest request);
    Task<NewMessageReponseBase<object>> InportRequest(StockCardImportRequest request);
    Task<NewMessageReponseBase<int>> CheckInventoryRequest(StockCardCheckInventoryRequest request);

    Task<NewMessageReponseBase<List<CardRequestResponseDto>>> ExportCardToSaleRequest(
        StockCardExportSaleRequest request);

    Task<NewMessageReponseBase<string>> InportListRequest(StockCardImportListRequest request);

    Task<NewMessageReponseBase<List<NewMessageReponseBase<string>>>> CardImportStockFromProvider(
        StockCardImportApiRequest request);

    Task<NewMessageReponseBase<string>> CheckTransCardFromProvider(
       StockCardApiCheckTransRequest request);
}