using System;
using System.Linq;
using System.Threading.Tasks;
using Topup.Shared;
using Topup.Shared.ConfigDtos;
using Topup.Shared.Dtos;
using Microsoft.Extensions.Logging;
using Topup.Discovery.Requests.TopupGateways;
using Topup.Discovery.Requests.Workers;
using ServiceStack;

namespace Topup.Worker.Components.WorkerProcess;

public partial class WorkerProcess
{
    public async Task<NewMessageResponseBase<InvoiceResultDto>> BillQueryRequest(WorkerBillQueryRequest request)
    {
        try
        {
            _logger.LogInformation("BillQueryRequest:{Request}", request.ToJson());
            var serviceConfiguration = await _externalServiceConnector.ServiceConfigurationAsync(request.TransCode,
                request.PartnerCode,
                request.ServiceCode,
                request.CategoryCode, request.ProductCode);

            if ((serviceConfiguration == null || serviceConfiguration.Count == 0) &&
                !string.IsNullOrEmpty(request.PartnerCode))
                serviceConfiguration = await _externalServiceConnector.ServiceConfigurationAsync(request.TransCode,
                    null,
                    request.ServiceCode,
                    request.CategoryCode, request.ProductCode);

            if (serviceConfiguration != null && serviceConfiguration.Count > 0)
            {
                var serviceConfig = serviceConfiguration.OrderBy(c => c.Priority).First();
                _logger.LogInformation("BillQueryRequest:{ProviderCode}", serviceConfig.ProviderCode);
                var queryResult = await _grpcClient.GetClientCluster(GrpcServiceName.TopupGateway).SendAsync(new GateBillQueryRequest
                {
                    CategoryCode = request.CategoryCode,
                    IsInvoice = request.IsInvoice,
                    ProductCode = request.ProductCode,
                    ProviderCode = serviceConfig.ProviderCode,
                    ReceiverInfo = request.ReceiverInfo,
                    ServiceCode = request.ServiceCode,
                    TransRef = request.TransCode,
                    Vendor = request.ProductCode.Split('_')[0]
                });
                _logger.LogInformation("BillQueryRequest:{Response}", queryResult.ToJson());
                return queryResult;
            }

            return new NewMessageResponseBase<InvoiceResultDto>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error,
                    "Dịch vụ chưa được thiết lập. Vui lòng liên hệ CSKH để được hỗ trợ")
            };
        }
        catch (Exception e)
        {
            _logger.LogError($"{request.ReceiverInfo}-BillQueryRequest:{e}");
            return new NewMessageResponseBase<InvoiceResultDto>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_BillException,
                    "Không thể truy vấn thông tin. Vui lòng thử lại sau")
            };
        }
    }
}