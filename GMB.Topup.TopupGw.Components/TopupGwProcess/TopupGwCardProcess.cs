using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GMB.Topup.TopupGw.Components.Connectors;
using GMB.Topup.Shared;
using GMB.Topup.Shared.Dtos;


using Microsoft.Extensions.Logging;
using GMB.Topup.Discovery.Requests.TopupGateways;
using GMB.Topup.TopupGw.Contacts.Dtos;
using GMB.Topup.TopupGw.Contacts.Enums;
using ServiceStack;

namespace GMB.Topup.TopupGw.Components.TopupGwProcess;

public partial class TopupGwProcess
{
    public async Task<NewMessageResponseBase<List<CardRequestResponseDto>>> GetCardToSaleRequest(
        GateCardRequest cardRequest)
    {
        var returnMessage = new NewMessageResponseBase<List<CardRequestResponseDto>>
        {
            ResponseStatus = new ResponseStatusApi
            {
                ErrorCode = ResponseCodeConst.Error
            }
        };
        var transRequest = new CardRequestLogDto
        {
            Quantity = cardRequest.Quantity,
            Status = TransRequestStatus.Init,
            RequestDate = cardRequest.RequestDate,
            TransCode = cardRequest.TransCodeProvider,
            TransAmount = cardRequest.Amount,
            TransRef = cardRequest.TransRef,
            Vendor = cardRequest.Vendor,
            ProviderCode = cardRequest.ProviderCode,
            ProductCode = cardRequest.ProductCode,
            ReferenceCode = cardRequest.ReferenceCode
        };


        transRequest = await _topupGatewayService.CardRequestLogCreateAsync(transRequest);

        if (transRequest != null)
        {
            _gatewayConnector =
                HostContext.Container
                    .ResolveNamed<IGatewayConnector
                    >(cardRequest.ProviderCode.Split('-')[0]); // _connectorFactory.GetServiceByKey(cardRequest.ProviderCode);
            _logger.LogInformation(
                $"GatewayConnector {_gatewayConnector.ToJson()} CargGetByBatch:{transRequest.TransRef}-{transRequest.TransCode}-{transRequest.ProviderCode}");
            var result = await _gatewayConnector.CardGetByBatchAsync(transRequest);

            returnMessage.ResponseStatus.ErrorCode = result.ResponseCode;

            if (result.ResponseCode == ResponseCodeConst.ResponseCode_Success)
            {
                returnMessage.ResponseStatus.Message = "Success";
                returnMessage.Results = result.Payload.ConvertTo<List<CardRequestResponseDto>>();
            }
            else
            {
                returnMessage.ResponseStatus.Message = result.ResponseMessage;
            }
        }
        else
        {
            _logger.LogWarning("Error create transRequest with: {TransRef}", cardRequest.TransRef);
            returnMessage.ResponseStatus.Message = "Fail to create request";
        }

        return returnMessage;
    }

    public async Task<NewMessageResponseBase<List<CardRequestResponseDto>>> GateCardBathToStockRequest(
        GateCardBathToStockRequest request)
    {
        try
        {
            _logger.LogInformation($"GateCardBathToStockRequest:{request.ToJson()}");

            var transRef = request.TransRef;
            if (string.IsNullOrEmpty(transRef))
                throw new ArgumentNullException(nameof(transRef));

            var providerCode = request.ProviderCode;
            if (string.IsNullOrEmpty(providerCode))
                throw new ArgumentNullException(nameof(providerCode));
            var productCode = request.ProductCode;
            if (string.IsNullOrEmpty(productCode))
                throw new ArgumentNullException(nameof(productCode));

            var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(providerCode);

            if (providerInfo == null)
                throw new ArgumentNullException(nameof(productCode));

            var transRequest = new CardRequestLogDto
            {
                Quantity = request.Quantity,
                ProductCode = productCode,
                Status = TransRequestStatus.Init,
                RequestDate = DateTime.Now,
                TransCode = request.TransCodeProvider,
                TransRef = transRef,
                TransIndex = "C" + DateTime.Now.ToString("yyMMddHHmmssffff"),
                TransAmount = request.Amount,
                Vendor = string.IsNullOrEmpty(request.Vendor)
                    ? request.ProductCode.Split('_')[0]
                    : request.Vendor.Split('_')[0],
                ProviderCode = providerCode,
                CategoryCode = request.CategoryCde,
                PartnerCode = request.PartnerCode,
                ReferenceCode = request.ReferenceCode,
                ServiceCode = request.ServiceCode
            };


            if (string.IsNullOrEmpty(transRequest.TransCode))
            {
                if (request.ProviderCode.StartsWith(ProviderConst.VINNET))
                    transRequest.TransCode = Guid.NewGuid().ToString();
                transRequest.TransCode = DateTime.Now.ToString("yyMMddHHmmssffff");
            }

            transRequest = await _topupGatewayService.CardRequestLogCreateAsync(transRequest);

            if (transRequest != null)
            {
                _gatewayConnector =
                    HostContext.Container.ResolveNamed<IGatewayConnector>(providerCode.Split('-')[0]);
                _logger.LogInformation(
                    $"GatewayConnector {_gatewayConnector.ToJson()} CardRequest:{transRequest.TransRef}-{transRequest.TransCode}-{transRequest.ProviderCode}");
                var startTime = DateTime.Now;
                var result = await _gatewayConnector.CardGetByBatchAsync(transRequest);
                var endTime = DateTime.Now;
                var processedTime = endTime.Subtract(startTime).TotalSeconds;
                if (providerInfo.ProcessTimeAlarm > 0 && processedTime > providerInfo.ProcessTimeAlarm)
                {
                    await AlarmProcessedTime(result, transRequest.ConvertTo<SendWarningDto>(), providerInfo, Math.Round(processedTime));
                }
                _logger.LogInformation($"{transRequest.TransRef}|{transRequest.TransCode} Get card result: " +
                                       result.ToJson());
                //Cảnh báo nếu gd lỗi
                if (providerInfo.IsAlarm && result.ResponseCode != ResponseCodeConst.Success)
                {
                    await AlarmProvider(result, transRequest.ConvertTo<SendWarningDto>(), providerInfo);
                }
                if (result.ResponseCode == ResponseCodeConst.Success)
                {
                    var cards = result.Payload.ConvertTo<List<CardRequestResponseDto>>();
                    _logger.LogInformation(
                        $"{transRequest.TransRef}|{transRequest.TransCode} Get card reponse : {result.ResponseCode}|{result.ResponseMessage}");
                    return new NewMessageResponseBase<List<CardRequestResponseDto>>
                    {
                        ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Success, "Success"),
                        Results = cards
                    };
                }
                return new NewMessageResponseBase<List<CardRequestResponseDto>>
                {
                    ResponseStatus = new ResponseStatusApi(result.ResponseCode, result.ResponseMessage)
                };
            }

            _logger.LogInformation("Error create transRequest with: " + transRef);
            return new NewMessageResponseBase<List<CardRequestResponseDto>>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error, "Fail to create request")
            };
        }
        catch (Exception e)
        {
            return new NewMessageResponseBase<List<CardRequestResponseDto>>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error, e.Message)
            };
        }
    }
}