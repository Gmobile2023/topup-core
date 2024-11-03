using System;
using System.Threading.Tasks;
using GMB.Topup.TopupGw.Components.Connectors;
using GMB.Topup.Shared;
using GMB.Topup.Shared.Dtos;

using Microsoft.Extensions.Logging;
using GMB.Topup.Discovery.Requests.TopupGateways;
using GMB.Topup.TopupGw.Contacts.Dtos;
using ServiceStack;

namespace GMB.Topup.TopupGw.Components.TopupGwProcess;

public partial class TopupGwProcess
{
    public async Task<NewMessageResponseBase<InvoiceResultDto>> BillQueryRequest(GateBillQueryRequest request)
    {
        try
        {
            _logger.LogInformation("BillQueryRequest:" + request.ToJson());
            _gatewayConnector =
                HostContext.Container.ResolveNamed<IGatewayConnector>(
                    request.ProviderCode.Split('-')[0]); // _connectorFactory.GetServiceByKey(request.ProviderCode);
            //_logger.LogInformation($"GatewayConnector {_gatewayConnector.ToJson()} BillQueryRequest:{request.TransRef}-{request.ProviderCode}");
            var queryResult = await _gatewayConnector.QueryAsync(new PayBillRequestLogDto
            {
                TransCode = DateTime.Now.ToString("yyMMddHHmmssffff"),
                ReceiverInfo = request.ReceiverInfo,
                IsInvoice = request.IsInvoice,
                Vendor = request.Vendor,
                CategoryCode = request.CategoryCode,
                ProviderCode = request.ProviderCode,
                ProductCode = request.ProductCode,
                ServiceCode = request.ServiceCode
            });

            _logger.LogInformation($"{request.ReceiverInfo}-BillQueryReturn" + queryResult.ToJson());
            return queryResult;
        }
        catch (Exception e)
        {
            _logger.LogError($"{request.ReceiverInfo}-BillQuery:{e}");
            return new NewMessageResponseBase<InvoiceResultDto>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_BillException,
                    "Không thể truy vấn thông tin. Vui lòng thử lại sau")
            };
        }
    }
}