using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GMB.Topup.Balance.Domain.Entities;
using GMB.Topup.Balance.Models.Dtos;
using GMB.Topup.Balance.Models.Requests;
using GMB.Topup.Shared;
using GMB.Topup.Discovery.Requests.Balance;

namespace GMB.Topup.Balance.Domain.Services;

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