using System;
using System.Threading.Tasks;
using HLS.Paygate.Gw.Model;
using HLS.Paygate.Shared;
using HLS.Paygate.Shared.ConfigDtos;
using Microsoft.Extensions.Logging;
using Paygate.Discovery.Requests.Workers;
using ServiceStack;

namespace HLS.Paygate.Gw.Interface.Services;

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
            return new NewMessageReponseBase<object>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_WaitForResult,
                    "Giao dịch chưa có kết quả. Vui lòng liên hệ CSKH để được hỗ trợ")
            };
        }
    }
}