using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HLS.Paygate.Balance.Domain.Entities;
using HLS.Paygate.Balance.Models.Dtos;
using HLS.Paygate.Balance.Models.Requests;
using HLS.Paygate.Shared;
using Paygate.Discovery.Requests.Balance;

namespace HLS.Paygate.Balance.Domain.Services;

public interface ITransactionService
{
    Task<MessageResponseBase> DepositAsync(DepositRequest depositRequest);

    //Task<string> CodeGenGetAsync(string prefix, string key);
    Task<MessageResponseBase> TransferAsync(TransferRequest transferRequest);
    Task SettlementsInsertAsync(List<SettlementDto> settlementDtos);
    Task<MessageResponseBase> CashOutAsync(CashOutRequest cashOutRequest);
    Task<MessageResponseBase> PaymentAsync(BalancePaymentRequest paymentRequest);
    Task<MessageResponseBase> PriorityPaymentAsync(PriorityPaymentRequest request);
    Task<MessageResponseBase> CancelPaymentAsync(BalanceCancelPaymentRequest request);
    Task<MessageResponseBase> MasterTopupAsync(MasterTopupRequest masterTopupRequest);

    Task<MessageResponseBase> CollectDiscountAsync(CollectDiscountRequest request);

    Task<MessageResponseBase> RevertAsync(Transaction paymentRequest);

    Task<Transaction> TransactionGetByCode(string transactionCode);
    Task<MessageResponseBase> ChargingAsync(ChargingRequest chargingRequest);
    Task<MessageResponseBase> AdjustmentAsync(AdjustmentRequest request);
    Task<MessageResponseBase> ClearDebtAsync(ClearDebtRequest request);
    Task<MessageResponseBase> SaleDepositAsync(SaleDepositRequest request);
    Task<MessageResponseBase> PayBatchAsync(PaybatchAccount request);
    Task<MessageResponseBase> BlockBalanceAsync(BlockBalanceRequest request);
    Task<MessageResponseBase> UnBlockBalanceAsync(UnBlockBalanceRequest request);
    Task<MessageResponseBase> TransferSystemAsync(TransferSystemRequest transferRequest);
    Task<bool> CheckCancelPayment(string transcode);
    Task<MessageResponseBase> PayCommissionAsync(BalancePayCommissionRequest request);
    Task<bool> UpdateTransactionStatus(TransactionDto transaction);    
}