using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Topup.Balance.Models;
using Topup.Balance.Models.Dtos;
using Topup.Balance.Models.Enums;
using Topup.Balance.Models.Events;
using Topup.Balance.Models.Exceptions;
using Topup.Balance.Models.Grains;
using Topup.Balance.Models.Requests;
using Topup.Contracts.Commands.Commons;
using Topup.Contracts.Requests.Commons;
using Topup.Shared;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Orleans;


using Topup.Discovery.Requests.Balance;
using Topup.Gw.Model.Events;
using ServiceStack;
using Topup.Balance.Domain.Activities;
using Topup.Balance.Domain.Entities;
using Topup.Balance.Domain.Repositories;

namespace Topup.Balance.Domain.Services;

public class BalanceService : IBalanceService
{
    private readonly IBalanceMongoRepository _balanceMongoRepository;
    private readonly IBus _busReport;
    private readonly IGrainFactory _grainFactory;
    private readonly IConfiguration _configuration;

    //private readonly Logger _logger = LogManager.GetLogger("BalanceService");
    private readonly ILogger<BalanceService> _logger;
    //private readonly ITransactionReportService _transactionReportService;
    private readonly ITransactionService _transactionService;

    public BalanceService(IBalanceMongoRepository balanceMongoRepository, ITransactionService transactionService,
        IBus busReport,
        IConfiguration configuration,
        IGrainFactory grainFactory, ILogger<BalanceService> logger)
    {
        _balanceMongoRepository = balanceMongoRepository;
        _transactionService = transactionService;
        _busReport = busReport;
        _configuration = configuration;
        _grainFactory = grainFactory;
        _logger = logger;
    }

    public async Task<AccountBalanceDto> AccountBalanceGetAsync(string accountCode, string currencyCode)
    {
        var accountBalance = await _balanceMongoRepository.GetOneAsync<AccountBalance>(p =>
            p.AccountCode == accountCode && p.CurrencyCode == currencyCode && p.Status == BalanceStatus.Active);

        return accountBalance?.ConvertTo<AccountBalanceDto>();
    }

    public async Task<AccountBalanceDto> AccountBalanceCreateAsync(AccountBalanceDto accountBalanceDto)
    {
        try
        {
            var accountBalance = accountBalanceDto.ConvertTo<AccountBalance>();

            var code = accountBalanceDto.AccountCode.Split('*')[0];
            if (code == BalanceConst.MASTER_ACCOUNT
                || code == BalanceConst.CASHOUT_ACCOUNT
                || code == BalanceConst.CONTROL_ACCOUNT
                || code == BalanceConst.PAYMENT_ACCOUNT
               ) //Chỗ này chia tạm theo loại tk
                accountBalance.AccountType = BalanceAccountTypeConst.SYSTEM;
            else
                accountBalance.AccountType = BalanceAccountTypeConst.CUSTOMER;

            accountBalance.CheckSum = accountBalance.ToCheckSum();
            await _balanceMongoRepository.AddOneAsync(accountBalance);
            return accountBalance.ConvertTo<AccountBalanceDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError("Create account balance error: " + ex.Message);
        }

        return null;
    }


    public async Task<bool> AccountBalanceUpdateAsync(AccountBalanceDto accountBalanceDto)
    {
        _logger.LogInformation("AccountBalanceUpdateAsync request: {AccountCode}-{LastTransCode}-{Balance}",
            accountBalanceDto.AccountCode, accountBalanceDto.LastTransCode, accountBalanceDto.Balance);

        var accountBalance = _balanceMongoRepository.GetOne<AccountBalance>(p =>
            p.AccountCode == accountBalanceDto.AccountCode &&
            p.CurrencyCode == accountBalanceDto.CurrencyCode); //.GetById<AccountBalance>(accountBalanceDto.Id);

        if (accountBalance == null)
            throw new BalanceException(6001, $"Account {accountBalanceDto.AccountCode} has been null");

        if (!accountBalance.IsValid())
            throw new BalanceException(6001,
                $"Account {accountBalance.AccountCode} has been modified outside the system");

        if (accountBalanceDto.CheckSum != accountBalance.CheckSum)
            throw new BalanceException(6001, $"Account {accountBalance.AccountCode} has not valid checksum");

        accountBalance = accountBalanceDto.ConvertTo<AccountBalance>();
        accountBalance.CheckSum = accountBalance.ToCheckSum();
        var retry = 0;
        var update = await UpdateBalance(accountBalance);
        while (!update && retry < 3)
        {
            retry++;
            update = await UpdateBalance(accountBalance);
        }

        if (!update)
        {
            _logger.LogError("Update balance error. Retry not ok {AccountCode} -Info: {Json}",
                accountBalanceDto.AccountCode, accountBalance.ToJson());
            await _busReport.Publish<SendBotMessage>(new
            {
                MessageType = BotMessageType.Error,
                BotType = BotType.Dev,
                Module = "Balance",
                Title = $"Số dư tài khoản {accountBalance.AccountCode} retry update không thành công. Vui lòng kiểm tra ngay",
                Message = $"Info: {accountBalance.ToJson()}\n",
                CorrelationId = Guid.NewGuid()
            });
        }
        return update;
    }

    private async Task<bool> UpdateBalance(AccountBalance accountBalance)
    {
// #if DEBUG
//         if (accountBalance.AccountCode.Contains("PAYMENT"))
//             return false;
// #endif
        
        try
        {
            return await _balanceMongoRepository.UpdateOneAsync(accountBalance);
        }
        catch (Exception ex)
        {
            _logger.LogError("Update balance for {AccountCode} -Info: {Json} Error: {Message}",
                accountBalance.AccountCode, accountBalance.ToJson(), ex.Message);
            await _busReport.Publish<SendBotMessage>(new
            {
                MessageType = BotMessageType.Error,
                BotType = BotType.Dev,
                Module = "Balance",
                Title = $"Số dư tài khoản {accountBalance.AccountCode} update không thành công. Vui lòng kiểm tra ngay",
                Message = $"Info: {accountBalance.ToJson()}\n" +
                          $"Error {ex.Message}\n",
                CorrelationId = Guid.NewGuid()
            });
            return false;
        }
    }

    public async Task<MessageResponseBase> CheckCurrencyAsync(string currencyCode)
    {
        var checkCurrencyExist =
            await _balanceMongoRepository.AnyAsync<Currency>(p => p.CurrencyCode == currencyCode);
        if (!checkCurrencyExist)
            return new MessageResponseBase
            {
                ResponseCode = "6000",
                ResponseMessage = $"CurrencyCode {currencyCode} does not exist"
            };

        return new MessageResponseBase
        {
            ResponseCode = ResponseCodeConst.Success,
            ResponseMessage = "ok"
        };
    }

    public async Task CurrencyCreateAsync(string currencyCode)
    {
        var checkCurrencyExist =
            await _balanceMongoRepository.AnyAsync<Currency>(p => p.CurrencyCode == currencyCode);

        if (!checkCurrencyExist)
            await _balanceMongoRepository.AddOneAsync(new Currency
            {
                CurrencyCode = currencyCode
            });
    }


    public async Task<MessageResponseBase> TransferAsync(TransferRequest transferRequest)
    {
        try
        {
            _logger.LogInformation("Received TransferRequest: " + transferRequest.TransRef);
            var responseMessage = await CheckCurrencyAsync(transferRequest.CurrencyCode);

            if (responseMessage.ResponseCode != ResponseCodeConst.Success)
                return responseMessage;

            if (transferRequest.Amount <= 0)
            {
                responseMessage.ResponseCode = ResponseCodeConst.Error;
                responseMessage.ResponseMessage = "Số tiền không hợp lệ";
                return responseMessage;
            }

            var checkBalance = await CheckBalance(transferRequest.SrcAccount, transferRequest.CurrencyCode,
                transferRequest.Amount);
            if (checkBalance.ResponseCode != ResponseCodeConst.Success)
                return checkBalance;

            responseMessage = await _transactionService.TransferAsync(transferRequest);
            if (responseMessage.ResponseCode != ResponseCodeConst.Success)
                return responseMessage;

            var transaction = (TransactionDto)responseMessage.Payload;

            var settlement = transaction.Settlements;

            responseMessage = await TransferMoneyAsync(transaction.Settlements);
            if (responseMessage.ResponseCode != ResponseCodeConst.Success)
            {
                transaction.Status = TransStatus.Error;
                await _transactionService.UpdateTransactionStatus(transaction);
                return responseMessage;
            }

            await PublishBalanceHisotryMessage(transaction, (BalanceResponse)responseMessage.Payload);
            return responseMessage;
        }
        catch (Exception ex)
        {
            _logger.LogError($"{transferRequest.ToJson()}-TransferRequest error: {ex}");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = $"Chuyển tiền lỗi: {ex}"
            };
        }
    }

    public async Task<MessageResponseBase> DepositAsync(DepositRequest depositRequest)
    {
        try
        {
            _logger.LogInformation("Received DepositRequest: " + depositRequest.TransRef);
            var responseMessage = await CheckCurrencyAsync(depositRequest.CurrencyCode);

            if (responseMessage.ResponseCode != ResponseCodeConst.Success)
                return responseMessage;

            if (depositRequest.Amount <= 0)
            {
                responseMessage.ResponseCode = ResponseCodeConst.Error;
                responseMessage.ResponseMessage = "Số tiền không hợp lệ";
                return responseMessage;
            }

            var checkBalance = await CheckBalance(BalanceConst.MASTER_ACCOUNT, depositRequest.CurrencyCode,
                depositRequest.Amount);
            if (checkBalance.ResponseCode != ResponseCodeConst.Success)
                return checkBalance;

            responseMessage = await _transactionService.DepositAsync(depositRequest);
            if (responseMessage.ResponseCode != ResponseCodeConst.Success)
                return responseMessage;

            var transaction = (TransactionDto)responseMessage.Payload;

            responseMessage = await TransferMoneyAsync(transaction.Settlements);
            _logger.LogInformation($"Deposit return: {responseMessage.ResponseCode}-{responseMessage.ResponseMessage}");
            if (responseMessage.ResponseCode != ResponseCodeConst.Success)
            {
                transaction.Status = TransStatus.Error;
                await _transactionService.UpdateTransactionStatus(transaction);
                return responseMessage;
            }

            await PublishBalanceHisotryMessage(transaction, (BalanceResponse)responseMessage.Payload);
            return responseMessage;
        }
        catch (Exception ex)
        {
            _logger.LogError($"{depositRequest.ToJson()}-DepositRequest error: {ex}");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = $"Nạp tiền lỗi: {ex}"
            };
        }
    }

    public async Task<MessageResponseBase> CashOutAsync(CashOutRequest cashOutRequest)
    {
        try
        {
            _logger.LogInformation("Received CashOutRequest: " + cashOutRequest.ToJson());
            var responseMessage = await CheckCurrencyAsync(cashOutRequest.CurrencyCode);

            if (responseMessage.ResponseCode != ResponseCodeConst.Success)
                return responseMessage;

            if (cashOutRequest.Amount <= 0)
            {
                responseMessage.ResponseCode = ResponseCodeConst.Error;
                responseMessage.ResponseMessage = "Số tiền không hợp lệ";
                return responseMessage;
            }

            var checkBalance = await CheckBalance(cashOutRequest.AccountCode, cashOutRequest.CurrencyCode,
                cashOutRequest.Amount);
            if (checkBalance.ResponseCode != ResponseCodeConst.Success)
                return checkBalance;

            responseMessage = await _transactionService.CashOutAsync(cashOutRequest);
            if (responseMessage.ResponseCode != ResponseCodeConst.Success)
                return responseMessage;

            var transaction = (TransactionDto)responseMessage.Payload;

            responseMessage = await TransferMoneyAsync(transaction.Settlements);
            if (responseMessage.ResponseCode != ResponseCodeConst.Success)
            {
                transaction.Status = TransStatus.Error;
                await _transactionService.UpdateTransactionStatus(transaction);
                return responseMessage;
            }

            await PublishBalanceHisotryMessage(transaction, (BalanceResponse)responseMessage.Payload);
            return responseMessage;
        }
        catch (Exception ex)
        {
            _logger.LogError($"{cashOutRequest.ToJson()}-CashoutRequest error: {ex}");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = $"Rút tiền lỗi: {ex}"
            };
        }
    }

    public async Task<MessageResponseBase> CollectDiscountAsync(CollectDiscountRequest correctRequest)
    {
        try
        {
            _logger.LogInformation("Received CollectDiscountRequest: " + correctRequest.ToJson());
            var responseMessage = await CheckCurrencyAsync(CurrencyCode.VND.ToString("G"));

            if (responseMessage.ResponseCode != ResponseCodeConst.Success)
                return responseMessage;

            responseMessage = await _transactionService.CollectDiscountAsync(correctRequest);
            if (responseMessage.ResponseCode != ResponseCodeConst.Success)
                return responseMessage;

            var transaction = (TransactionDto)responseMessage.Payload;

            responseMessage = await TransferMoneyAsync(transaction.Settlements);
            _logger.LogInformation($"CollectDiscountRequest return: {responseMessage.ToJson()}");
            if (responseMessage.ResponseCode != ResponseCodeConst.Success)
            {
                transaction.Status = TransStatus.Error;
                await _transactionService.UpdateTransactionStatus(transaction);
                return responseMessage;
            }

            await PublishBalanceHisotryMessage(transaction, (BalanceResponse)responseMessage.Payload);
            return responseMessage;
        }
        catch (Exception ex)
        {
            _logger.LogError($"{correctRequest.ToJson()}-CorrectRequest error: {ex}");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = $"Lỗi: {ex}"
            };
        }
    }

    public async Task<MessageResponseBase> MasterTopupAysnc(MasterTopupRequest masterTopupRequest)
    {
        var responseMessage = await CheckCurrencyAsync(masterTopupRequest.CurrencyCode);

        if (responseMessage.ResponseCode != ResponseCodeConst.Success)
            return responseMessage;

        if (masterTopupRequest.Amount <= 0)
        {
            responseMessage.ResponseCode = ResponseCodeConst.Error;
            responseMessage.ResponseMessage = "Số tiền không hợp lệ";
            return responseMessage;
        }

        responseMessage = await _transactionService.MasterTopupAsync(masterTopupRequest);
        if (responseMessage.ResponseCode != ResponseCodeConst.Success)
            return responseMessage;

        var transaction = (TransactionDto)responseMessage.Payload;
        responseMessage = await TransferMoneyAsync(transaction.Settlements);

        if (responseMessage.ResponseCode != ResponseCodeConst.Success)
        {
            transaction.Status = TransStatus.Error;
            await _transactionService.UpdateTransactionStatus(transaction);
            return responseMessage;
        }

        await PublishBalanceHisotryMessage(transaction, (BalanceResponse)responseMessage.Payload);
        return responseMessage;
    }

    public async Task<MessageResponseBase> PaymentAsync(BalancePaymentRequest paymentRequest)
    {
        try
        {
            _logger.LogInformation($"BalancePaymentRequest:{paymentRequest.TransCode}-{paymentRequest.TransRef}-{paymentRequest.AccountCode}");
            if (string.IsNullOrEmpty(paymentRequest.CurrencyCode))
                paymentRequest.CurrencyCode = CurrencyCode.VND.ToString("G");
            var responseMessage = await CheckCurrencyAsync(paymentRequest.CurrencyCode);
            _logger.LogInformation($"CheckCurrency return:{paymentRequest.TransCode}-{paymentRequest.TransRef}-{paymentRequest.AccountCode}");
            if (responseMessage.ResponseCode != ResponseCodeConst.Success)
                return responseMessage;

            if (paymentRequest.PaymentAmount <= 0)
            {
                responseMessage.ResponseCode = ResponseCodeConst.Error;
                responseMessage.ResponseMessage = "Số tiền không hợp lệ";
                return responseMessage;
            }

            var checkBalance = await CheckBalance(paymentRequest.AccountCode, paymentRequest.CurrencyCode,
                paymentRequest.PaymentAmount);
            _logger.LogInformation($"CheckBalance return:{paymentRequest.TransCode}-{paymentRequest.TransRef}-{paymentRequest.AccountCode}");
            if (checkBalance.ResponseCode != ResponseCodeConst.Success)
                return checkBalance;

            responseMessage = await _transactionService.PaymentAsync(paymentRequest);
            _logger.LogInformation(
                $"PaymentAsync:{responseMessage.ResponseCode}-{responseMessage.ResponseMessage}-{paymentRequest.TransCode}-{paymentRequest.TransRef}");
            if (responseMessage.ResponseCode != ResponseCodeConst.Success)
                return responseMessage;
            var transaction = (TransactionDto)responseMessage.Payload;

            responseMessage = await TransferMoneyAsync(transaction.Settlements);
            _logger.LogInformation(
                $"TransferMoneyAsync:{responseMessage.ResponseCode}-{responseMessage.ResponseMessage}-{paymentRequest.TransCode}-{paymentRequest.TransRef}");
            if (responseMessage.ResponseCode != ResponseCodeConst.Success)
            {
                transaction.Status = TransStatus.Error;
                await _transactionService.UpdateTransactionStatus(transaction);
                return responseMessage;
            }

            await PublishBalanceHisotryMessage(transaction, (BalanceResponse)responseMessage.Payload);
            return responseMessage;
        }
        catch (Exception e)
        {
            _logger.LogError($"{paymentRequest.ToJson()}-PaymentRequest error: {e}");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = $"Lỗi: {e}"
            };
        }
    }

    public async Task<MessageResponseBase> RevertAsync(RevertRequest revertRequest)
    {
        try
        {
            var transactionRevert = await _transactionService.TransactionGetByCode(revertRequest.TransactionCode);
            if (transactionRevert == null)
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error,
                    ResponseMessage = "Giao dịch không tồn tại"
                };
            if (transactionRevert.Status != TransStatus.Done)
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error,
                    ResponseMessage = "Giao dịch có trạng thái không thể revert"
                };

            if (revertRequest.RevertAmount <= 0)
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error,
                    ResponseMessage = "Số tiền không hợp lệ"
                };

            transactionRevert.Description = revertRequest.TransNote;
            var responseMessage = await _transactionService.RevertAsync(transactionRevert);
            if (responseMessage.ResponseCode != ResponseCodeConst.Success)
                return responseMessage;

            var transaction = (TransactionDto)responseMessage.Payload;

            responseMessage = await TransferMoneyAsync(transaction.Settlements);
            if (responseMessage.ResponseCode != ResponseCodeConst.Success)
            {
                transaction.Status = TransStatus.Error;
                await _transactionService.UpdateTransactionStatus(transaction);
                return responseMessage;
            }

            await PublishBalanceHisotryMessage(transaction, (BalanceResponse)responseMessage.Payload);
            return responseMessage;
        }
        catch (Exception e)
        {
            _logger.LogError($"{revertRequest.ToJson()}-RevertRequest error: {e}");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = $"Lỗi: {e}"
            };
        }
    }

    public async Task<MessageResponseBase> CancelPaymentAsync(BalanceCancelPaymentRequest request)
    {
        try
        {
            //_logger.LogInformation("Received CancelPaymentAsync: " + request.ToJson());
            var responseMessage = await CheckCurrencyAsync(request.CurrencyCode);

            if (responseMessage.ResponseCode != ResponseCodeConst.Success)
                return responseMessage;

            if (request.RevertAmount <= 0)
            {
                responseMessage.ResponseMessage = "Số tiền không hợp lệ";
                responseMessage.ResponseCode = ResponseCodeConst.Error;
                return responseMessage;
            }

            responseMessage = await _transactionService.CancelPaymentAsync(request);
            if (responseMessage.ResponseCode != ResponseCodeConst.Success)
                return responseMessage;

            var transaction = (TransactionDto)responseMessage.Payload;

            responseMessage = await TransferMoneyAsync(transaction.Settlements);
            if (responseMessage.ResponseCode != ResponseCodeConst.Success)
            {
                transaction.Status = TransStatus.Error;
                await _transactionService.UpdateTransactionStatus(transaction);
                return responseMessage;
            }

            await PublishBalanceHisotryMessage(transaction, (BalanceResponse)responseMessage.Payload);
            return responseMessage;
        }
        catch (Exception e)
        {
            _logger.LogError($"{request.ToJson()}-CancelPaymentAsync error: {e}");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = $"Lỗi: {e}"
            };
        }
    }

    public async Task<MessageResponseBase> PriorityAsync(PriorityPaymentRequest request)
    {
        try
        {
            _logger.LogInformation("Received PriorityAsync: " + request.ToJson());
            var responseMessage = await CheckCurrencyAsync(request.CurrencyCode);

            if (responseMessage.ResponseCode != ResponseCodeConst.Success)
                return responseMessage;

            responseMessage = await _transactionService.PriorityPaymentAsync(request);
            if (responseMessage.ResponseCode != ResponseCodeConst.Success)
                return responseMessage;

            var transaction = (TransactionDto)responseMessage.Payload;

            responseMessage = await TransferMoneyAsync(transaction.Settlements);
            if (responseMessage.ResponseCode != ResponseCodeConst.Success)
            {
                transaction.Status = TransStatus.Error;
                await _transactionService.UpdateTransactionStatus(transaction);
                return responseMessage;
            }

            await PublishBalanceHisotryMessage(transaction, (BalanceResponse)responseMessage.Payload);
            return responseMessage;
        }
        catch (Exception e)
        {
            _logger.LogError($"{request.ToJson()}-PriorityAsync error: {e}");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = $"Lỗi: {e}"
            };
        }
    }

    public async Task<decimal> AccountBalanceCheckAsync(
        AccountBalanceCheckRequest accountBalanceCheckRequest)
    {
        //_logger.LogInformation("Received AccountBalanceCheckRequest: " + accountBalanceCheckRequest.ToJson());
        var responseMessage = await CheckCurrencyAsync(accountBalanceCheckRequest.CurrencyCode);

        if (responseMessage.ResponseCode != ResponseCodeConst.Success)
            return 0;

        //_orleansClient.GetGrain<IBalanceGrain>()

        var balanceGrain = _grainFactory.GetGrain<IBalanceGrain>(accountBalanceCheckRequest.AccountCode + "|" +
                                                                  accountBalanceCheckRequest.CurrencyCode);

        // var balanceGrain =
        //     _client.GetGrain<IBalanceGrain>(accountBalanceCheckRequest.AccountCode + "|" +
        //                                     accountBalanceCheckRequest.CurrencyCode);
        return await balanceGrain.GetBalance();
    }

    public async Task<MessageResponseBase> ChargingAsync(ChargingRequest request)
    {
        try
        {
            //_logger.LogInformation("Received ChargingAsync: " + request.ToJson());
            var responseMessage = await CheckCurrencyAsync(request.CurrencyCode);

            if (responseMessage.ResponseCode != ResponseCodeConst.Success)
                return responseMessage;

            if (request.Amount <= 0)
            {
                responseMessage.ResponseMessage = "Số tiền không hợp lệ";
                responseMessage.ResponseCode = ResponseCodeConst.Error;
                return responseMessage;
            }

            var checkBalance = await CheckBalance(request.AccountCode, request.CurrencyCode,
                request.Amount);
            if (checkBalance.ResponseCode != ResponseCodeConst.Success)
                return checkBalance;

            responseMessage = await _transactionService.ChargingAsync(request);
            if (responseMessage.ResponseCode != ResponseCodeConst.Success)
                return responseMessage;

            var transaction = (TransactionDto)responseMessage.Payload;

            responseMessage = await TransferMoneyAsync(transaction.Settlements);
            _logger.LogInformation($"ChargingAsync return: {responseMessage.ToJson()}");
            if (responseMessage.ResponseCode != ResponseCodeConst.Success)
            {
                transaction.Status = TransStatus.Error;
                await _transactionService.UpdateTransactionStatus(transaction);
                return responseMessage;
            }

            await PublishBalanceHisotryMessage(transaction, (BalanceResponse)responseMessage.Payload);
            return responseMessage;
        }
        catch (Exception ex)
        {
            _logger.LogError($"{request.ToJson()}-ChargingRequest error: {ex}");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = $"ChargingRequest error: {ex}"
            };
        }
    }

    public async Task<List<string>> GetAccountCodeListAsync(BalanceAccountCodesRequest request)
    {
        //xem lại hàm này để làm gì
        if (string.IsNullOrEmpty(request.AccountCode))
        {
            var account = (await _balanceMongoRepository.GetAllAsync<AccountBalance>(p =>
                    p.CurrencyCode == request.CurrencyCode))
                .Select(c => c.AccountCode).ToList();

            return account;
        }
        else
        {
            var account = (await _balanceMongoRepository.GetAllAsync<AccountBalance>(p =>
                    p.CurrencyCode == request.CurrencyCode && p.AccountCode == request.AccountCode))
                .Select(c => c.AccountCode).ToList();

            return account;
        }
    }

    public async Task<MessageResponseBase> AdjustmentAsync(AdjustmentRequest request)
    {
        try
        {
            _logger.LogInformation("Received AdjustmentAsync: " + request.ToJson());
            var responseMessage = await CheckCurrencyAsync(request.CurrencyCode);

            if (responseMessage.ResponseCode != ResponseCodeConst.Success)
                return responseMessage;

            if (request.Amount <= 0)
            {
                responseMessage.ResponseMessage = "Số tiền không hợp lệ";
                responseMessage.ResponseCode = ResponseCodeConst.Error;
                return responseMessage;
            }

            var accountCheck = request.AdjustmentType == AdjustmentType.Decrease
                ? request.AccountCode
                : BalanceConst.MASTER_ACCOUNT;

            var checkBalance = await CheckBalance(accountCheck, request.CurrencyCode,
                request.Amount);
            if (checkBalance.ResponseCode != ResponseCodeConst.Success)
                return checkBalance;


            responseMessage = await _transactionService.AdjustmentAsync(request);
            if (responseMessage.ResponseCode != ResponseCodeConst.Success)
                return responseMessage;

            var transaction = (TransactionDto)responseMessage.Payload;

            responseMessage = await TransferMoneyAsync(transaction.Settlements);
            _logger.LogInformation($"Adjustment return: {responseMessage.ToJson()}");
            if (responseMessage.ResponseCode != ResponseCodeConst.Success)
            {
                transaction.Status = TransStatus.Error;
                await _transactionService.UpdateTransactionStatus(transaction);
                return responseMessage;
            }

            await PublishBalanceHisotryMessage(transaction, (BalanceResponse)responseMessage.Payload);
            return responseMessage;
        }
        catch (Exception ex)
        {
            _logger.LogError($"{request.ToJson()}-AdjustmentAsync error: {ex}");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = $"Giao dịch không thành công: {ex}"
            };
        }
    }

    public async Task<MessageResponseBase> ClearDebtAsync(ClearDebtRequest request)
    {
        try
        {
            var currencyCode = CurrencyCode.DEBT.ToString("G");
            _logger.LogInformation("Received ClearDebtRequest: " + request.ToJson());
            var responseMessage = await CheckCurrencyAsync(currencyCode);

            if (responseMessage.ResponseCode != ResponseCodeConst.Success)
                return responseMessage;

            if (request.Amount <= 0)
            {
                responseMessage.ResponseMessage = "Số tiền không hợp lệ";
                responseMessage.ResponseCode = ResponseCodeConst.Error;
                return responseMessage;
            }

            var checkBalance = await CheckBalance(request.AccountCode, currencyCode,
                request.Amount);
            if (checkBalance.ResponseCode != ResponseCodeConst.Success)
                return checkBalance;

            responseMessage = await _transactionService.ClearDebtAsync(request);
            if (responseMessage.ResponseCode != ResponseCodeConst.Success)
                return responseMessage;

            var transaction = (TransactionDto)responseMessage.Payload;

            responseMessage = await TransferMoneyAsync(transaction.Settlements);
            _logger.LogInformation($"ClearDebtRequest return: {responseMessage.ToJson()}");
            if (responseMessage.ResponseCode != ResponseCodeConst.Success)
            {
                transaction.Status = TransStatus.Error;
                await _transactionService.UpdateTransactionStatus(transaction);
                return responseMessage;
            }

            await PublishBalanceHisotryMessage(transaction, (BalanceResponse)responseMessage.Payload);
            return responseMessage;
        }
        catch (Exception ex)
        {
            _logger.LogError($"{request.ToJson()}-ClearDebtRequest error: {ex}");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = $"Giao dịch không thành công: {ex}"
            };
        }
    }

    public async Task<MessageResponseBase> SaleDepositAsync(SaleDepositRequest request)
    {
        try
        {
            _logger.LogInformation("Received SaleDepositAsync: " + request.ToJson());
            var responseMessage = await CheckCurrencyAsync(CurrencyCode.VND.ToString("G"));

            if (responseMessage.ResponseCode != ResponseCodeConst.Success)
                return responseMessage;

            if (request.Amount <= 0)
            {
                responseMessage.ResponseMessage = "Số tiền không hợp lệ";
                responseMessage.ResponseCode = ResponseCodeConst.Error;
                return responseMessage;
            }

            var checkBalance = await CheckBalance(BalanceConst.MASTER_ACCOUNT, CurrencyCode.VND.ToString("G"),
                request.Amount);
            if (checkBalance.ResponseCode != ResponseCodeConst.Success)
                return checkBalance;

            responseMessage = await _transactionService.SaleDepositAsync(request);
            if (responseMessage.ResponseCode != ResponseCodeConst.Success)
                return responseMessage;

            var transaction = (TransactionDto)responseMessage.Payload;

            responseMessage = await TransferMoneyAsync(transaction.Settlements);

            if (responseMessage.ResponseCode != ResponseCodeConst.Success)
            {
                transaction.Status = TransStatus.Error;
                await _transactionService.UpdateTransactionStatus(transaction);
                return responseMessage;
            }

            await PublishBalanceHisotryMessage(transaction, (BalanceResponse)responseMessage.Payload);
            return responseMessage;
        }
        catch (Exception e)
        {
            _logger.LogError($"{request.ToJson()}-SaleDepositAsync error: {e}");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = $"Giao dịch không thành công: {e}"
            };
        }
    }

    public async Task<MessageResponseBase> BlockBalanceAsync(BlockBalanceRequest request)
    {
        try
        {
            _logger.LogInformation("Received BlockBalanceAsync: " + request.ToJson());
            var responseMessage = await CheckCurrencyAsync(request.CurrencyCode);

            if (responseMessage.ResponseCode != ResponseCodeConst.Success)
                return responseMessage;
            if (request.BlockAmount <= 0)
            {
                responseMessage.ResponseCode = ResponseCodeConst.Error;
                responseMessage.ResponseMessage = "Số tiền phong tỏa không hợp lệ";
                return responseMessage;
            }

            var checkBalance = await CheckBalance(request.AccountCode, request.CurrencyCode,
                request.BlockAmount);
            if (checkBalance.ResponseCode != ResponseCodeConst.Success)
                return checkBalance;
            //Chỗ này k tạo transaction
            // responseMessage = await _transactionService.BlockBalanceAsync(request);
            // if (responseMessage.ResponseCode != ResponseCodeConst.Success)
            //     return responseMessage;

            //var transaction = (TransactionDto) responseMessage.Payload;

            //var settlement = transaction.Settlements[0];

            var isBlock = await BlockMoneyAsync(request.AccountCode, request.BlockAmount, request.CurrencyCode);
            if (!isBlock)
            {
                responseMessage.ResponseMessage = "Phong tỏa số dư không thành công";
                responseMessage.ResponseCode = ResponseCodeConst.Error;
                responseMessage.Payload = null;
                return responseMessage;
            }

            //await _transactionService.SettlementsInsertAsync(new List<SettlementDto> {settlement});
            var balanceInfo = await AccountBalanceStateInfo(request.AccountCode, request.CurrencyCode);
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Success,
                ResponseMessage = "Success",
                Payload = balanceInfo
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"{request.ToJson()}-BlockBalance error: {ex}");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Phong tỏa số dư không lỗi"
            };
        }
    }

    public async Task<MessageResponseBase> UnBlockBalanceAsync(UnBlockBalanceRequest request)
    {
        try
        {
            _logger.LogInformation("Received UnBlockBalanceAsync: " + request.ToJson());
            var responseMessage = await CheckCurrencyAsync(request.CurrencyCode);

            if (responseMessage.ResponseCode != ResponseCodeConst.Success)
                return responseMessage;

            if (request.UnBlockAmount <= 0)
            {
                responseMessage.ResponseCode = ResponseCodeConst.Error;
                responseMessage.ResponseMessage = "Số tiền giải phong tỏa không hợp lệ";
                return responseMessage;
            }

            // responseMessage = await _transactionService.UnBlockBalanceAsync(request);
            // if (responseMessage.ResponseCode != ResponseCodeConst.Success)
            //     return responseMessage;

            //var transaction = (TransactionDto) responseMessage.Payload;

            //var settlement = transaction.Settlements[0];
            var check = await GetBlockMoney(request.AccountCode, request.CurrencyCode);
            if (check - request.UnBlockAmount < 0)
            {
                responseMessage.ResponseCode = ResponseCodeConst.Error;
                responseMessage.ResponseMessage =
                    "Không thành công. Số tiền giải phong tỏa lớn hơn số tiền phong tỏa hiện tại";
                return responseMessage;
            }

            var isUnBlock =
                await UnBlockMoneyAsync(request.AccountCode, request.UnBlockAmount, request.CurrencyCode);
            if (!isUnBlock)
            {
                responseMessage.ResponseMessage = "Giải phong tỏa số dư không thành công";
                responseMessage.Payload = null;
                responseMessage.ResponseCode = ResponseCodeConst.Error;
                return responseMessage;
            }

            //await _transactionService.SettlementsInsertAsync(new List<SettlementDto> {settlement});
            var balanceInfo = await AccountBalanceStateInfo(request.AccountCode, request.CurrencyCode);
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Success,
                ResponseMessage = "Success",
                Payload = balanceInfo
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"{request.ToJson()}-UnBlockBalance error: {ex}");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Giải phong tỏa số dư không lỗi"
            };
        }
    }

    public async Task<ResponseMessageApi<List<PaybatchAccount>>> PayBatchAsync(PaybatchRequest request)
    {
        try
        {
            var response = new ResponseMessageApi<List<PaybatchAccount>>
            {
                Error = new ErrorMessage
                {
                    Code = 0
                }
            };
            _logger.LogInformation("Received PayBatchAsync: " + request.ToJson());
            var responseMessage = await CheckCurrencyAsync(request.CurrencyCode);

            if (responseMessage.ResponseCode != ResponseCodeConst.Success)
            {
                response.Error.Message = responseMessage.ResponseMessage;
                return response;
            }

            var totalAmount = request.Accounts.Sum(x => x.Amount);
            var checkBalance = await CheckBalance(BalanceConst.COMMISSION_ACCOUNT, request.CurrencyCode,
                totalAmount);
            if (checkBalance.ResponseCode != ResponseCodeConst.Success)
            {
                response.Error.Message = "Số dư tài khoản trả thưởng không đủ thực hiện cho giao dịch này";
                return response;
            }

            var listItem = new List<PaybatchAccount>();
            foreach (var item in request.Accounts)
            {
                _logger.LogInformation(
                    $"Process Create paybatch:{item.AccountCode}-{item.Amount}-{request.TransRef}");
                var payment = await _transactionService.PayBatchAsync(new PaybatchAccount
                {
                    Amount = item.Amount,
                    AccountCode = item.AccountCode,
                    TransRef = request.TransRef,
                    TransNote = request.TransNote
                });
                _logger.LogInformation(
                    $"Create paybatch:{item.AccountCode}-{item.Amount}-{request.TransRef} return:{payment.ToJson()}");
                if (payment.ResponseCode != ResponseCodeConst.Success) continue;
                var transaction = (TransactionDto)payment.Payload;

                var transfer = await TransferMoneyAsync(transaction.Settlements);
                _logger.LogInformation(
                    $"Paybatch balance account {item.AccountCode}-{item.Amount}-{item.TransRef} return: {transfer.ToJson()}");
                if (transfer.ResponseCode == ResponseCodeConst.Success)
                {
                    item.Success = true;
                    item.TransNote = "Thanh toán thành công";
                    item.TransRef = transaction.TransactionCode;
                    await PublishBalanceHisotryMessage(transaction, (BalanceResponse)transfer.Payload);
                }
                else
                {
                    item.Success = false;
                    item.TransNote = transfer.ResponseMessage;
                    transaction.Status = TransStatus.Error;
                    await _transactionService.UpdateTransactionStatus(transaction);
                }

                listItem.Add(item);
            }

            if (request.Accounts.Any(x => x.Success))
            {
                response.Success = true;
                response.Error.Message = "Giao dịch thành công. Vui lòng xem chi tiết kết quả quả thanh toán";
                response.Result = listItem;
                return response;
            }

            response.Error.Message = "Giao dịch thất bại";
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError($"{request.ToJson()}-BayBatch error: {ex}");
            return new ResponseMessageApi<List<PaybatchAccount>>
            {
                Error = new ErrorMessage
                {
                    Message = "Giao dịch không thành công"
                }
            };
        }
    }

    public async Task<AccountBalanceDto> AccountBalanceStateInfo(string accountCode, string currencyCode)
    {
        _logger.LogInformation($"Received AccountBalanceStateInfo: {accountCode}-{currencyCode}");
        var responseMessage = await CheckCurrencyAsync(currencyCode);

        if (responseMessage.ResponseCode != ResponseCodeConst.Success)
            return null;
        var balanceGrain = _grainFactory.GetGrain<IBalanceGrain>(accountCode + "|" + currencyCode);
        return await balanceGrain.GetBalanceAccount();
    }

    public async Task<MessageResponseBase> TransferSystemAsync(TransferSystemRequest transferRequest)
    {
        try
        {
            _logger.LogInformation("Received TransferSystemRequest: " + transferRequest.ToJson());
            if (!CheckAccountSystem(transferRequest.SrcAccount) || !CheckAccountSystem(transferRequest.DesAccount))
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error,
                    ResponseMessage = "Tài khoản không hợp lệ"
                };

            var responseMessage = await CheckCurrencyAsync(transferRequest.CurrencyCode);

            if (responseMessage.ResponseCode != ResponseCodeConst.Success)
                return responseMessage;

            if (transferRequest.Amount <= 0)
            {
                responseMessage.ResponseCode = ResponseCodeConst.Error;
                responseMessage.ResponseMessage = "Số tiền không hợp lệ";
                return responseMessage;
            }

            var checkBalance = await CheckBalance(transferRequest.SrcAccount, transferRequest.CurrencyCode,
                transferRequest.Amount);
            if (checkBalance.ResponseCode != ResponseCodeConst.Success)
                return checkBalance;

            responseMessage = await _transactionService.TransferSystemAsync(transferRequest);
            if (responseMessage.ResponseCode != ResponseCodeConst.Success)
                return responseMessage;

            var transaction = (TransactionDto)responseMessage.Payload;

            responseMessage = await TransferMoneyAsync(transaction.Settlements);
            if (responseMessage.ResponseCode != ResponseCodeConst.Success)
            {
                transaction.Status = TransStatus.Error;
                await _transactionService.UpdateTransactionStatus(transaction);
                return responseMessage;
            }

            await PublishBalanceHisotryMessage(transaction, (BalanceResponse)responseMessage.Payload);
            return responseMessage;
        }
        catch (Exception ex)
        {
            _logger.LogError($"{transferRequest.ToJson()}-TransferSystemAsync error: {ex}");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = $"Chuyển tiền lỗi: {ex}"
            };
        }
    }


    public async Task<MessageResponseBase> PayCommissionAsync(BalancePayCommissionRequest request)
    {
        try
        {
            _logger.LogInformation("PayCommissionAsync: " + request.ToJson());
            var responseMessage = await CheckCurrencyAsync(request.CurrencyCode);

            if (responseMessage.ResponseCode != ResponseCodeConst.Success)
                return responseMessage;

            if (request.Amount <= 0)
            {
                responseMessage.ResponseCode = ResponseCodeConst.Error;
                responseMessage.ResponseMessage = "Số tiền không hợp lệ";
                return responseMessage;
            }

            var checkBalance = await CheckBalance(BalanceConst.COMMISSION_ACCOUNT, request.CurrencyCode,
                request.Amount);
            if (checkBalance.ResponseCode != ResponseCodeConst.Success)
                return checkBalance;

            responseMessage = await _transactionService.PayCommissionAsync(request);
            if (responseMessage.ResponseCode != ResponseCodeConst.Success)
                return responseMessage;

            var transaction = (TransactionDto)responseMessage.Payload;

            responseMessage = await TransferMoneyAsync(transaction.Settlements);
            _logger.LogInformation($"Deposit return: {responseMessage.ToJson()}");
            if (responseMessage.ResponseCode != ResponseCodeConst.Success)
            {
                transaction.Status = TransStatus.Error;
                await _transactionService.UpdateTransactionStatus(transaction);
                return responseMessage;
            }

            await PublishBalanceHisotryMessage(transaction, (BalanceResponse)responseMessage.Payload);
            return responseMessage;
        }
        catch (Exception ex)
        {
            _logger.LogError($"{request.ToJson()}-DepositRequest error: {ex}");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = $"Cộng tiền hoa hồng lỗi: {ex}"
            };
        }
    }

    public bool CheckAccountSystem(string accountCode)
    {
        var accountCheck = accountCode.Split('*')[0];
        return accountCheck == BalanceConst.MASTER_ACCOUNT || accountCheck == BalanceConst.CASHOUT_ACCOUNT ||
               accountCheck == BalanceConst.CONTROL_ACCOUNT || accountCheck == BalanceConst.PAYMENT_ACCOUNT ||
               accountCheck == BalanceConst.COMMISSION_ACCOUNT;
    }

    private async Task<MessageResponseBase> TransferMoneyAsync(List<SettlementDto> settlementDtos)
    {
        var paymentTransCode = settlementDtos[0].TransRef;
        try
        {
            _logger.LogInformation($"TransferMoneyAsync request:{settlementDtos[0].PaymentTransCode}-{settlementDtos[0].TransRef}-{settlementDtos[0].TransCode}");
            var balanceResponse = new BalanceResponse
            {
                SrcBalance = -1,
                DesBalance = -1,
                TransactionCode = settlementDtos[0].TransRef//Mã gd của Transaction
            };
            var sagaBuilder = _grainFactory.CreateSaga();
            _logger.LogInformation($"TransferMoneyAsync CreateSaga:{settlementDtos[0].PaymentTransCode}-{settlementDtos[0].TransRef}-{settlementDtos[0].TransCode}");
            foreach (var settlementDto in settlementDtos)
            {
                if (settlementDto.SrcAccountCode == settlementDto.DesAccountCode)
                    return new MessageResponseBase(ResponseCodeConst.Error,
                        "Source account and Destination account must not the same");

                if (!string.IsNullOrEmpty(settlementDto.SrcAccountCode) &&
                    !string.IsNullOrEmpty(settlementDto.DesAccountCode))
                    sagaBuilder = sagaBuilder
                        .AddActivity<BalanceWithdrawActivity>
                        (
                            x => { x.Add(BalanceWithdrawActivity.SETTLEMENT, settlementDto); }
                        )
                        .AddActivity<BalanceDepositActivity>
                        (
                            x => { x.Add(BalanceWithdrawActivity.SETTLEMENT, settlementDto); }
                        );
                else if (!string.IsNullOrEmpty(settlementDto.SrcAccountCode))
                    sagaBuilder = sagaBuilder
                        .AddActivity<BalanceWithdrawActivity>
                        (
                            x => { x.Add(BalanceWithdrawActivity.SETTLEMENT, settlementDto); }
                        )
                        .AddActivity<NoneAccountActivity>
                        (
                            x => { x.Add("HasResult", settlementDto.ReturnResult); }
                        );
                else if (!string.IsNullOrEmpty(settlementDto.DesAccountCode))
                    sagaBuilder = sagaBuilder
                        .AddActivity<NoneAccountActivity>
                        (
                            x => { x.Add("HasResult", settlementDto.ReturnResult); }
                        )
                        .AddActivity<BalanceDepositActivity>
                        (
                            x => { x.Add(BalanceWithdrawActivity.SETTLEMENT, settlementDto); }
                        );
            }
            _logger.LogInformation($"Build saga:{settlementDtos[0].PaymentTransCode}-{settlementDtos[0].TransRef}-{settlementDtos[0].TransCode}");
            if (sagaBuilder != null)
            {
                var saga = await sagaBuilder.ExecuteSagaAsync();
                _logger.LogInformation($"ExecuteSagaAsync saga:{settlementDtos[0].PaymentTransCode}-{settlementDtos[0].TransRef}-{settlementDtos[0].TransCode}");
                var result = await saga.WaitForTransferResult(settlementDtos, 100);
                _logger.LogInformation($"WaitForTransferResult CreateSaga:{settlementDtos[0].PaymentTransCode}-{settlementDtos[0].TransRef}-{settlementDtos[0].TransCode}");
                if (result.Count > 0 && result.Count == settlementDtos.Count)
                {
                    await _transactionService.SettlementsInsertAsync(result);
                    _logger.LogInformation($"SettlementsInsertAsync:{settlementDtos[0].PaymentTransCode}-{settlementDtos[0].TransRef}-{settlementDtos[0].TransCode}");
                    if (result.Exists(p => p.Status != SettlementStatus.Done))
                    {
                        var error = await saga.GetSagaError();
                        _logger.LogError($"{paymentTransCode} - Transfer error: {error}");
                        // await saga.Dispose();
                        return new MessageResponseBase(ResponseCodeConst.Error,
                            "Error " + error);
                    }
                    balanceResponse.SrcBalance = result[0].SrcAccountBalance;
                    balanceResponse.DesBalance = result[^1].DesAccountBalance;
                    balanceResponse.BalanceAfterTrans = (from x in result
                                                         select new BalanceAfterTransDto
                                                         {
                                                             SrcAccount = x.SrcAccountCode,
                                                             SrcBeforeBalance = x.SrcAccountBalanceBeforeTrans,
                                                             SrcBalance = x.SrcAccountBalance,
                                                             DesAccount = x.DesAccountCode,
                                                             DesBeforeBalance = x.DesAccountBalanceBeforeTrans,
                                                             DesBalance = x.DesAccountBalance,
                                                             Amount = x.Amount,
                                                             CurrencyCode = x.CurrencyCode,
                                                             TransCode = x.TransRef,
                                                         }).ToList();

                    // await saga.Dispose();
                }
                else
                {
                    var error = await saga.GetSagaError();
                    _logger.LogError($"{paymentTransCode} - Transfer error with result: {error}");

                    // await saga.Dispose();
                    return new MessageResponseBase(ResponseCodeConst.Error,
                        error);
                }
            }

            return new MessageResponseBase(ResponseCodeConst.Success, "Success") { Payload = balanceResponse };
        }
        catch (Exception e)
        {
            _logger.LogError($"{paymentTransCode}-TransferMoneyAsync error: {e}");

            return new MessageResponseBase(ResponseCodeConst.Error,
                "Error");
        }
    }

    // private async Task<MessageResponseBase> TransferMoneyAsync(SettlementDto settlementDto)
    // {
    //     try
    //     {
    //         var returnVal = ((decimal) -1, (decimal) -1);
    //         var response = new MessageResponseBase();
    //
    //         if (settlementDto.SrcAccountCode == settlementDto.DesAccountCode)
    //         {
    //             response.ResponseCode = ResponseCodeConst.Error;
    //             response.ResponseMessage = "Src and Des must not equal";
    //             return response;
    //         }
    //
    //         if (!string.IsNullOrEmpty(settlementDto.SrcAccountCode) &&
    //             !string.IsNullOrEmpty(settlementDto.DesAccountCode))
    //             returnVal = await _clusterClient.GetGrain<ITransferGrain>(settlementDto.Id)
    //                 .Transfer(settlementDto.SrcAccountCode + "|" + settlementDto.CurrencyCode,
    //                     settlementDto.DesAccountCode + "|" + settlementDto.CurrencyCode, settlementDto.Amount,
    //                     settlementDto.TransCode);
    //         else if (!string.IsNullOrEmpty(settlementDto.SrcAccountCode))
    //             try
    //             {
    //                 returnVal = (await _clusterClient
    //                     .GetGrain<IBalanceGrain>(settlementDto.SrcAccountCode + "|" + settlementDto.CurrencyCode)
    //                     .Withdraw(settlementDto.Amount, settlementDto.TransCode), 0);
    //             }
    //             catch (Exception e)
    //             {
    //                 _logger.LogError($"{settlementDto.TransCode}-{settlementDto.TransCode}-Error withdraw: " +
    //                                  e.Message);
    //             }
    //         else if (!string.IsNullOrEmpty(settlementDto.DesAccountCode))
    //             try
    //             {
    //                 returnVal = (0, await _clusterClient
    //                     .GetGrain<IBalanceGrain>(settlementDto.DesAccountCode + "|" + settlementDto.CurrencyCode)
    //                     .Deposit(settlementDto.Amount, settlementDto.TransCode));
    //             }
    //             catch (Exception e)
    //             {
    //                 _logger.LogError($"{settlementDto.TransCode}-{settlementDto.TransCode}-Error Deposit: " +
    //                                  e.Message);
    //             }
    //
    //         if (returnVal.Item1 < 0 || returnVal.Item2 < 0)
    //         {
    //             response.ResponseCode = ResponseCodeConst.Error;
    //             response.ResponseMessage = "Transfer error";
    //             _logger.LogError($"{settlementDto.TransCode}-{settlementDto.TransCode}-Transfer error");
    //         }
    //         else
    //         {
    //             response.ResponseCode = ResponseCodeConst.Success;
    //             response.ResponseMessage = "Thành công";
    //             response.Payload = new BalanceReponse
    //             {
    //                 SrcBalance = returnVal.Item1,
    //                 DesBalance = returnVal.Item2
    //             };
    //         }
    //
    //         _logger.LogInformation(
    //             $"{settlementDto.TransCode}-{settlementDto.TransCode}-TransferAsync return: {response.ToJson()}");
    //         return response;
    //     }
    //     catch (Exception e)
    //     {
    //         _logger.LogError($"{settlementDto.TransCode}-{settlementDto.TransCode}-TransferMoneyAsync error:{e}");
    //         return new MessageResponseBase
    //         {
    //             ResponseCode = ResponseCodeConst.Error,
    //             ResponseMessage = "Không thành công"
    //         };
    //     }
    // }

    private async Task<bool> BlockMoneyAsync(string accountcode, decimal amount, string currencyCode)
    {
        try
        {
            var response = (await _grainFactory
                .GetGrain<IBalanceGrain>(accountcode + "|" + currencyCode)
                .BlockBalance(amount, null), 0);
            _logger.LogInformation($"BlockMoneyAsync retrurn:{response.ToJson()}");
            return response.Item1;
        }
        catch (Exception e)
        {
            _logger.LogError("Error BlockMoneyAsync: " + e);
            return false;
        }
    }

    private async Task<bool> UnBlockMoneyAsync(string accountcode, decimal amount, string currencyCode)
    {
        try
        {
            var response = (await _grainFactory
                .GetGrain<IBalanceGrain>(accountcode + "|" + currencyCode)
                .UnBlockBalance(amount, null), 0);
            _logger.LogInformation($"UnBlockMoneyAsync retrurn:{response.ToJson()}");
            return response.Item1;
        }
        catch (Exception e)
        {
            _logger.LogError("Error UnBlockMoneyAsync: " + e);
            return false;
        }
    }

    private async Task PublishBalanceHisotryMessage(TransactionDto request, BalanceResponse balanceResponse,
        string extraInfo = "")
    {
        try
        {
            //if (settlementDto != null)
            //{
            //    settlementDto.SrcAccountBalanceBeforeTrans = settlementDto.SrcAccountBalance + settlementDto.Amount;
            //    settlementDto.DesAccountBalanceBeforeTrans = settlementDto.DesAccountBalance - settlementDto.Amount;
            //}

            //try
            //{
            //    _logger.LogInformation($"BalanceHistoryCreateAsync request: {request.TransactionCode}");
            //    var rs = await _transactionReportService.BalanceHistoryCreateAsync(request, settlementDto);
            //    _logger.LogInformation($"Consume BalanceHistoryCreateAsync return: {rs.ToJson()}");
            //}
            //catch (Exception e)
            //{
            //    _logger.LogInformation($"Consume BalanceChanged error: {e}");
            //}


            //Đẩy luồng report mới
            var settlement = new SettlementReportDto()
            {
                Amount = Convert.ToDouble(request.Amount),
                CurrencyCode = request.CurrencyCode,
                CreatedDate = request.CreatedDate,
                DesAccountCode = request.DesAccountCode,
                SrcAccountCode = request.SrcAccountCode,
                DesAccountBalance = Convert.ToDouble(balanceResponse.DesBalance),
                TransactionType = request.TransType.ToString(),
                TransCode = request.TransactionCode,
                TransRef = request.TransRef,
                Description = request.Description,
            };

            if (request.TransType == TransactionType.SaleDeposit)
            {
                if (!string.IsNullOrEmpty(request.DesAccountCode))
                {
                    var data = balanceResponse.BalanceAfterTrans.Where(c => c.CurrencyCode == CurrencyCode.VND.ToString()).FirstOrDefault();
                    if (data != null)
                    {
                        settlement.DesAccountBalance = Convert.ToDouble(data.DesBalance);
                        settlement.DesAccountBalanceBeforeTrans = Convert.ToDouble(data.DesBeforeBalance);
                    }
                    else
                    {
                        settlement.DesAccountBalance = Convert.ToDouble(balanceResponse.DesBalance);
                        settlement.DesAccountBalanceBeforeTrans = Convert.ToDouble(balanceResponse.DesBalance - request.Amount);
                    }
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(request.SrcAccountCode))
                {
                    settlement.SrcAccountBalance = Convert.ToDouble(balanceResponse.SrcBalance);
                    settlement.SrcAccountBalanceBeforeTrans = Convert.ToDouble(balanceResponse.SrcBalance + request.Amount);
                }

                if (!string.IsNullOrEmpty(request.DesAccountCode))
                {
                    settlement.DesAccountBalance = Convert.ToDouble(balanceResponse.DesBalance);
                    settlement.DesAccountBalanceBeforeTrans = Convert.ToDouble(balanceResponse.DesBalance - request.Amount);
                }
            }

            await _busReport.Publish<ReportBalanceHistoriesMessage>(new
            {
                Transaction = request,
                Settlement = settlement,
                ExtraInfo = extraInfo
            });
        }
        catch (Exception e)
        {
            _logger.LogError($"PublishBalanceHisotryMessage error: {e}");
        }
    }

    private async Task<decimal> GetBlockMoney(string accountCode, string currencyCode)
    {
        var balanceGrain = _grainFactory.GetGrain<IBalanceGrain>(accountCode + "|" + currencyCode);
        return await balanceGrain.GetBlockMoney();
    }

    private async Task AutoDepositAccount(TransactionDto request, SettlementDto settlementDto)
    {
        try
        {
            var enable = bool.Parse(_configuration["BalanceConfig:AccountAutoDeposit:IsEnable"]);
            if (enable)
            {
                var accountConfigs = _configuration["BalanceConfig:AccountAutoDeposit:AccountCode"].Split(',');
                var minBalance = decimal.Parse(_configuration["BalanceConfig:AccountAutoDeposit:MinBalance"]);
                if (accountConfigs.Contains(settlementDto.SrcAccountCode) &&
                    settlementDto.SrcAccountBalance <= minBalance)
                    await _busReport.Publish<BalanceDepositMessage>(new
                    {
                        TransRef = request.TransactionCode,
                        TransNote = "Nạp tiền vào tài khoản",
                        Description = "Nạp tiền vào tài khoản",
                        CurrencyCode = CurrencyCode.VND.ToString("G"),
                        AccountCode = settlementDto.SrcAccountCode,
                        Amount = decimal.Parse(_configuration["BalanceConfig:AccountAutoDeposit:DepositAmount"])
                    });
            }
        }
        catch (Exception e)
        {
            _logger.LogError("AutoDepositAccount error:" + e);
        }
    }

    private async Task<MessageResponseBase> CheckBalance(string srcAccount, string currencyCode,
        decimal requestAmount)
    {
        try
        {
            var srcGrain = _grainFactory.GetGrain<IBalanceGrain>(srcAccount + "|" + currencyCode);
            var srcBalance = await srcGrain.GetBalance();

            if (srcBalance - requestAmount < 0)
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.ResponseCode_Balance_Not_Enough,
                    ResponseMessage = "Số dư tài khoản không đủ"
                };

            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.ResponseCode_Success,
                ResponseMessage = "Thành công"
            };
        }
        catch (Exception e)
        {
            _logger.LogError($"Checkbalance error:{srcAccount}-{e.Message}");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.ResponseCode_00,
                ResponseMessage = "Giao dịch không thành công"
            };
        }
    }

    public async Task<ResponseMesssageObject<string>> GetSettlementSelectByAsync(BalanceHistoriesRequest request)
    {
        try
        {

            var t1 = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var t2 = DateTime.Now.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss");
            _logger.LogInformation($"GetSettlementSelectByAsync input: DateNow= {t1}|DateNowToUniversalTime= {t2}");
            _logger.LogInformation($"GetSettlementSelectByAsync inputFromDate: DateNow= {request.FromDate.ToString("yyyy-MM-dd HH:mm:ss")}|DateNowToUniversalTime= {request.FromDate.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss")}");

            _logger.LogInformation($"FromDate= {request.FromDate.ToString("yyyy-MM-dd HH:mm:ss")}|ToDate= {request.ToDate.ToString("yyyy-MM-dd HH:mm:ss")}|AccountCode= {request.AccountCode}|TransCode= {request.TransCode}");



            #region ***Transaction***
            Expression<Func<Transaction, bool>> queryTrans = p => p.Status == TransStatus.Done && p.CreatedDate >= request.FromDate.ToUniversalTime() && p.CreatedDate <= request.ToDate.ToUniversalTime();
            if (!string.IsNullOrEmpty(request.AccountCode))
            {
                Expression<Func<Transaction, bool>> newQuery = p => p.DesAccountCode == request.AccountCode || p.SrcAccountCode == request.AccountCode;
                queryTrans = queryTrans.And(newQuery);
            }

            if (!string.IsNullOrEmpty(request.TransCode))
            {
                Expression<Func<Transaction, bool>> newQuery = p => p.TransactionCode == request.TransCode;
                queryTrans = queryTrans.And(newQuery);
            }

            if (!string.IsNullOrEmpty(request.TransRef))
            {
                Expression<Func<Transaction, bool>> newQuery = p => p.TransRef == request.TransRef;
                queryTrans = queryTrans.And(newQuery);
            }

            #endregion

            #region ***Settlement***

            Expression<Func<Settlement, bool>> querySettlement = p => p.CreatedDate >= request.FromDate.ToUniversalTime() && p.CreatedDate <= request.ToDate.ToUniversalTime();
            if (!string.IsNullOrEmpty(request.AccountCode))
            {
                Expression<Func<Settlement, bool>> newQuery = p => p.DesAccountCode == request.AccountCode || p.SrcAccountCode == request.AccountCode;
                querySettlement = querySettlement.And(newQuery);
            }

            if (!string.IsNullOrEmpty(request.TransCode))
            {
                Expression<Func<Settlement, bool>> newQuery = p => p.TransRef == request.TransCode;
                querySettlement = querySettlement.And(newQuery);
            }

            #endregion

            var dataTrans = await _balanceMongoRepository.GetAllAsync(queryTrans);
            var dataSettlements = await _balanceMongoRepository.GetAllAsync(querySettlement);


            var list = (from x in dataTrans
                        join y in dataSettlements on x.TransactionCode equals y.TransRef
                        select y).ToList();

            var lit = list.ConvertTo<List<SettlementDto>>();
            return new ResponseMesssageObject<string>()
            {
                ResponseCode = ResponseCodeConst.Success,
                ResponseMessage = "Thành công",
                Payload = lit.ToJson(),
                Total = lit.Count(),
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"FromDate= {request.FromDate.ToString("yyyy-MM-dd HH:mm:ss")}|ToDate= {request.ToDate.ToString("yyyy-MM-dd HH:mm:ss")}|AccountCode= {request.AccountCode}|TransCode= {request.TransCode} => GetSettlementSelectByAsync Exception: {ex}");
            return new ResponseMesssageObject<string>()
            {
                ResponseCode = ResponseCodeConst.Success,
                ResponseMessage = "Lỗi",
                Payload = "",
                Total = 0,
            };
        }
    }

    public async Task<ResponseMesssageObject<string>> GetSettlementBalanceDayByAsync(BalanceDayRequest request)
    {
        try
        {
            _logger.LogInformation($"GetSettlementBalanceDayByAsync AccountCode= {request.AccountCode}|Date= {request.Date.ToString("yyyy-MM-dd")}");
            var fromDate = request.Date;
            var toDate = request.Date.AddDays(1);

            #region ***Settlement***

            Expression<Func<Settlement, bool>> querySettlement = p => p.CreatedDate >= fromDate.ToUniversalTime()
            && p.Status == SettlementStatus.Done && p.CreatedDate < toDate.ToUniversalTime()
            && (p.DesAccountCode == request.AccountCode || p.SrcAccountCode == request.AccountCode);

            #endregion

            var lst = await _balanceMongoRepository.GetSortedPaginatedAsync<Settlement, Guid>(querySettlement, s => s.CreatedDate, false, 1, 1);
            if (lst == null)
            {
                fromDate = request.Date.AddDays(5);
                toDate = request.Date.AddDays(1);
                Expression<Func<Settlement, bool>> querySettlementNew = p => p.CreatedDate >= fromDate.ToUniversalTime() && p.Status == SettlementStatus.Done && p.CreatedDate < toDate.ToUniversalTime()
                && (p.DesAccountCode == request.AccountCode || p.SrcAccountCode == request.AccountCode);
                lst = await _balanceMongoRepository.GetSortedPaginatedAsync<Settlement, Guid>(querySettlementNew, s => s.CreatedDate, false, 1, 1);

            }

            var lit = lst.ConvertTo<SettlementDto>();

            return new ResponseMesssageObject<string>()
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Thành công",
                Payload = lst != null ? (lst.ConvertTo<SettlementDto>()).ToJson() : string.Empty,
                Total = 1,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"Date= {request.Date.ToString("yyyy-MM-dd")}|AccountCode= {request.AccountCode} => GetSettlementBalanceDayByAsync Exception: {ex}");
            return new ResponseMesssageObject<string>()
            {
                ResponseCode = ResponseCodeConst.Success,
                ResponseMessage = "Lỗi",
                Payload = "",
                Total = 0,
            };
        }
    }
}