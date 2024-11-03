using System.Threading.Tasks;
using GMB.Topup.Stock.Components.StockProcess;
using Microsoft.Extensions.Logging;
using GMB.Topup.Discovery.Requests.Stocks;
using ServiceStack;

namespace GMB.Topup.Stock.Components.ApiServices;

public class StockProcessService : Service
{
    private readonly ILogger<StockProcessService> _logger;
    private readonly IStockProcess _stockProcess;

    public StockProcessService(IStockProcess stockProcess, ILogger<StockProcessService> logger)
    {
        _stockProcess = stockProcess;
        _logger = logger;
    }

    public async Task<object> PostAsync(StockCardCheckInventoryRequest request)
    {
        _logger.LogInformation($"StockCardCheckInventoryRequest request: {request.ToJson()}");
        var result = await _stockProcess.CheckInventoryRequest(request);
        _logger.LogInformation($"StockCardCheckInventoryRequest return: {result.ToJson()}");
        return result;
    }

    public async Task<object> PostAsync(StockCardExchangeRequest request)
    {
        _logger.LogInformation($"StockCardExchangeRequest request: {request.ToJson()}");
        var result = await _stockProcess.ExchangeRequest(request);
        _logger.LogInformation($"StockCardExchangeRequest return: {result.ToJson()}");
        return result;
    }

    public async Task<object> PostAsync(StockCardImportRequest request)
    {
        _logger.LogInformation($"StockCardImportRequest request: {request.ToJson()}");
        var result = await _stockProcess.InportRequest(request);
        _logger.LogInformation($"StockCardImportRequest return: {result.ToJson()}");
        return result;
    }

    public async Task<object> PostAsync(StockCardImportListRequest request)
    {
        _logger.LogInformation($"StockCardImportListRequest request: BatchCode= {request.BatchCode}|ProductCode= {request.ProductCode}|Count= {(request?.CardItems.Count)}");
        var result = await _stockProcess.InportListRequest(request);
        _logger.LogInformation($"StockCardImportListRequest return: {result.ToJson()}");
        return result;
    }

    public async Task<object> PostAsync(StockCardExportSaleRequest request)
    {
        _logger.LogInformation($"StockCardExportSaleRequest request: {request.ToJson()}");
        var result = await _stockProcess.ExportCardToSaleRequest(request);
        _logger.LogInformation($"StockCardExportSaleRequest return: {result.ResponseStatus.ToJson()}");
        return result;
    }
}