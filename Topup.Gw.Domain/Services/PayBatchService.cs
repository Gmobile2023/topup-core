using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Topup.Gw.Model;
using Topup.Gw.Model.Dtos;
using Topup.Shared;
using Topup.Shared.ConfigDtos;
using Topup.Shared.Dtos;
using Topup.Shared.Helpers;
using MassTransit;
using Microsoft.Extensions.Logging;
using Nest;
using Topup.Discovery.Requests.Workers;
using ServiceStack;

namespace Topup.Gw.Domain.Services;

public class PayBatchService : IPayBatchService
{
    //private readonly IServiceGateway _gateway; gunner
    private readonly ILogger<PayBatchService> _logger;
    private readonly ISaleService _saleService;
    private readonly GrpcClientHepper _grpcClient;
    public PayBatchService(ISaleService saleService, ILogger<PayBatchService> logger, GrpcClientHepper grpcClient)
    {
        _saleService = saleService;
        _logger = logger;
        _grpcClient = grpcClient;
        //_gateway = HostContext.AppHost.GetServiceGateway(); gunner
    }

    public async Task<NewMessageResponseBase<BatchRequestDto>> PayBatchProcess(PayBatchRequest request)
    {
        //_logger.LogInformation("{Partner} PayBatchProcess: {Request}", request.PartnerCode, request.ToJson());
        var returnMessage = new NewMessageResponseBase<BatchRequestDto>();

        try
        {
            var batchLot = request.ConvertTo<BatchRequestDto>();
            batchLot.BatchCode = request.TransRef;
            batchLot.BatchType = request.BatchType;
            batchLot.Items = request.Items;
            batchLot = await _saleService.BatchLotRequestCreateAsync(batchLot);
            await Task.Factory.StartNew(async () =>
            {
                var batchTemp = batchLot;
                await ProcessingBatchLot(batchTemp);
            }).ConfigureAwait(false);

            _logger.LogInformation("PayBatchProcess {Code} received", batchLot.BatchCode);
            returnMessage.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Success, "Thành công");
            returnMessage.Results = batchLot;
            return returnMessage;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                $"{request.TransRef} PayBatchProcess:  {ex.Message}|{ex.StackTrace}|{ex.InnerException}");
            return new NewMessageResponseBase<BatchRequestDto>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error, "Lỗi")
            };
        }
    }

    private async Task<bool> ProcessingBatchLot(BatchRequestDto dto)
    {
        try
        {
            var batch = await _saleService.BatchRequestSingleGetAsync(dto.BatchCode);

            if (batch != null && batch.Status == BatchLotRequestStatus.Init)
            {
                batch.Status = BatchLotRequestStatus.Process;
                await _saleService.UpdateBatchRequestAsync(batch.BatchCode, BatchLotRequestStatus.Process);
            }
            else
            {
                return false;
            }           
           
            foreach (var item in dto.Items)
            {
                //1.cập item trạng thái đang xử lý
                var batchDetail =
                    await _saleService.BatchRequestGetDetailSingleAsync(batch.BatchCode, item.TransRef);
                if (batchDetail != null
                    && batchDetail.BatchStatus == BatchLotRequestStatus.Init
                    && batchDetail.Status == SaleRequestStatus.Init)
                {
                    await _saleService.UpdateBatchDetailStatusTowAsync(batch.BatchCode, item.TransRef,
                        BatchLotRequestStatus.Process, SaleRequestStatus.InProcessing);

                    //NewMessageReponseBase<object> responseMsg = null;
                    var pinCodeResult = new NewMessageResponseBase<List<CardRequestResponseDto>>();
                    var result = new NewMessageResponseBase<WorkerResult>();
                    if (batch.SaleRequestType == SaleRequestType.Topup)
                    {
                        //3.Topup
                        var topup = item.ConvertTo<TopupRequest>();
                        topup.Channel = dto.Channel;
                        topup.TransCode = item.TransRef;
                        topup.StaffAccount = dto.StaffAccount;
                        //result = await _gateway.SendAsync(topup);
                        result = await _grpcClient.GetClientCluster(GrpcServiceName.Worker).SendAsync(new WorkerTopupRequest
                        {
                            Amount = topup.Amount,
                            Channel = topup.Channel,
                            AgentType = dto.AgentType,
                            AccountType = dto.AccountType,
                            ParentCode = dto.ParentCode,
                            CategoryCode = topup.CategoryCode,
                            ProductCode = topup.ProductCode,
                            PartnerCode = topup.PartnerCode,
                            ReceiverInfo = topup.ReceiverInfo,
                            //RequestIp = Request.UserHostAddress,
                            ServiceCode = topup.ServiceCode,
                            StaffAccount = topup.StaffAccount,
                            TransCode = topup.TransCode,
                            RequestDate = DateTime.Now
                        });
                    }
                    else if (batch.SaleRequestType == SaleRequestType.PayBill)
                    {
                        //3.PayBill
                        var payBill = item.ConvertTo<PayBill>();
                        payBill.Channel = dto.Channel;
                        payBill.TransCode = item.TransRef;
                        payBill.StaffAccount = dto.StaffAccount;
                        //result = await _gateway.SendAsync(payBill);
                        result = await _grpcClient.GetClientCluster(GrpcServiceName.Worker).SendAsync(new WorkerPayBillRequest
                        {
                            Amount = payBill.Amount,
                            Channel = payBill.Channel,
                            AgentType = dto.AgentType,
                            AccountType = dto.AccountType,
                            ParentCode = dto.ParentCode,
                            CategoryCode = payBill.CategoryCode,
                            ProductCode = payBill.ProductCode,
                            ServiceCode = payBill.ServiceCode,
                            ExtraInfo = payBill.ExtraInfo,
                            PartnerCode = payBill.PartnerCode,
                            ReceiverInfo = payBill.ReceiverInfo,
                            //RequestIp = Request.UserHostAddress,
                            StaffAccount = payBill.StaffAccount,
                            TransCode = payBill.TransCode,
                            RequestDate = DateTime.Now
                        });
                    }
                    else if (batch.SaleRequestType == SaleRequestType.PinCode)
                    {
                        //3.PinCode
                        var card = item.ConvertTo<WorkerPinCodeRequest>();
                        card.Channel = dto.Channel;
                        card.TransCode = item.TransRef;
                        card.Quantity = item.Quantity;
                        card.ParentCode = dto.ParentCode;
                        card.CardValue = item.Amount;
                        card.StaffAccount = dto.StaffAccount;
                        card.AccountType = dto.AccountType;
                        card.AgentType = dto.AgentType;
                        card.RequestDate = DateTime.Now;
                        pinCodeResult = await _grpcClient.GetClientCluster(GrpcServiceName.Worker).SendAsync(card);

                        _logger.LogInformation($"{item.TransRef} Reponse: {pinCodeResult.ToJson()}");
                        result = new NewMessageResponseBase<WorkerResult>
                        {
                            ResponseStatus = pinCodeResult.ResponseStatus
                        };
                    }

                    _logger.LogInformation($"{item.TransRef} Reponse: {result.ToJson()}");
                    SaleRequestStatus statusCheck;
                    if (batch.SaleRequestType == SaleRequestType.PinCode &&
                        pinCodeResult.ResponseStatus.ErrorCode == ResponseCodeConst.Success)
                    {
                        statusCheck = SaleRequestStatus.Success;
                    }
                    else if (result.ResponseStatus.ErrorCode == ResponseCodeConst.Success)
                    {
                        statusCheck = SaleRequestStatus.Success;
                    }
                    else
                    {
                        var responseError = batch.SaleRequestType == SaleRequestType.PinCode
                            ? pinCodeResult.ResponseStatus.ErrorCode
                            : result.ResponseStatus.ErrorCode;
                        if (responseError == ResponseCodeConst.ResponseCode_WaitForResult
                            || responseError == ResponseCodeConst.ResponseCode_TimeOut
                            || responseError == ResponseCodeConst.ResponseCode_RequestReceived
                            || responseError == ResponseCodeConst.ResponseCode_InProcessing
                            || responseError == ResponseCodeConst.ResponseCode_Paid)
                            statusCheck = SaleRequestStatus.WaitForResult;
                        else statusCheck = SaleRequestStatus.Failed;
                    }

                    var saleResponse = await _saleService.SaleRequestGetTransRefAsync(item.TransRef, item.PartnerCode);
                    _logger.LogInformation(
                        $"{item.TransRef} checkRequestSale Reponse: {(saleResponse != null ? saleResponse.Status.ToString() : "null object")}");
                    //4.Cập nhật trạng thái item
                    var vendor = saleResponse != null ? saleResponse.Vendor : "";
                    var provider = saleResponse != null ? saleResponse.Provider : "";

                    await _saleService.UpdateBatchDetailStatusTowAsync(batch.BatchCode, item.TransRef,
                        BatchLotRequestStatus.Completed, statusCheck, vendor, provider);
                }
            }

            batch = await _saleService.BatchRequestSingleGetAsync(dto.BatchCode);

            if (batch != null && batch.Status == BatchLotRequestStatus.Process)
            {
                batch.Status = BatchLotRequestStatus.Completed;
                await _saleService.UpdateBatchRequestAsync(batch.BatchCode, batch.Status);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                $"{dto.BatchCode} ProcessingBatchLot Exception:  {ex.Message}|{ex.StackTrace}|{ex.InnerException}");
            return false;
        }
    }   
}