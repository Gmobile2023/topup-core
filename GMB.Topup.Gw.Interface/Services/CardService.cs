using System;
using System.Threading.Tasks;
using GMB.Topup.Gw.Model;


using Microsoft.Extensions.Logging;
using GMB.Topup.Discovery.Requests.Workers;
using GMB.Topup.Shared;
using GMB.Topup.Shared.ConfigDtos;
using ServiceStack;

namespace GMB.Topup.Gw.Interface.Services;

public partial class TopupService
{
    public async Task<object> PostAsync(CardSaleRequest cardSaleRequest)
    {
        try
        {
            _logger.LogInformation("CardSaleRequest {Request}", cardSaleRequest.ToJson());
            var request = cardSaleRequest.ConvertTo<WorkerPinCodeRequest>();
            request.RequestDate = DateTime.Now;
            request.RequestIp = Request.UserHostAddress;
            var response = await _grpcClient.GetClientCluster(GrpcServiceName.Worker).SendAsync(request);
            _logger.LogInformation($"CardSaleRequest return: {response.ResponseStatus.ToJson()}");
            return response;
        }
        catch (Exception e)
        {
            _logger.LogError(e,$"{cardSaleRequest.TransCode}-CardSaleRequest error:{e.Message}");
            return new NewMessageResponseBase<object>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_WaitForResult,
                    "Giao dịch chưa có kết quả. Vui lòng liên hệ CSKH để được hỗ trợ")
            };
        }
    }
}