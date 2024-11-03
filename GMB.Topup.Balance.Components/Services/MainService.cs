using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using GMB.Topup.Balance.Domain.Services;
using GMB.Topup.Balance.Models.Dtos;
using GMB.Topup.Balance.Models.Requests;
using GMB.Topup.Discovery.Requests.Balance;
using GMB.Topup.Shared;
using Microsoft.Extensions.Logging;

using ServiceStack;

namespace GMB.Topup.Balance.Components.Services;

public partial class MainService : Service
{
    private readonly IBalanceService _balanceService;
    private readonly ILogger<MainService> _logger;

    public MainService(IBalanceService balanceService,
        ILogger<MainService> logger)
    {
        _balanceService = balanceService;
        _logger = logger;
    }

    public async Task<object> PostAsync(TransferRequest transferRequest)
    {
        try
        {
            _logger.LogInformation("Received TransferRequest: " + transferRequest.ToJson());
            var rs = await _balanceService.TransferAsync(transferRequest);
            _logger.LogInformation("TransferRequest return: " + rs.ToJson());
            return rs;
        }
        catch (Exception ex)
        {
            _logger.LogError($"TransferRequest error: {ex}");
            return new HttpResult(new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = $"Chuyển tiền lỗi: {ex}"
            }, HttpStatusCode.Forbidden);
        }
    }

    public async Task<object> PostAsync(DepositRequest depositRequest)
    {
        try
        {
            _logger.LogInformation("Received DepositRequest: " + depositRequest.ToJson());
            var rs = await _balanceService.DepositAsync(depositRequest);
            _logger.LogInformation("DepositRequest return: " + rs.ToJson());
            return rs;
        }
        catch (Exception ex)
        {
            _logger.LogError($"DepositRequest error: {ex}");
            return new HttpResult(new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = $"Nạp tiền lỗi: {ex}"
            }, HttpStatusCode.Forbidden);
        }
    }

    public async Task<object> PostAsync(CashOutRequest cashOutRequest)
    {
        try
        {
            _logger.LogInformation("Received CashoutRequest: " + cashOutRequest.ToJson());
            var rs = await _balanceService.CashOutAsync(cashOutRequest);
            _logger.LogInformation("CashoutRequest return: " + rs.ToJson());
            return rs;
        }
        catch (Exception ex)
        {
            _logger.LogError($"CashoutRequest error: {ex}");
            return new HttpResult(new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = $"Rút tiền lỗi: {ex}"
            }, HttpStatusCode.Forbidden);
        }
    }

    public async Task<NewMessageResponseBase<BalanceResponse>> PostAsync(BalancePaymentRequest paymentRequest)
    {
        _logger.LogInformation("Received PaymentRequest: " + paymentRequest.ToJson());
        var rs = await _balanceService.PaymentAsync(paymentRequest);
        _logger.LogInformation("PaymentRequest return: " + rs.ToJson());
        BalanceResponse balance = null;
        if (rs.ResponseCode == ResponseCodeConst.Success)
            balance = rs.Payload.ConvertTo<BalanceResponse>();

        return new NewMessageResponseBase<BalanceResponse>
        {
            ResponseStatus = new ResponseStatusApi(rs.ResponseCode, rs.ResponseMessage),
            Results = new BalanceResponse()
            {
                TransactionCode = balance != null ? balance.TransactionCode : "",
                SrcBalance = balance != null ? balance.SrcBalance : 0,
                DesBalance = balance != null ? balance.DesBalance : 0,
            }
        };
    }

    public async Task<object> PostAsync(PriorityPaymentRequest request)
    {
        _logger.LogInformation("Received PriorityPaymentRequest: " + request.ToJson());
        var rs = await _balanceService.PriorityAsync(request);
        _logger.LogInformation("PriorityPaymentRequest return: " + rs.ToJson());
        return rs;
    }

    public async Task<object> PostAsync(RevertRequest revertRequest)
    {
        _logger.LogInformation("Received RevertRequest: " + revertRequest.ToJson());
        var rs = await _balanceService.RevertAsync(revertRequest);
        _logger.LogInformation("RevertRequest return: " + rs.ToJson());
        return rs;
    }

    public async Task<object> PostAsync(BalanceCancelPaymentRequest request)
    {
        _logger.LogInformation("BalanceCancelPaymentRequest request: " + request.ToJson());
        var rs = await _balanceService.CancelPaymentAsync(request);
        _logger.LogInformation("BalanceCancelPaymentRequest return: " + rs.ToJson());
        BalanceResponse balance = null;
        if (rs.ResponseCode == ResponseCodeConst.Success)
            balance = rs.Payload.ConvertTo<BalanceResponse>();

        return new NewMessageResponseBase<BalanceResponse>
        {
            ResponseStatus = new ResponseStatusApi(rs.ResponseCode, rs.ResponseMessage),
            Results = new BalanceResponse()
            {
                TransactionCode = rs.TransCode,
                SrcBalance = balance != null ? balance.SrcBalance : 0,
                DesBalance = balance != null ? balance.DesBalance : 0,
            }
        };
    }

    public async Task<object> PostAsync(CorrectRequest correctRequest)
    {
        return await Task.FromResult(Task.CompletedTask);
    }

    public async Task<object> PostAsync(CollectDiscountRequest correctRequest)
    {
        try
        {
            _logger.LogInformation("Received CollectDiscountRequest: " + correctRequest.ToJson());
            var rs = await _balanceService.CollectDiscountAsync(correctRequest);
            _logger.LogInformation("CollectDiscountRequest return: " + rs.ToJson());
            return rs;
        }
        catch (Exception ex)
        {
            _logger.LogError($"CollectDiscountRequest error: {ex}");
            return new HttpResult(new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = $"Lỗi: {ex}"
            }, HttpStatusCode.Forbidden);
        }
    }

    public async Task<object> PostAsync(ChargingRequest request)
    {
        try
        {
            _logger.LogInformation("ChargingRequest: " + request.ToJson());
            var rs = await _balanceService.ChargingAsync(request);
            _logger.LogInformation("ChargingRequest return: " + rs.ToJson());
            return rs;
        }
        catch (Exception ex)
        {
            _logger.LogError($"ChargingRequest error: {ex}");
            return new HttpResult(new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = $"ChargingRequest error: {ex}"
            }, HttpStatusCode.Forbidden);
        }
    }

    public async Task<object> PostAsync(MasterTopupRequest masterTopupRequest)
    {
        _logger.LogInformation("Received MasterTopupRequest: " + masterTopupRequest.ToJson());
        var rs = await _balanceService.MasterTopupAysnc(masterTopupRequest);
        _logger.LogInformation("masterTopupRequest return: " + rs.ToJson());
        return rs;
    }

    public async Task<object> PostAsync(MasterTopdownRequest masterTopdownRequest)
    {
        return await Task.FromResult(Task.CompletedTask);
    }

    public async Task<object> PostAsync(AccountUpdateStatusRequest accountUpdateStatusRequest)
    {
        return await Task.FromResult(Task.CompletedTask);
    }

    public async Task<object> GetAsync(AccountBalanceCheckRequest accountBalanceCheckRequest)
    {
        _logger.LogInformation("Received AccountBalanceCheckRequest: " + accountBalanceCheckRequest.ToJson());
        var rs = await _balanceService.AccountBalanceCheckAsync(accountBalanceCheckRequest);
        _logger.LogInformation("AccountBalanceCheckRequest return: " + rs.ToJson());
        return new ResponseMessageApi<decimal>
        {
            Result = rs,
            Success = true
        };
    }

    public async Task<object> GetAsync(BalanceHistoriesRequest request)
    {
        _logger.LogInformation($"BalanceHistoriesRequest request: {request.ToJson()}");
        var rs = await _balanceService.GetSettlementSelectByAsync(request);
        _logger.LogInformation($"BalanceHistoriesRequest return: {rs.ResponseCode} - {rs.ResponseMessage}");
        return rs;
    }

    public async Task<object> GetAsync(BalanceDayRequest request)
    {
        _logger.LogInformation($"BalanceDayRequest: {request.ToJson()}");
        var rs = await _balanceService.GetSettlementBalanceDayByAsync(request);
        _logger.LogInformation($"BalanceDayRequest return: {rs.ResponseCode} - {rs.ResponseMessage}");
        return rs;
    }

    public async Task<List<string>> GetAsync(BalanceAccountCodesRequest request)
    {
        _logger.LogInformation($"AccountCodeRequest: {request.ToJson()}");
        var rs = await _balanceService.GetAccountCodeListAsync(request);
        _logger.LogInformation($"AccountCodeRequest return: {rs}");
        return rs;
    }

    public async Task<object> PostAsync(AdjustmentRequest request)
    {
        try
        {
            _logger.LogInformation("Received AdjustmentRequest: " + request.ToJson());
            var rs = await _balanceService.AdjustmentAsync(request);
            _logger.LogInformation("AdjustmentRequest return: " + rs.ToJson());
            return rs;
        }
        catch (Exception ex)
        {
            _logger.LogError($"AdjustmentRequest error: {ex}");
            return new HttpResult(new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = $"Giao dịch không thành công: {ex}"
            }, HttpStatusCode.Forbidden);
        }
    }

    public async Task<object> PostAsync(SaleDepositRequest request)
    {
        try
        {
            _logger.LogInformation("Received SaleDepositRequest: " + request.ToJson());
            var rs = await _balanceService.SaleDepositAsync(request);
            _logger.LogInformation("SaleDepositRequest return: " + rs.ToJson());
            return rs;
        }
        catch (Exception ex)
        {
            _logger.LogError($"SaleDepositRequest error: {ex}");
            return new HttpResult(new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = $"Giao dịch không thành công: {ex}"
            }, HttpStatusCode.Forbidden);
        }
    }

    public async Task<object> PostAsync(ClearDebtRequest request)
    {
        try
        {
            _logger.LogInformation("Received ClearDebtRequest: " + request.ToJson());
            var rs = await _balanceService.ClearDebtAsync(request);
            _logger.LogInformation("ClearDebtRequest return: " + rs.ToJson());
            return rs;
        }
        catch (Exception ex)
        {
            _logger.LogError($"ClearDebtRequest error: {ex}");
            return new HttpResult(new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = $"Giao dịch không thành công: {ex}"
            }, HttpStatusCode.Forbidden);
        }
    }

    public async Task<object> PostAsync(PaybatchRequest depositRequest)
    {
        try
        {
            _logger.LogInformation("Received PaybatchRequest: " + depositRequest.ToJson());
            var rs = await _balanceService.PayBatchAsync(depositRequest);
            _logger.LogInformation("PaybatchRequest return: " + rs.ToJson());
            return rs;
        }
        catch (Exception ex)
        {
            _logger.LogError($"PaybatchRequest error: {ex}");
            return new HttpResult(new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = $"Giao dịch không thành công: {ex}"
            }, HttpStatusCode.Forbidden);
        }
    }

    public async Task<object> PostAsync(BlockBalanceRequest request)
    {
        _logger.LogInformation("Received BlockBalanceRequest: " + request.ToJson());
        var rs = await _balanceService.BlockBalanceAsync(request);
        _logger.LogInformation("BlockBalanceRequest return: " + rs.ToJson());
        return rs;
    }

    public async Task<object> PostAsync(UnBlockBalanceRequest request)
    {
        _logger.LogInformation("Received UnBlockBalanceRequest: " + request.ToJson());
        var rs = await _balanceService.UnBlockBalanceAsync(request);
        _logger.LogInformation("UnBlockBalanceRequest return: " + rs.ToJson());
        return rs;
    }

    public async Task<object> GetAsync(AccountBalanceInfoCheckRequest request)
    {
        try
        {
            var returnMess = new ResponseMessageApi<object>();
            _logger.LogInformation("Received AccountBalanceInfoCheckRequest: " + request.ToJson());
            var rs = await _balanceService.AccountBalanceStateInfo(request.AccountCode, request.CurrencyCode);
            _logger.LogInformation("AccountBalanceInfoCheckRequest return: " + rs.ToJson());
            if (rs == null)
            {
                returnMess.Error.Message = "Không thành công";
                return returnMess;
            }

            returnMess.Success = true;
            returnMess.Result = new AccountBalanceInfo
            {
                Balance = rs.Balance,
                AvailableBalance = rs.AvailableBalance,
                BlockedMoney = rs.BlockedMoney
            };
            return returnMess;
        }
        catch (Exception ex)
        {
            _logger.LogError($"AccountBalanceInfoCheckRequest error: {ex}");
            return new ResponseMessageApi<object>
            {
                Error = new ErrorMessage
                {
                    Message = "Truy vấn thông tin không thành công"
                }
            };
        }
    }

    public async Task<object> PostAsync(TransferSystemRequest transferRequest)
    {
        try
        {
            _logger.LogInformation("Received TransferSystemRequest: " + transferRequest.ToJson());
            var rs = await _balanceService.TransferSystemAsync(transferRequest);
            _logger.LogInformation("TransferSystemRequest return: " + rs.ToJson());
            return rs;
        }
        catch (Exception ex)
        {
            _logger.LogError($"TransferSystemRequest error: {ex}");
            return new HttpResult(new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = $"Chuyển tiền lỗi: {ex}"
            }, HttpStatusCode.Forbidden);
        }
    }

    public async Task<object> PostAsync(BalancePayCommissionRequest request)
    {
        try
        {
            _logger.LogInformation("BalancePayCommissionRequest: " + request.ToJson());
            var rs = await _balanceService.PayCommissionAsync(request);
            _logger.LogInformation("BalancePayCommission return: " + request.ToJson());
            BalanceResponse balance = null;
            if (rs.ResponseCode == ResponseCodeConst.Success)
                balance = rs.Payload.ConvertTo<BalanceResponse>();

            return new NewMessageResponseBase<BalanceResponse>
            {
                ResponseStatus = new ResponseStatusApi(rs.ResponseCode, rs.ResponseMessage),
                Results = new BalanceResponse()
                {
                    TransactionCode = rs.TransCode,
                    SrcBalance = balance != null ? balance.SrcBalance : 0,
                    DesBalance = balance != null ? balance.DesBalance : 0,
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"BalancePayCommissionRequest error: {ex}");
            return new HttpResult(new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = $"Nạp tiền lỗi: {ex}"
            }, HttpStatusCode.Forbidden);
        }
    }
}