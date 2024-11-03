using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using GMB.Topup.TopupGw.Components.Connectors;
using GMB.Topup.Shared;


using Microsoft.Extensions.Logging;
using GMB.Topup.Discovery.Requests.TopupGateways;
using GMB.Topup.TopupGw.Contacts.ApiRequests;
using GMB.Topup.TopupGw.Contacts.Dtos;
using ServiceStack;

namespace GMB.Topup.TopupGw.Components.ApiServices;

public partial class MainService
{
    public async Task<object> PostAsync(GateTopupRequest topupRequest)
    {
        //_logger.LogInformation("GateTopupRequest: {Request}", topupRequest.ToJson());
        Console.WriteLine($"GateTopupRequest: {Request}", topupRequest.TransRef);
        var rs = await _topupGatewayProcess.TopupRequest(topupRequest);
        //_logger.LogInformation("GateTopupRequest_Return:{Response}", rs.ToJson());
        return rs;
    }

    public async Task<object> GetAsync(GateBillQueryRequest billQueryRequest)
    {
        Console.WriteLine($"GateBillQueryRequest: {Request}", billQueryRequest.TransRef);
        //_logger.LogInformation("GateBillQueryRequest:{Request}", billQueryRequest.ToJson());
        var rs = await _topupGatewayProcess.BillQueryRequest(billQueryRequest);
        //_logger.LogInformation("GateBillQueryRequest_Return:{Response}", rs.ToJson());
        return rs;
    }

    public async Task<object> PostAsync(GatePayBillRequest payBillRequest)
    {
        Console.WriteLine($"GatePayBillRequest: {Request}", payBillRequest.TransRef);
        //_logger.LogInformation("GatePayBillRequest: {Request}", payBillRequest.ToJson());
        var rs = await _topupGatewayProcess.PayBillRequest(payBillRequest);
        //_logger.LogInformation("GatePayBillRequest: {Response}", rs.ToJson());
        return rs;
    }

    public async Task<object> PostAsync(GateCardRequest cardRequest)
    {
        Console.WriteLine($"GateCardRequest: {Request}", cardRequest.TransRef);
        //_logger.LogInformation("Received cardRequest: {Request}", cardRequest.ToJson());
        var rs = await _topupGatewayProcess.GetCardToSaleRequest(cardRequest);
        //_logger.LogInformation("GateCardRequestReturn:{Response}", rs.ToJson());
        return rs;
    }

    public async Task<object> PostAsync(GateCardBathToStockRequest cardRequest)
    {
        Console.WriteLine($"GateCardBathToStockRequest: {Request}", cardRequest.TransRef);
        //_logger.LogInformation("Received GateCardBathToStockRequest: {Request}", cardRequest.ToJson());
        var rs = await _topupGatewayProcess.GateCardBathToStockRequest(cardRequest);
        //_logger.LogInformation("GateCardBathToStockRequest:{Response}", rs.ToJson());
        return rs;
    }

    public async Task<object> GetAsync(CheckBalanceRequest checkBalanceRequest)
    {
        _logger.LogInformation($"CheckBalanceRequest:{checkBalanceRequest.ToJson()}");
        var connector =
            HostContext.Container.ResolveNamed<IGatewayConnector>(checkBalanceRequest.ProviderCode.Split('-')[0]);
        //_logger.LogInformation($"GatewayConnector {connector.ToJson()} CheckBalance:{checkBalanceRequest.ToJson()}");
        var result = await connector.CheckBalanceAsync(checkBalanceRequest.ProviderCode,
            DateTime.Now.ToString("yyMMddHHmmssffff"));
        _logger.LogInformation($"CheckBalanceRequest return:{result.ToJson()}");
        if (result.ResponseCode == ResponseCodeConst.Success)
            return new NewMessageResponseBase<string>
            {
                Results = result.Payload.ToString(),
                ResponseStatus = new ResponseStatusApi(result.ResponseCode, result.ResponseMessage)
            };

        return new NewMessageResponseBase<string>
        {
            Results = result.ResponseMessage,
            ResponseStatus = new ResponseStatusApi(result.ResponseCode,
                result.ResponseMessage +
                $"- Thông tin lỗi NCC: code:{result.ProviderResponseCode}-message:{result.ProviderResponseMessage}")
        };
    }

    public async Task<object> GetAsync(GateCheckTransRequest checkTransRequest)
    {
        var connector =
            HostContext.Container.ResolveNamed<IGatewayConnector>(checkTransRequest.ProviderCode.Split('-')[0]);
        //_logger.LogInformation($"GateCheckTrans request:{checkTransRequest.ToJson()}");
        var result = await connector.TransactionCheckAsync(checkTransRequest.ProviderCode,
            checkTransRequest.TransCodeToCheck,
            $"{checkTransRequest.TransCodeToCheck}_{DateTime.Now:ddMMyyyyhhmmssfff}",
            checkTransRequest.ServiceCode);
        //_logger.LogInformation($"GateCheckTrans return:{result.ToJson()}");
        if (result.ResponseCode == ResponseCodeConst.Success)
            return new NewMessageResponseBase<ResponseProvider>
            {
                Results = new ResponseProvider
                {
                    Code = result.ProviderResponseCode,
                    Message = result.ProviderResponseMessage,
                    ReceiverType = result.ReceiverType,
                    ProviderResponseTransCode = result.ProviderResponseTransCode,
                    PayLoad = result.Payload.ToJson(),
                },
                ResponseStatus = new ResponseStatusApi(result.ResponseCode, result.ResponseMessage)
            };

        return new NewMessageResponseBase<ResponseProvider>
        {
            Results = new ResponseProvider
            { Code = result.ProviderResponseCode, Message = result.ProviderResponseMessage },
            ResponseStatus = new ResponseStatusApi(result.ResponseCode, result.ResponseMessage)
        };
    }

    public async Task<object> PostAsync(ViettelDepositRequest viettelDepositRequest)
    {
        _logger.LogInformation($"ViettelDepositRequest:{viettelDepositRequest.ToJson()}");
        if (viettelDepositRequest.Amount <= 0)
            throw new HttpError(HttpStatusCode.Forbidden);

        var providerCode = string.IsNullOrEmpty(viettelDepositRequest.ProviderCode)
            ? ProviderConst.VTT
            : viettelDepositRequest.ProviderCode;
        var connector = HostContext.Container.ResolveNamed<IGatewayConnector>(providerCode.Split('-')[0]);
        //_logger.LogInformation($"GatewayConnector {connector.ToJson()} Deposit:{viettelDepositRequest.ToJson()}");
        var transCode = DateTime.Now.ToString("yyMMddHHmmssffff");
        var response = await connector.DepositAsync(new DepositRequestDto
        {
            ProviderCode = providerCode,
            TransCode = transCode,
            Amount = viettelDepositRequest.Amount
        });
        _logger.LogInformation($"ViettelDepositRequest return:{response.ToJson()}");
        if (response != null)
        {
            return new NewMessageResponseBase<string>
            {
                Results = response.ProviderResponseCode + "|" + response.ProviderResponseMessage,
                ResponseStatus = new ResponseStatusApi(response.ResponseCode, response.ResponseMessage)
            };
        }

        throw new HttpError(HttpStatusCode.BadRequest);
    }

    public async Task<object> PostAsync(ProviderSalePriceInfoRequest request)
    {
        if (string.IsNullOrEmpty(request.ProviderCode))
            throw new HttpError(HttpStatusCode.BadRequest);

        var connector = HostContext.Container.ResolveNamed<IGatewayConnector>(request.ProviderCode.Split('-')[0]);
        var result = await connector.QueryAsync(new PayBillRequestLogDto()
        {
            ProviderCode = request.ProviderCode,
            ServiceCode = request.ServiceCode,
            CategoryCode = request.TopupType,
            ProductCode = request.TopupType,
            Vendor = request.ProviderType,
            ReceiverInfo = request.Account,
        });

        if (request.ServiceCode.StartsWith("SALEPRICE"))
        {
            var lst = result.ResponseStatus.Message.FromJson<List<ProviderSalePriceDto>>();
            await _topupGatewayService.ProviderSalePriceInfoCreateAsync(lst);
        }

        return new NewMessageResponseBase<ProviderInfoDto>
        {
            ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Success, "Thành công")
        };
    }

    public async Task<object> GetAsync(GetProviderProductInfo request)
    {
        if (string.IsNullOrEmpty(request.ProviderCode))
            throw new HttpError(HttpStatusCode.BadRequest);
        try
        {
            var connector = HostContext.Container.ResolveNamed<IGatewayConnector>(request.ProviderCode.Split('-')[0]);
            var rs = await connector.GetProductInfo(request);
            return rs;
        }
        catch
        {
            return new NewMessageResponseBase<ProviderInfoDto>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_Failed, "Thất bại")
            };
        }
    }

    public async Task<object> PostAsync(GateCheckMsIsdnRequest input)
    {
        if (string.IsNullOrEmpty(input.ProviderCode))
            throw new HttpError(HttpStatusCode.BadRequest);
        try
        {
            var connector = HostContext.Container.ResolveNamed<IGatewayConnector>(input.ProviderCode.Split('-')[0]);
            var rs = await connector.CheckPrePostPaid(input.MsIsdn, input.TransCode, input.ProviderCode);
            return new NewMessageResponseBase<string>
            {
                ResponseStatus = new ResponseStatusApi(rs.ResponseCode, rs.ResponseMessage),
                Results = rs.Payload != null ? rs.Payload.ToString() : string.Empty
            };
        }
        catch (Exception e)
        {
            _logger.LogError($"GateCheckMsIsdnRequest error {e}-{input.ToJson()}");
            return new NewMessageResponseBase<string>
            {
                ResponseStatus = new ResponseStatusApi("9999", "Thất bại")
            };
        }
    }
}