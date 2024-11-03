using System.Threading.Tasks;
using HLS.Paygate.Shared;
using HLS.Paygate.Shared.Dtos;
using HLS.Paygate.TopupGw.Contacts.ApiRequests;
using HLS.Paygate.TopupGw.Contacts.Dtos;

namespace HLS.Paygate.TopupGw.Components.Connectors;

public interface IGatewayConnector
{
    Task<MessageResponseBase> TopupAsync(TopupRequestLogDto topupRequestLog, ProviderInfoDto providerInfo);

    Task<MessageResponseBase> TransactionCheckAsync(string providerCode, string transCodeToCheck,
        string transCode, string serviceCode = null, ProviderInfoDto providerInfo = null);

    Task<NewMessageReponseBase<InvoiceResultDto>> QueryAsync(PayBillRequestLogDto payBillRequestLog);
    Task<MessageResponseBase> CardGetByBatchAsync(CardRequestLogDto cardRequestLog);
    Task<MessageResponseBase> CheckBalanceAsync(string providerCode, string transCode);
    Task<MessageResponseBase> DepositAsync(DepositRequestDto request);
    Task<MessageResponseBase> PayBillAsync(PayBillRequestLogDto payBillRequestLog);
    Task<ResponseMessageApi<object>> GetProductInfo(GetProviderProductInfo info);

    Task<MessageResponseBase> CheckPrePostPaid(string msisdn, string transCode, string providerCode);
}
