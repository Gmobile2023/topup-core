using System;
using System.Threading.Tasks;
using HLS.Paygate.Shared;
using HLS.Paygate.TopupGw.Components.Connectors;
using HLS.Paygate.TopupGw.Contacts.Dtos;
using HLS.Paygate.TopupGw.Contacts.Enums;
using Microsoft.Extensions.Logging;
using Paygate.Discovery.Requests.TopupGateways;
using ServiceStack;

namespace HLS.Paygate.TopupGw.Components.TopupGwProcess;

public partial class TopupGwProcess
{
    public async Task<NewMessageReponseBase<ResponseProvider>> PayBillRequest(GatePayBillRequest request)
    {
        try
        {
            var response = new NewMessageReponseBase<ResponseProvider>();
            _logger.LogInformation("PayBillRequest" + request.ToJson());
            var amount = request.Amount;
            if (amount <= 0)
            {
                response.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_00,
                    "Số tiền thanh toán không hợp lệ");
                return response;
            }

            var receiverInfo = request.ReceiverInfo;
            if (string.IsNullOrEmpty(receiverInfo))
            {
                response.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_00,
                    "Thông tin nhận không tồn tại");
                return response;
            }

            var transRef = request.TransRef;
            if (string.IsNullOrEmpty(transRef))
            {
                response.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_00,
                    "Mã giao dịch đối tác không tồn tại");
                return response;
            }

            var providerCode = request.ProviderCode;
            if (string.IsNullOrEmpty(providerCode))
            {
                response.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_00,
                    "Provider not found");
                return response;
            }
            var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(providerCode);

            if (providerInfo == null)
            {
                _logger.LogInformation("providerInfo is null");
                return response;
            }
            var transRequest = new PayBillRequestLogDto
            {
                ReceiverInfo = request.ReceiverInfo,
                Status = TransRequestStatus.Init,
                RequestDate = DateTime.Now,
                TransCode = request.TransCodeProvider,
                TransIndex = "V" + DateTime.Now.ToString("yyMMddHHmmssffff"),
                TransAmount = amount,
                TransRef = transRef,
                ProviderCode = providerCode,
                ProductCode = request.ProductCode,
                Vendor = request.Vendor,
                PartnerCode = request.PartnerCode,
                ReferenceCode = request.ReferenceCode,
                ServiceCode = request.ServiceCode,
                CategoryCode = request.CategoryCode,
                ResponseInfo = request.Info
            };


            transRequest = await _topupGatewayService.PayBillRequestLogCreateAsync(transRequest);
            _logger.LogInformation($"PayBillRequest CreatedLog:{request.TransRef}-{request.TransCodeProvider}");
            if (transRequest != null)
            {
                _gatewayConnector = HostContext.Container.ResolveNamed<IGatewayConnector>(providerCode.Split('-')[0]); //_connectorFactory.GetServiceByKey(providerCode);
                var startTime = DateTime.Now;
                _logger.LogInformation($"GatewayConnector {_gatewayConnector.ToJson()} PayBillRequest:{request.TransRef}-{request.TransCodeProvider}-{request.ProviderCode}");
                var result = await _gatewayConnector.PayBillAsync(transRequest);
                var endTime = DateTime.Now;
                var processedTime = endTime.Subtract(startTime).TotalSeconds;
                if (providerInfo.ProcessTimeAlarm > 0 && processedTime > providerInfo.ProcessTimeAlarm)
                {
                    await AlarmProcessedTime(result, transRequest.ConvertTo<SendWarningDto>(), providerInfo,Math.Round(processedTime));
                }
                response.ResponseStatus = new ResponseStatusApi(result.ResponseCode, result.ResponseMessage);
                response.Results = new ResponseProvider
                {
                    Code = result.ProviderResponseCode,
                    Message = result.ProviderResponseMessage,
                    ProviderResponseTransCode = result.ProviderResponseTransCode,
                    ReceiverType = result.ReceiverType,
                };
                //Cảnh báo nếu gd lỗi
                if (providerInfo.IsAlarm && result.ResponseCode != ResponseCodeConst.Success)
                {
                    await AlarmProvider(result, transRequest.ConvertTo<SendWarningDto>(), providerInfo);
                }
                return response;
            }

            _logger.LogInformation("Error create transRequest with: " + transRef);
            response.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error, "Fail to create request");
            return response;
        }
        catch (Exception e)
        {
            _logger.LogError(
                $"{request.TransRef}-{request.TransCodeProvider}-{request.ProviderCode}-PayBillRequestError: " + e);
            return new NewMessageReponseBase<ResponseProvider>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_WaitForResult,
                    "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ. Xin vui lòng thử lại sau")
            };
        }
    }
}