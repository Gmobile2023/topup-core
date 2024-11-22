using System.Threading.Tasks;
using Topup.Shared;
using Topup.Shared.Dtos;
using Topup.TopupGw.Contacts.ApiRequests;
using Topup.TopupGw.Contacts.Dtos;



namespace Topup.TopupGw.Components.Connectors;

public interface IGatewayConnector
{
    Task<MessageResponseBase> TopupAsync(TopupRequestLogDto topupRequestLog, ProviderInfoDto providerInfo);

    Task<MessageResponseBase> TransactionCheckAsync(string providerCode, string transCodeToCheck,
        string transCode, string serviceCode = null, ProviderInfoDto providerInfo = null);

    Task<NewMessageResponseBase<InvoiceResultDto>> QueryAsync(PayBillRequestLogDto payBillRequestLog);
    Task<MessageResponseBase> CardGetByBatchAsync(CardRequestLogDto cardRequestLog);
    Task<MessageResponseBase> CheckBalanceAsync(string providerCode, string transCode);
    Task<MessageResponseBase> DepositAsync(DepositRequestDto request);
    Task<MessageResponseBase> PayBillAsync(PayBillRequestLogDto payBillRequestLog);
    Task<ResponseMessageApi<object>> GetProductInfo(GetProviderProductInfo info);

    Task<MessageResponseBase> CheckPrePostPaid(string msisdn, string transCode, string providerCode);
}
