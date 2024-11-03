using System.Collections.Generic;
using System.Threading.Tasks;
using HLS.Paygate.Balance.Models.Dtos;
using HLS.Paygate.Balance.Models.Requests;
using HLS.Paygate.Shared;
using Paygate.Discovery.Requests.Balance;

namespace HLS.Paygate.Balance.Domain.Services;

public interface IBalanceService
{
    Task<AccountBalanceDto> AccountBalanceGetAsync(string accountCode, string currencyCode);
    Task<AccountBalanceDto> AccountBalanceCreateAsync(AccountBalanceDto accountBalanceDto);
    Task<bool> AccountBalanceUpdateAsync(AccountBalanceDto accountBalanceDto);
    Task<MessageResponseBase> CheckCurrencyAsync(string currencyCode);
    Task CurrencyCreateAsync(string currencyCode);

    Task<MessageResponseBase> TransferAsync(TransferRequest transferRequest);
    Task<MessageResponseBase> DepositAsync(DepositRequest depositRequest);
    Task<MessageResponseBase> CashOutAsync(CashOutRequest cashOutRequest);
    Task<MessageResponseBase> CollectDiscountAsync(CollectDiscountRequest correctRequest);
    Task<MessageResponseBase> MasterTopupAysnc(MasterTopupRequest masterTopupRequest);
    Task<MessageResponseBase> PaymentAsync(BalancePaymentRequest paymentRequest);
    Task<MessageResponseBase> RevertAsync(RevertRequest paymentRequest);
    Task<MessageResponseBase> CancelPaymentAsync(BalanceCancelPaymentRequest paymentRequest);
    Task<MessageResponseBase> PriorityAsync(PriorityPaymentRequest paymentRequest);
    Task<decimal> AccountBalanceCheckAsync(AccountBalanceCheckRequest accountBalanceCheckRequest);
    Task<MessageResponseBase> ChargingAsync(ChargingRequest request);
    Task<List<string>> GetAccountCodeListAsync(BalanceAccountCodesRequest request);
    Task<MessageResponseBase> AdjustmentAsync(AdjustmentRequest request);
    Task<MessageResponseBase> ClearDebtAsync(ClearDebtRequest request);

    Task<MessageResponseBase> SaleDepositAsync(SaleDepositRequest request);
    Task<MessageResponseBase> BlockBalanceAsync(BlockBalanceRequest request);
    Task<MessageResponseBase> UnBlockBalanceAsync(UnBlockBalanceRequest request);
    Task<ResponseMessageApi<List<PaybatchAccount>>> PayBatchAsync(PaybatchRequest request);
    Task<AccountBalanceDto> AccountBalanceStateInfo(string accountCode, string currencyCode);
    Task<MessageResponseBase> TransferSystemAsync(TransferSystemRequest transferRequest);
    Task<MessageResponseBase> PayCommissionAsync(BalancePayCommissionRequest request);
    bool CheckAccountSystem(string accountCode);
    Task<ResponseMesssageObject<string>> GetSettlementSelectByAsync(BalanceHistoriesRequest request);

    Task<ResponseMesssageObject<string>> GetSettlementBalanceDayByAsync(BalanceDayRequest request);
}