using System;
using System.Threading.Tasks;
using Topup.Gw.Model;
using Microsoft.Extensions.Logging;
using Topup.Contracts.Commands.Commons;
using Topup.Discovery.Requests.Workers;
using Topup.Shared;
using Topup.Shared.ConfigDtos;
using Topup.Shared.Dtos;
using ServiceStack;

namespace Topup.Gw.Interface.Services;

public partial class TopupService
{
    //private readonly Logger _payBillLogger = LogManager.GetLogger("PayBillService");
    public async Task<object> PostAsync(PayBill payBill)
    {
        try
        {
            _logger.LogInformation("PayBill Request {Request}", payBill.ToJson());
            var returnMessage = new NewMessageResponseBase<object>();
            var request = payBill.ConvertTo<WorkerPayBillRequest>();
            request.RequestDate = DateTime.Now;
            request.RequestIp = Request.UserHostAddress;
            var response = await _grpcClient.GetClientCluster(GrpcServiceName.Worker).SendAsync(request);

            _logger.LogInformation($"{payBill.TransCode}-PayBill return:{response.ToJson()}");
            returnMessage.ResponseStatus = response.ResponseStatus;
            if (response.ResponseStatus.ErrorCode == ResponseCodeConst.Success)
                returnMessage.Results = response.Results;

            if (payBill.IsSaveBill && returnMessage.ResponseStatus.ErrorCode == ResponseCodeConst.Success)
                await SaveBillAsync(payBill, returnMessage.ResponseStatus.ErrorCode == ResponseCodeConst.Success,
                    returnMessage.ToJson());

            return returnMessage;
        }
        catch (Exception e)
        {
            _logger.LogError($"{payBill.TransCode}-PayBill error:{e}");
            return new NewMessageResponseBase<object>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_WaitForResult,
                    "Giao dịch chưa có kết quả. Vui lòng liên hệ CSKH để được hỗ trợ")
            };
        }
    }

    public async Task<object> GetAsync(BillQuery billQuery)
    {
        _logger.LogInformation("BillQuery Request {Request}", billQuery.ToJson());
        var response = await _grpcClient.GetClientCluster(GrpcServiceName.Worker).SendAsync(new WorkerBillQueryRequest
        {
            CategoryCode = billQuery.CategoryCode,
            PartnerCode = billQuery.PartnerCode,
            ProductCode = billQuery.ProductCode,
            RequestIp = Request.UserHostAddress,
            ReceiverInfo = billQuery.ReceiverInfo,
            TransCode = billQuery.TransCode,
            ServiceCode = ServiceCodes.QUERY_BILL,
            RequestDate = DateTime.Now
        });
        _logger.LogInformation($"{billQuery.TransCode}-BillQuery return:{response.ToJson()}");
        return response;
    }

    private async Task SaveBillAsync(PayBill request, bool isSuccess, string description)
    {
        try
        {
            var info = request.ExtraInfo.FromJson<InvoiceDto>();
            _logger.LogError("SaveBill request: {Request}", info.ToJson());
            await _bus.Publish<PayBillSaveCommand>(new
            {
                request.Channel,
                AccountCode = request.StaffAccount, //lấy theo NV
                request.CategoryCode,
                request.ProductCode,
                info?.ProductName,
                InvoiceCode = request.ReceiverInfo,
                request.ServiceCode,
                InvoiceInfo = request.ExtraInfo,
                LastTransCode = request.TransCode,
                IsLastSuccess = isSuccess,
                //LastProviderCode = request.Provider,
                Description = description
            });
        }
        catch (Exception e)
        {
            _logger.LogError("SaveBill error: {Message}", e.Message);
        }
    }
}