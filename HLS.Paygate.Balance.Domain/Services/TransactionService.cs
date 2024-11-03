using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using HLS.Paygate.Balance.Domain.Entities;
using HLS.Paygate.Balance.Domain.Repositories;
using HLS.Paygate.Balance.Models.Dtos;
using HLS.Paygate.Balance.Models.Enums;
using HLS.Paygate.Balance.Models.Requests;
using HLS.Paygate.Shared;
using HLS.Paygate.Shared.UniqueIdGenerator;
using Microsoft.Extensions.Logging;
using Paygate.Discovery.Requests.Balance;
using ServiceStack;

namespace HLS.Paygate.Balance.Domain.Services;

public class TransactionService : ITransactionService
{
    private readonly IBalanceMongoRepository _balanceMongoRepository;
    private readonly ILogger<TransactionService> _logger;

    //private readonly Logger _logger = LogManager.GetLogger("TransactionService");
    private readonly ITransCodeGenerator _transCodeGenerator;

    public TransactionService(IBalanceMongoRepository balanceMongoRepository,
        ITransCodeGenerator transCodeGenerator, ILogger<TransactionService> logger)
    {
        _balanceMongoRepository = balanceMongoRepository;
        _transCodeGenerator = transCodeGenerator;
        _logger = logger;
    }

    public async Task<MessageResponseBase> DepositAsync(DepositRequest depositRequest)
    {
        var createdDate = DateTime.Now;
        var depositResponse = new MessageResponseBase();
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            Amount = depositRequest.Amount,
            AddedAtUtc = createdDate,
            CreatedDate = createdDate,
            TransRef = depositRequest.TransRef,
            CurrencyCode = depositRequest.CurrencyCode,
            DesAccountCode = depositRequest.AccountCode,
            SrcAccountCode = BalanceConst.MASTER_ACCOUNT,
            Description = depositRequest.Description,
            Status = TransStatus.Done,
            TransType = TransactionType.Deposit,
            TransNote = depositRequest.TransNote,
            TransactionCode = await _transCodeGenerator.TransCodeGeneratorAsync("D")
        };

        try
        {
            await _balanceMongoRepository.AddOneAsync(transaction);
        }
        catch (Exception e)
        {
            _logger.LogError($"{depositRequest.TransRef}-Create transaction error: " + e.Message);
            depositResponse.ResponseCode = "6009";
            depositResponse.ResponseMessage = "Transaction fail!";
            return depositResponse;
        }

        var settlement = new SettlementDto
        {
            AddedAtUtc = DateTime.UtcNow,
            CreatedDate = createdDate,
            Amount = transaction.Amount,
            TransRef = transaction.TransactionCode,
            Status = SettlementStatus.Init,
            SrcAccountCode = transaction.SrcAccountCode,
            SrcShardAccountCode = transaction.SrcAccountCode,
            DesAccountCode = transaction.DesAccountCode,
            DesShardAccountCode = transaction.DesAccountCode,
            CurrencyCode = transaction.CurrencyCode,
            PaymentTransCode = transaction.TransRef,
            TransCode = Guid.NewGuid().ToString(),
            TransType = transaction.TransType
        };

        var transactionDto = transaction.ConvertTo<TransactionDto>();

        transactionDto.Settlements = new List<SettlementDto> { settlement };

        depositResponse.ResponseCode = "01";
        depositResponse.ResponseMessage = "Success";
        depositResponse.Payload = transactionDto;

        return depositResponse;
    }

    public async Task<MessageResponseBase> TransferAsync(TransferRequest transferRequest)
    {
        var transferResponse = new MessageResponseBase();
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            Amount = transferRequest.Amount,
            TransRef = transferRequest.TransRef,
            CurrencyCode = transferRequest.CurrencyCode,
            DesAccountCode = transferRequest.DesAccount,
            SrcAccountCode = transferRequest.SrcAccount,
            Description = transferRequest.Description,
            Status = TransStatus.Done,
            TransType = TransactionType.Transfer,
            CreatedDate = DateTime.Now,
            TransNote = transferRequest.TransNote,
            TransactionCode = await _transCodeGenerator.TransCodeGeneratorAsync()
        };

        try
        {
            await _balanceMongoRepository.AddOneAsync(transaction);
        }
        catch (Exception e)
        {
            _logger.LogError($"{transferRequest.TransRef}-Create transaction error: " + e.Message);
            transferResponse.ResponseCode = "6009";
            transferResponse.ResponseMessage = "Transaction fail!";
            return transferResponse;
        }

        var settlement = new SettlementDto
        {
            AddedAtUtc = DateTime.UtcNow,
            CreatedDate = DateTime.Now,
            Amount = transaction.Amount,
            TransRef = transaction.TransactionCode,
            Status = SettlementStatus.Init,
            SrcAccountCode = transaction.SrcAccountCode,
            SrcShardAccountCode = transaction.SrcAccountCode,
            DesAccountCode = transaction.DesAccountCode,
            DesShardAccountCode = transaction.DesAccountCode,
            CurrencyCode = transaction.CurrencyCode,
            PaymentTransCode = transaction.TransRef,
            TransType = transaction.TransType,
            TransCode = Guid.NewGuid().ToString(),
            ModifiedDate = DateTime.Now,
        };

        var transactionDto = transaction.ConvertTo<TransactionDto>();

        transactionDto.Settlements = new List<SettlementDto> { settlement };

        transferResponse.ResponseCode = "01";
        transferResponse.ResponseMessage = "Success";
        transferResponse.Payload = transactionDto;

        return transferResponse;
    }

    public async Task<MessageResponseBase> CashOutAsync(CashOutRequest cashOutRequest)
    {
        var createdDate = DateTime.Now;
        var cashOutResponse = new MessageResponseBase();
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            CreatedDate = createdDate,
            Amount = cashOutRequest.Amount,
            AddedAtUtc = createdDate,
            TransRef = cashOutRequest.TransRef,
            CurrencyCode = cashOutRequest.CurrencyCode,
            SrcAccountCode = cashOutRequest.AccountCode,
            DesAccountCode = BalanceConst.CASHOUT_ACCOUNT,
            Description = cashOutRequest.Description,
            Status = TransStatus.Done,
            TransType = TransactionType.Cashout,
            TransNote = cashOutRequest.TransNote,
            TransactionCode = await _transCodeGenerator.TransCodeGeneratorAsync("C")
        };

        try
        {
            await _balanceMongoRepository.AddOneAsync(transaction);
        }
        catch (Exception e)
        {
            _logger.LogError($"{cashOutRequest.TransRef}-Create transaction error: " + e.Message);
            cashOutResponse.ResponseCode = "6009";
            cashOutResponse.ResponseMessage = "Transaction fail!";
            return cashOutResponse;
        }

        var settlement = new SettlementDto
        {
            AddedAtUtc = DateTime.UtcNow,
            CreatedDate = createdDate,
            Amount = transaction.Amount,
            TransRef = transaction.TransactionCode,
            Status = SettlementStatus.Init,
            SrcAccountCode = transaction.SrcAccountCode,
            SrcShardAccountCode = transaction.SrcAccountCode,
            DesAccountCode = transaction.DesAccountCode,
            DesShardAccountCode = transaction.DesAccountCode,
            CurrencyCode = transaction.CurrencyCode,
            PaymentTransCode = transaction.TransRef,
            TransType = transaction.TransType,
            TransCode = Guid.NewGuid().ToString(),
            ModifiedDate = DateTime.Now
        };

        var transactionDto = transaction.ConvertTo<TransactionDto>();

        transactionDto.Settlements = new List<SettlementDto> { settlement };

        cashOutResponse.ResponseCode = "01";
        cashOutResponse.ResponseMessage = "Success";
        cashOutResponse.Payload = transactionDto;

        return cashOutResponse;
    }

    public async Task<MessageResponseBase> PaymentAsync(BalancePaymentRequest paymentRequest)
    {
        var createdDate = DateTime.Now;
        var paymentResponse = new MessageResponseBase();
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            CreatedDate = createdDate,
            Amount = paymentRequest.PaymentAmount,
            AddedAtUtc = createdDate,
            TransRef = paymentRequest.TransRef,
            CurrencyCode = paymentRequest.CurrencyCode,
            SrcAccountCode = paymentRequest.AccountCode,
            DesAccountCode = !string.IsNullOrEmpty(paymentRequest.MerchantCode)
                ? paymentRequest.MerchantCode
                : BalanceConst.PAYMENT_ACCOUNT,
            Description = paymentRequest.Description,
            Status = TransStatus.Done,
            TransType = TransactionType.Payment,
            TransNote = paymentRequest.TransNote,
            TransactionCode = await _transCodeGenerator.TransCodeGeneratorAsync("P")
        };

        try
        {
            await _balanceMongoRepository.AddOneAsync(transaction);
            _logger.LogInformation($"{paymentRequest.TransRef}-Create transaction success");
        }
        catch (Exception e)
        {
            _logger.LogError($"{paymentRequest.TransRef}-Create transaction error: " + e.Message);
            paymentResponse.ResponseCode = "6009";
            paymentResponse.ResponseMessage = "Transaction fail!";
            return paymentResponse;
        }

        var settlement = new SettlementDto
        {
            AddedAtUtc = DateTime.UtcNow,
            CreatedDate = createdDate,
            Amount = transaction.Amount,
            TransRef = transaction.TransactionCode,
            Status = SettlementStatus.Init,
            SrcAccountCode = transaction.SrcAccountCode,
            SrcShardAccountCode = transaction.SrcAccountCode,
            DesAccountCode = transaction.DesAccountCode,
            DesShardAccountCode = transaction.DesAccountCode,
            CurrencyCode = transaction.CurrencyCode,
            PaymentTransCode = transaction.TransRef,
            TransCode = Guid.NewGuid().ToString(),
            TransType = transaction.TransType,
            ModifiedDate = DateTime.Now
        };

        var transactionDto = transaction.ConvertTo<TransactionDto>();

        transactionDto.Settlements = new List<SettlementDto> { settlement };

        paymentResponse.ResponseCode = "01";
        paymentResponse.ResponseMessage = "Success";
        paymentResponse.TransCode = transactionDto.TransactionCode;
        paymentResponse.Payload = transactionDto;

        return paymentResponse;
    }

    public async Task<MessageResponseBase> PriorityPaymentAsync(PriorityPaymentRequest request)
    {
        var createdDate = DateTime.Now;
        var paymentResponse = new MessageResponseBase();
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            CreatedDate = createdDate,
            Amount = request.Amount,
            AddedAtUtc = createdDate,
            TransRef = request.TransRef,
            CurrencyCode = request.CurrencyCode,
            SrcAccountCode = request.AccountCode,
            DesAccountCode = BalanceConst.PAYMENT_ACCOUNT,
            Description = request.Description,
            Status = TransStatus.Done,
            TransType = TransactionType.FeePriority,
            TransNote = request.TransNote,
            TransactionCode = await _transCodeGenerator.TransCodeGeneratorAsync("P")
        };

        try
        {
            await _balanceMongoRepository.AddOneAsync(transaction);
        }
        catch (Exception e)
        {
            _logger.LogError($"{request.TransRef}-Create transaction error: " + e.Message);
            paymentResponse.ResponseCode = "6009";
            paymentResponse.ResponseMessage = "Transaction fail!";
            return paymentResponse;
        }

        var settlement = new SettlementDto
        {
            AddedAtUtc = DateTime.UtcNow,
            CreatedDate = createdDate,
            Amount = transaction.Amount,
            TransRef = transaction.TransactionCode,
            Status = SettlementStatus.Init,
            SrcAccountCode = transaction.SrcAccountCode,
            SrcShardAccountCode = transaction.SrcAccountCode,
            DesAccountCode = transaction.DesAccountCode,
            DesShardAccountCode = transaction.DesAccountCode,
            CurrencyCode = transaction.CurrencyCode,
            PaymentTransCode = transaction.TransRef,
            TransCode = Guid.NewGuid().ToString(),
            TransType = transaction.TransType,
            ModifiedDate = DateTime.Now
        };

        var transactionDto = transaction.ConvertTo<TransactionDto>();

        transactionDto.Settlements = new List<SettlementDto> { settlement };

        paymentResponse.ResponseCode = "01";
        paymentResponse.ResponseMessage = "Success";
        paymentResponse.Payload = transactionDto;

        return paymentResponse;
    }

    public async Task<MessageResponseBase> CancelPaymentAsync(BalanceCancelPaymentRequest request)
    {
        if (await CheckCancelPayment(request.TransRef))
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Không thể hoàn tiền cho giao đã được hoàn trước đó"
            };
        var createdDate = DateTime.Now;
        var paymentResponse = new MessageResponseBase();
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            CreatedDate = createdDate,
            Amount = request.RevertAmount,
            AddedAtUtc = createdDate,
            TransRef = request.TransRef,
            CurrencyCode = request.CurrencyCode,
            SrcAccountCode = BalanceConst.PAYMENT_ACCOUNT,
            DesAccountCode = request.AccountCode,
            Description = request.Description,
            Status = TransStatus.Done,
            TransType = TransactionType.CancelPayment,
            TransNote = request.TransNote,
            TransactionCode = await _transCodeGenerator.TransCodeGeneratorAsync("P")
        };

        try
        {
            await _balanceMongoRepository.AddOneAsync(transaction);
        }
        catch (Exception e)
        {
            _logger.LogError($"{request.TransRef}-Create transaction error: " + e.Message);
            paymentResponse.ResponseCode = "6009";
            paymentResponse.ResponseMessage = "Transaction fail!";
            return paymentResponse;
        }

        var settlement = new SettlementDto
        {
            AddedAtUtc = DateTime.UtcNow,
            CreatedDate = createdDate,
            Amount = transaction.Amount,
            TransRef = transaction.TransactionCode,
            Status = SettlementStatus.Init,
            SrcAccountCode = transaction.SrcAccountCode,
            SrcShardAccountCode = transaction.SrcAccountCode,
            DesAccountCode = transaction.DesAccountCode,
            DesShardAccountCode = transaction.DesAccountCode,
            CurrencyCode = transaction.CurrencyCode,
            PaymentTransCode = transaction.TransRef,
            TransCode = Guid.NewGuid().ToString(),
            TransType = transaction.TransType,
            ModifiedDate = DateTime.Now
        };

        var transactionDto = transaction.ConvertTo<TransactionDto>();

        transactionDto.Settlements = new List<SettlementDto> { settlement };

        paymentResponse.ResponseCode = "01";
        paymentResponse.ResponseMessage = "Success";
        paymentResponse.Payload = transactionDto;

        return paymentResponse;
    }

    public async Task<MessageResponseBase> MasterTopupAsync(MasterTopupRequest masterTopupRequest)
    {
        var createdDate = DateTime.Now;
        var paymentResponse = new MessageResponseBase();
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            CreatedDate = createdDate,
            Amount = masterTopupRequest.Amount,
            AddedAtUtc = createdDate,
            TransRef = masterTopupRequest.TransRef,
            CurrencyCode = masterTopupRequest.CurrencyCode,
            DesAccountCode = BalanceConst.MASTER_ACCOUNT,
            Status = TransStatus.Done,
            TransType = TransactionType.MasterTopup,
            TransNote = masterTopupRequest.TransNote,
            TransactionCode = await _transCodeGenerator.TransCodeGeneratorAsync("M")
        };

        try
        {
            await _balanceMongoRepository.AddOneAsync(transaction);
        }
        catch (Exception e)
        {
            _logger.LogError($"{masterTopupRequest.TransRef}-Create transaction error: " + e.Message);
            paymentResponse.ResponseCode = "6009";
            paymentResponse.ResponseMessage = "Transaction fail!";
            return paymentResponse;
        }

        var settlement1 = new SettlementDto
        {
            AddedAtUtc = DateTime.UtcNow,
            CreatedDate = createdDate,
            Amount = transaction.Amount,
            TransRef = transaction.TransactionCode,
            Status = SettlementStatus.Init,
            DesAccountCode = BalanceConst.CONTROL_ACCOUNT,
            DesShardAccountCode = BalanceConst.CONTROL_ACCOUNT,
            CurrencyCode = transaction.CurrencyCode,
            TransCode = Guid.NewGuid().ToString(),
            TransType = transaction.TransType,
            ModifiedDate = DateTime.Now
        };

        var settlement2 = new SettlementDto
        {
            AddedAtUtc = DateTime.UtcNow,
            CreatedDate = createdDate,
            Amount = transaction.Amount,
            TransRef = transaction.TransactionCode,
            Status = SettlementStatus.Init,
            DesAccountCode = transaction.DesAccountCode,
            DesShardAccountCode = transaction.DesAccountCode,
            CurrencyCode = transaction.CurrencyCode,
            TransType = transaction.TransType,
            TransCode = Guid.NewGuid().ToString(),
            ModifiedDate = DateTime.Now
        };

        var transactionDto = transaction.ConvertTo<TransactionDto>();

        transactionDto.Settlements = new List<SettlementDto> { settlement1, settlement2 };

        paymentResponse.ResponseCode = "01";
        paymentResponse.ResponseMessage = "Success";
        paymentResponse.Payload = transactionDto;

        return paymentResponse;
    }

    public async Task<MessageResponseBase> CollectDiscountAsync(CollectDiscountRequest request)
    {
        var createdDate = DateTime.Now;
        var response = new MessageResponseBase();
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            Amount = request.Amount,
            AddedAtUtc = createdDate,
            CreatedDate = createdDate,
            TransRef = request.TransRef,
            CurrencyCode = CurrencyCode.VND.ToString("G"),
            SrcAccountCode = BalanceConst.MASTER_ACCOUNT,
            DesAccountCode = request.AccountCode,
            Description = request.Reason,
            Status = TransStatus.Done,
            TransType = TransactionType.CollectDiscount,
            TransNote = request.TransNote,
            TransactionCode = await _transCodeGenerator.TransCodeGeneratorAsync("D")
        };

        try
        {
            await _balanceMongoRepository.AddOneAsync(transaction);
        }
        catch (Exception e)
        {
            _logger.LogError($"{request.TransRef}-Create transaction error: " + e.Message);
            response.ResponseCode = "6009";
            response.ResponseMessage = "Transaction fail!";
            return response;
        }

        var settlement = new SettlementDto
        {
            AddedAtUtc = DateTime.UtcNow,
            CreatedDate = createdDate,
            Amount = transaction.Amount,
            TransRef = transaction.TransactionCode,
            Status = SettlementStatus.Init,
            SrcAccountCode = transaction.SrcAccountCode,
            SrcShardAccountCode = transaction.SrcAccountCode,
            DesAccountCode = transaction.DesAccountCode,
            DesShardAccountCode = transaction.DesAccountCode,
            CurrencyCode = transaction.CurrencyCode,
            PaymentTransCode = transaction.TransRef,
            TransType = transaction.TransType,
            TransCode = Guid.NewGuid().ToString()
        };

        var transactionDto = transaction.ConvertTo<TransactionDto>();

        transactionDto.Settlements = new List<SettlementDto> { settlement };

        response.ResponseCode = "01";
        response.ResponseMessage = "Success";
        response.Payload = transactionDto;

        return response;
    }

    public async Task<MessageResponseBase> RevertAsync(Transaction transactionRevert)
    {
        var createdDate = DateTime.Now;
        var paymentResponse = new MessageResponseBase();
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            CreatedDate = createdDate,
            Amount = transactionRevert.Amount,
            AddedAtUtc = createdDate,
            TransRef = transactionRevert.TransRef,
            CurrencyCode = transactionRevert.CurrencyCode,
            DesAccountCode = transactionRevert.SrcAccountCode,
            SrcAccountCode = transactionRevert.DesAccountCode,
            Description = transactionRevert.Description,
            Status = TransStatus.Done,
            TransType = TransactionType.Revert,
            TransNote = transactionRevert.Description,
            TransactionCode = await _transCodeGenerator.TransCodeGeneratorAsync("P")
        };

        try
        {
            await _balanceMongoRepository.AddOneAsync(transaction);
        }
        catch (Exception e)
        {
            _logger.LogError($"{transactionRevert.TransRef}-Create transaction error: " + e.Message);
            paymentResponse.ResponseCode = "6009";
            paymentResponse.ResponseMessage = "Transaction fail!";
            return paymentResponse;
        }

        var settlement = new SettlementDto
        {
            AddedAtUtc = DateTime.UtcNow,
            CreatedDate = createdDate,
            Amount = transaction.Amount,
            TransRef = transaction.TransactionCode,
            Status = SettlementStatus.Init,
            SrcAccountCode = transaction.SrcAccountCode,
            SrcShardAccountCode = transaction.SrcAccountCode,
            DesAccountCode = transaction.DesAccountCode,
            DesShardAccountCode = transaction.DesAccountCode,
            CurrencyCode = transaction.CurrencyCode,
            PaymentTransCode = transaction.TransRef,
            TransCode = Guid.NewGuid().ToString(),
            TransType = transaction.TransType,
            ModifiedDate = DateTime.Now
        };

        var transactionDto = transaction.ConvertTo<TransactionDto>();

        transactionDto.Settlements = new List<SettlementDto> { settlement };

        paymentResponse.ResponseCode = "01";
        paymentResponse.ResponseMessage = "Success";
        paymentResponse.Payload = transactionDto;

        return paymentResponse;
    }

    public async Task SettlementsInsertAsync(List<SettlementDto> settlementDtos)
    {
        try
        {
            await _balanceMongoRepository.AddManyAsync(settlementDtos.ConvertTo<List<Settlement>>());
        }
        catch (Exception ex)
        {
            _logger.LogError("Error insert settlement: " + ex.Message);
        }
    }

    public async Task<Transaction> TransactionGetByCode(string transactionCode)
    {
        return await _balanceMongoRepository.GetOneAsync<Transaction>(x => x.TransactionCode == transactionCode);
    }

    public async Task<MessageResponseBase> ChargingAsync(ChargingRequest chargingRequest)
    {
        var createdDate = DateTime.Now;
        var response = new MessageResponseBase();
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            Amount = chargingRequest.Amount,
            AddedAtUtc = createdDate,
            CreatedDate = createdDate,
            TransRef = chargingRequest.TransRef,
            CurrencyCode = chargingRequest.CurrencyCode,
            DesAccountCode = chargingRequest.AccountCode,
            SrcAccountCode = BalanceConst.MASTER_ACCOUNT,
            Description = chargingRequest.TransNote,
            Status = TransStatus.Done,
            TransType = TransactionType.CardCharges,
            TransNote = chargingRequest.TransNote,
            TransactionCode = await _transCodeGenerator.TransCodeGeneratorAsync("D")
        };

        try
        {
            await _balanceMongoRepository.AddOneAsync(transaction);
        }
        catch (Exception e)
        {
            _logger.LogError($"{chargingRequest.TransRef}-Create transaction error: " + e.Message);
            response.ResponseCode = "6009";
            response.ResponseMessage = "Transaction fail!";
            return response;
        }

        var settlement = new SettlementDto
        {
            AddedAtUtc = DateTime.UtcNow,
            CreatedDate = createdDate,
            Amount = transaction.Amount,
            TransRef = transaction.TransactionCode,
            Status = SettlementStatus.Init,
            SrcAccountCode = transaction.SrcAccountCode,
            SrcShardAccountCode = transaction.SrcAccountCode,
            DesAccountCode = transaction.DesAccountCode,
            DesShardAccountCode = transaction.DesAccountCode,
            CurrencyCode = transaction.CurrencyCode,
            PaymentTransCode = transaction.TransRef,
            TransType = transaction.TransType,
            TransCode = Guid.NewGuid().ToString()
        };

        var transactionDto = transaction.ConvertTo<TransactionDto>();

        transactionDto.Settlements = new List<SettlementDto> { settlement };

        response.ResponseCode = "01";
        response.ResponseMessage = "Success";
        response.Payload = transactionDto;

        return response;
    }

    public async Task<MessageResponseBase> AdjustmentAsync(AdjustmentRequest request)
    {
        var response = new MessageResponseBase();
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            Amount = request.Amount,
            TransRef = request.TransRef,
            CurrencyCode = request.CurrencyCode,
            DesAccountCode = request.AdjustmentType == AdjustmentType.Decrease
                ? BalanceConst.MASTER_ACCOUNT
                : request.AccountCode,
            SrcAccountCode = request.AdjustmentType == AdjustmentType.Decrease
                ? request.AccountCode
                : BalanceConst.MASTER_ACCOUNT,
            Description = request.TransNote,
            Status = TransStatus.Done,
            TransType = request.AdjustmentType == AdjustmentType.Decrease
                ? TransactionType.AdjustmentDecrease
                : TransactionType.AdjustmentIncrease,
            CreatedDate = DateTime.Now,
            TransNote = request.TransNote,
            TransactionCode = await _transCodeGenerator.TransCodeGeneratorAsync()
        };

        try
        {
            await _balanceMongoRepository.AddOneAsync(transaction);
        }
        catch (Exception e)
        {
            _logger.LogError($"{request.TransRef}-Create transaction error: " + e.Message);
            response.ResponseCode = "6009";
            response.ResponseMessage = "Transaction fail!";
            return response;
        }

        var settlement = new SettlementDto
        {
            AddedAtUtc = DateTime.UtcNow,
            CreatedDate = DateTime.Now,
            Amount = transaction.Amount,
            TransRef = transaction.TransactionCode,
            Status = SettlementStatus.Init,
            SrcAccountCode = transaction.SrcAccountCode,
            SrcShardAccountCode = transaction.SrcAccountCode,
            DesAccountCode = transaction.DesAccountCode,
            DesShardAccountCode = transaction.DesAccountCode,
            CurrencyCode = transaction.CurrencyCode,
            PaymentTransCode = transaction.TransRef,
            TransCode = Guid.NewGuid().ToString(),
            TransType = transaction.TransType,
            ModifiedDate = DateTime.Now,
        };

        var transactionDto = transaction.ConvertTo<TransactionDto>();

        transactionDto.Settlements = new List<SettlementDto> { settlement };

        response.ResponseCode = "01";
        response.ResponseMessage = "Success";
        response.Payload = transactionDto;

        return response;
    }

    public async Task<MessageResponseBase> ClearDebtAsync(ClearDebtRequest request)
    {
        var response = new MessageResponseBase();
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            Amount = request.Amount,
            TransRef = request.TransRef,
            CurrencyCode = CurrencyCode.DEBT.ToString("G"),
            SrcAccountCode = request.AccountCode,
            Description = request.TransNote,
            Status = TransStatus.Done,
            TransType = TransactionType.ClearDebt,
            CreatedDate = DateTime.Now,
            TransNote = request.TransNote,
            TransactionCode = await _transCodeGenerator.TransCodeGeneratorAsync()
        };

        try
        {
            await _balanceMongoRepository.AddOneAsync(transaction);
        }
        catch (Exception e)
        {
            _logger.LogError($"{request.TransRef}-Create transaction error: " + e.Message);
            response.ResponseCode = "6009";
            response.ResponseMessage = "Transaction fail!";
            return response;
        }

        var settlement = new SettlementDto
        {
            AddedAtUtc = DateTime.UtcNow,
            CreatedDate = DateTime.Now,
            Amount = transaction.Amount,
            TransRef = transaction.TransactionCode,
            Status = SettlementStatus.Init,
            SrcAccountCode = transaction.SrcAccountCode,
            SrcShardAccountCode = transaction.SrcAccountCode,
            CurrencyCode = transaction.CurrencyCode,
            PaymentTransCode = transaction.TransRef,
            TransCode = Guid.NewGuid().ToString(),
            TransType = transaction.TransType,
            ModifiedDate = DateTime.Now,
        };

        var transactionDto = transaction.ConvertTo<TransactionDto>();

        transactionDto.Settlements = new List<SettlementDto> { settlement };

        response.ResponseCode = "01";
        response.ResponseMessage = "Success";
        response.Payload = transactionDto;

        return response;
    }

    public async Task<MessageResponseBase> SaleDepositAsync(SaleDepositRequest request)
    {
        var createdDate = DateTime.Now;
        var response = new MessageResponseBase();
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            Amount = request.Amount,
            TransRef = request.TransRef,
            CurrencyCode = CurrencyCode.VND.ToString("G"),
            DesAccountCode = request.AccountCode,
            SrcAccountCode = BalanceConst.MASTER_ACCOUNT,
            Description = request.TransNote,
            Status = TransStatus.Done,
            TransType = TransactionType.SaleDeposit,
            CreatedDate = createdDate,
            TransNote = request.TransNote,
            TransactionCode = await _transCodeGenerator.TransCodeGeneratorAsync()
        };

        try
        {
            await _balanceMongoRepository.AddOneAsync(transaction);
        }
        catch (Exception e)
        {
            _logger.LogError($"{request.TransRef}-Create transaction error: " + e.Message);
            response.ResponseCode = "6009";
            response.ResponseMessage = "Transaction fail!";
            return response;
        }

        var settlement1 = new SettlementDto
        {
            AddedAtUtc = DateTime.UtcNow,
            CreatedDate = createdDate,
            Amount = transaction.Amount,
            TransRef = transaction.TransactionCode,
            Status = SettlementStatus.Init,
            SrcAccountCode = transaction.SrcAccountCode,
            SrcShardAccountCode = transaction.SrcAccountCode,
            DesAccountCode = transaction.DesAccountCode,
            DesShardAccountCode = transaction.DesAccountCode,
            CurrencyCode = transaction.CurrencyCode,
            TransType = transaction.TransType,
            TransCode = Guid.NewGuid().ToString(),
            ModifiedDate = DateTime.Now,
            Description = request.SaleCode
        };

        var settlement2 = new SettlementDto
        {
            AddedAtUtc = DateTime.UtcNow,
            CreatedDate = createdDate,
            Amount = transaction.Amount,
            TransRef = transaction.TransactionCode,
            Status = SettlementStatus.Init,
            DesAccountCode = request.SaleCode,
            DesShardAccountCode = request.SaleCode,
            CurrencyCode = CurrencyCode.DEBT.ToString("G"),
            TransType = transaction.TransType,
            TransCode = Guid.NewGuid().ToString(),
            ModifiedDate = DateTime.Now,
            Description = transaction.DesAccountCode
        };

        var transactionDto = transaction.ConvertTo<TransactionDto>();

        transactionDto.Settlements = new List<SettlementDto> { settlement1, settlement2 };

        response.ResponseCode = "01";
        response.ResponseMessage = "Success";
        response.Payload = transactionDto;

        return response;
    }

    public async Task<MessageResponseBase> PayBatchAsync(PaybatchAccount request)
    {
        var createdDate = DateTime.Now;
        var depositResponse = new MessageResponseBase();
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            AddedAtUtc = createdDate,
            CreatedDate = createdDate,
            Amount = request.Amount,
            TransRef = request.TransRef,
            CurrencyCode = CurrencyCode.VND.ToString("G"),
            DesAccountCode = request.AccountCode,
            SrcAccountCode = BalanceConst.COMMISSION_ACCOUNT,
            Status = TransStatus.Done,
            TransType = TransactionType.PayBatch,
            TransNote = request.TransNote,
            TransactionCode = await _transCodeGenerator.TransCodeGeneratorAsync("D")
        };

        try
        {
            await _balanceMongoRepository.AddOneAsync(transaction);
        }
        catch (Exception e)
        {
            _logger.LogError($"{request.TransRef}-Create transaction error: " + e.Message);
            depositResponse.ResponseCode = "6009";
            depositResponse.ResponseMessage = "Transaction fail!";
            return depositResponse;
        }

        var settlement = new SettlementDto
        {
            AddedAtUtc = DateTime.UtcNow,
            CreatedDate = createdDate,
            Amount = transaction.Amount,
            TransRef = transaction.TransactionCode,
            Status = SettlementStatus.Init,
            SrcAccountCode = transaction.SrcAccountCode,
            SrcShardAccountCode = transaction.SrcAccountCode,
            DesAccountCode = transaction.DesAccountCode,
            DesShardAccountCode = transaction.DesAccountCode,
            CurrencyCode = transaction.CurrencyCode,
            PaymentTransCode = transaction.TransRef,
            TransType = transaction.TransType,
            TransCode = Guid.NewGuid().ToString()
        };

        var transactionDto = transaction.ConvertTo<TransactionDto>();

        transactionDto.Settlements = new List<SettlementDto> { settlement };

        depositResponse.ResponseCode = "01";
        depositResponse.ResponseMessage = "Success";
        depositResponse.Payload = transactionDto;

        return depositResponse;
    }

    public async Task<MessageResponseBase> BlockBalanceAsync(BlockBalanceRequest request)
    {
        var createdDate = DateTime.Now;
        var response = new MessageResponseBase();
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            Amount = request.BlockAmount,
            AddedAtUtc = createdDate,
            CreatedDate = createdDate,
            TransRef = request.TransRef,
            CurrencyCode = request.CurrencyCode,
            SrcAccountCode = request.AccountCode,
            Description = request.TransNote,
            Status = TransStatus.Done,
            TransType = TransactionType.Block,
            TransNote = request.TransNote,
            TransactionCode = await _transCodeGenerator.TransCodeGeneratorAsync("D")
        };

        try
        {
            await _balanceMongoRepository.AddOneAsync(transaction);
        }
        catch (Exception e)
        {
            _logger.LogError($"{request.TransRef}-Create transaction error: " + e.Message);
            response.ResponseCode = "6009";
            response.ResponseMessage = "Transaction fail!";
            return response;
        }

        var settlement = new SettlementDto
        {
            AddedAtUtc = DateTime.UtcNow,
            CreatedDate = createdDate,
            Amount = transaction.Amount,
            TransRef = transaction.TransactionCode,
            Status = SettlementStatus.Init,
            SrcAccountCode = transaction.SrcAccountCode,
            SrcShardAccountCode = transaction.SrcAccountCode,
            CurrencyCode = transaction.CurrencyCode,
            PaymentTransCode = transaction.TransRef,
            TransType = transaction.TransType,
            TransCode = Guid.NewGuid().ToString()
        };

        var transactionDto = transaction.ConvertTo<TransactionDto>();

        transactionDto.Settlements = new List<SettlementDto> { settlement };

        response.ResponseCode = "01";
        response.ResponseMessage = "Success";
        response.Payload = transactionDto;

        return response;
    }

    public async Task<MessageResponseBase> UnBlockBalanceAsync(UnBlockBalanceRequest request)
    {
        var createdDate = DateTime.Now;
        var response = new MessageResponseBase();
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            Amount = request.UnBlockAmount,
            AddedAtUtc = createdDate,
            CreatedDate = createdDate,
            TransRef = request.TransRef,
            CurrencyCode = request.CurrencyCode,
            DesAccountCode = request.AccountCode,
            Description = request.TransNote,
            Status = TransStatus.Done,
            TransType = TransactionType.Unblock,
            TransNote = request.TransNote,
            TransactionCode = await _transCodeGenerator.TransCodeGeneratorAsync("D")
        };

        try
        {
            await _balanceMongoRepository.AddOneAsync(transaction);
        }
        catch (Exception e)
        {
            _logger.LogError($"{request.TransRef}-Create transaction error: " + e.Message);
            response.ResponseCode = "6009";
            response.ResponseMessage = "Transaction fail!";
            return response;
        }

        var settlement = new SettlementDto
        {
            AddedAtUtc = DateTime.UtcNow,
            CreatedDate = createdDate,
            Amount = transaction.Amount,
            TransRef = transaction.TransactionCode,
            Status = SettlementStatus.Init,
            DesAccountCode = transaction.SrcAccountCode,
            DesShardAccountCode = transaction.SrcAccountCode,
            CurrencyCode = transaction.CurrencyCode,
            PaymentTransCode = transaction.TransRef,
            TransType = transaction.TransType,
            TransCode = Guid.NewGuid().ToString()
        };

        var transactionDto = transaction.ConvertTo<TransactionDto>();

        transactionDto.Settlements = new List<SettlementDto> { settlement };

        response.ResponseCode = "01";
        response.ResponseMessage = "Success";
        response.Payload = transactionDto;

        return response;
    }

    public async Task<MessageResponseBase> TransferSystemAsync(TransferSystemRequest transferRequest)
    {
        var transferResponse = new MessageResponseBase();
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            Amount = transferRequest.Amount,
            TransRef = transferRequest.TransRef,
            CurrencyCode = transferRequest.CurrencyCode,
            DesAccountCode = transferRequest.DesAccount,
            SrcAccountCode = transferRequest.SrcAccount,
            Status = TransStatus.Done,
            TransType = TransactionType.SystemTransfer,
            CreatedDate = DateTime.Now,
            TransNote = transferRequest.TransNote,
            TransactionCode = await _transCodeGenerator.TransCodeGeneratorAsync()
        };

        try
        {
            await _balanceMongoRepository.AddOneAsync(transaction);
        }
        catch (Exception e)
        {
            _logger.LogError($"{transferRequest.TransRef}-Create transaction error: " + e.Message);
            transferResponse.ResponseCode = "6009";
            transferResponse.ResponseMessage = "Transaction fail!";
            return transferResponse;
        }

        var settlement = new SettlementDto
        {
            AddedAtUtc = DateTime.UtcNow,
            CreatedDate = DateTime.Now,
            Amount = transaction.Amount,
            TransRef = transaction.TransactionCode,
            Status = SettlementStatus.Init,
            SrcAccountCode = transaction.SrcAccountCode,
            SrcShardAccountCode = transaction.SrcAccountCode,
            DesAccountCode = transaction.DesAccountCode,
            DesShardAccountCode = transaction.DesAccountCode,
            CurrencyCode = transaction.CurrencyCode,
            PaymentTransCode = transaction.TransRef,
            TransCode = Guid.NewGuid().ToString(),
            TransType = transaction.TransType,
            ModifiedDate = DateTime.Now,
        };

        var transactionDto = transaction.ConvertTo<TransactionDto>();

        transactionDto.Settlements = new List<SettlementDto> { settlement };

        transferResponse.ResponseCode = "01";
        transferResponse.ResponseMessage = "Success";
        transferResponse.Payload = transactionDto;

        return transferResponse;
    }

    public async Task<bool> CheckCancelPayment(string transcode)
    {
        var item = await _balanceMongoRepository.GetOneAsync<Transaction>(x =>
            x.TransRef == transcode && x.TransType == TransactionType.CancelPayment);
        return item != null;
    }

    public async Task<MessageResponseBase> PayCommissionAsync(BalancePayCommissionRequest request)
    {
        var createdDate = DateTime.Now;
        var response = new MessageResponseBase();
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            Amount = request.Amount,
            AddedAtUtc = createdDate,
            CreatedDate = createdDate,
            TransRef = request.TransRef,
            CurrencyCode = request.CurrencyCode,
            DesAccountCode = request.AccountCode,
            SrcAccountCode = BalanceConst.COMMISSION_ACCOUNT,
            Status = TransStatus.Done,
            TransType = TransactionType.PayCommission,
            TransNote = request.TransNote,
            TransactionCode = await _transCodeGenerator.TransCodeGeneratorAsync("D")
        };

        try
        {
            await _balanceMongoRepository.AddOneAsync(transaction);
        }
        catch (Exception e)
        {
            _logger.LogError($"{request.TransRef}-Create transaction error: " + e.Message);
            response.ResponseCode = "6009";
            response.ResponseMessage = "Transaction fail!";
            return response;
        }

        var settlement = new SettlementDto
        {
            AddedAtUtc = DateTime.UtcNow,
            CreatedDate = createdDate,
            Amount = transaction.Amount,
            TransRef = transaction.TransactionCode,
            Status = SettlementStatus.Init,
            SrcAccountCode = transaction.SrcAccountCode,
            SrcShardAccountCode = transaction.SrcAccountCode,
            DesAccountCode = transaction.DesAccountCode,
            DesShardAccountCode = transaction.DesAccountCode,
            CurrencyCode = transaction.CurrencyCode,
            PaymentTransCode = transaction.TransRef,
            TransType = transaction.TransType,
            TransCode = Guid.NewGuid().ToString()
        };

        var transactionDto = transaction.ConvertTo<TransactionDto>();
        transactionDto.Settlements = new List<SettlementDto> { settlement };
        response.ResponseCode = "01";
        response.ResponseMessage = "Success";
        response.Payload = transactionDto;
        return response;
    }

    public async Task<bool> UpdateTransactionStatus(TransactionDto transaction)
    {
        try
        {
            return await _balanceMongoRepository.UpdateOneAsync(new Transaction().PopulateWith(transaction));
        }
        catch (Exception e)
        {
            _logger.LogError($"{transaction.TransRef}-Update transaction error: " + e.Message);
            return false;
        }
    }

   
}