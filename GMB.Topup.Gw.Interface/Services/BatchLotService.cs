using System.Threading.Tasks;
using GMB.Topup.Gw.Model;
using Microsoft.Extensions.Logging;
using ServiceStack;

namespace GMB.Topup.Gw.Interface.Services;

public partial class TopupService
{
    public async Task<object> GetAsync(BatchListGetRequest request)
    {
        _logger.LogInformation("BatchListGetRequest Input: {Request}", request.ToJson());
        var response = await _saleService.BatchLotRequestGetListAsync(request);
        _logger.LogInformation("BatchListGetRequest response: {Response}", response.ToJson());

        return response;
    }

    public async Task<object> GetAsync(BatchDetailGetRequest request)
    {
        _logger.LogInformation("BatchDetailGetRequest Input: {Request}", request.ToJson());
        var response = await _saleService.BatchLotRequestGetDetailAsync(request);
        _logger.LogInformation("BatchDetailGetRequest response: {Response}", response.ToJson());

        return response;
    }

    public async Task<object> GetAsync(BatchSingleGetRequest request)
    {
        _logger.LogInformation("BatchSingleGetRequest Input: {Request}", request.ToJson());
        var response = await _saleService.BatchRequestSingleGetAsync(request.BatchCode);
        _logger.LogInformation("BatchSingleGetRequest response: {Response}", response.ToJson());

        return response;
    }

    public async Task<object> PostAsync(Batch_StopRequest request)
    {
        _logger.LogInformation("Batch_StopRequest Input: {Request}", request.ToJson());
        var response = await _saleService.BatchLotRequestStopAsync(request);
        _logger.LogInformation("Batch_StopRequest response: {Response}", response.ToJson());

        return response;
    }

    public async Task<object> PostAsync(PayBatchRequest request)
    {
        _logger.LogInformation("PayBatchRequest: {Request}", request.ToJson());
        var response = await _payBatchService.PayBatchProcess(request);
        _logger.LogInformation("PayBatchRequest: {Response}", response.ToJson());
        return response;
    }
}