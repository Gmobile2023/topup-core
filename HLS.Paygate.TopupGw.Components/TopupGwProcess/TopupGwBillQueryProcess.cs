using System;
using System.Threading.Tasks;
using HLS.Paygate.Shared;
using HLS.Paygate.Shared.Dtos;
using HLS.Paygate.TopupGw.Components.Connectors;
using HLS.Paygate.TopupGw.Contacts.Dtos;
using Microsoft.Extensions.Logging;
using Paygate.Discovery.Requests.TopupGateways;
using ServiceStack;

namespace HLS.Paygate.TopupGw.Components.TopupGwProcess;

public partial class TopupGwProcess
{
    public async Task<NewMessageReponseBase<InvoiceResultDto>> BillQueryRequest(GateBillQueryRequest request)
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
            return new NewMessageReponseBase<InvoiceResultDto>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_BillException,
                    "Không thể truy vấn thông tin. Vui lòng thử lại sau")
            };
        }
    }
}