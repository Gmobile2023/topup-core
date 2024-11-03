using HLS.Paygate.Balance.Models.Dtos;
using HLS.Paygate.Balance.Models.Enums;
using HLS.Paygate.Shared;
using ServiceStack;

namespace HLS.Paygate.Balance.Models.Requests;

[Route("/api/v1/balance", "POST")]
public class AccountCreateRequest : IPost, IReturn<MessageResponseBase>
{
    public string AccountCode { get; set; }
    public string CurrencyCode { get; set; }
}

[Route("/api/v1/balance/status", "PUT")]
public class AccountUpdateStatusRequest : IPut, IReturn<MessageResponseBase>
{
    public string AccountCode { get; set; }
    public string CurrencyCode { get; set; }
    public BalanceStatus Status { get; set; }
}

[Route("/api/v1/balance/checkBalanceInfo", "GET")]
public class AccountBalanceInfoCheckRequest : IGet, IReturn<ResponseMessageApi<AccountBalanceInfo>>
{
    public string AccountCode { get; set; }
    public string CurrencyCode { get; set; }
}

[Route("/api/v1/balance/checkBalanceInfo", "GET")]
public class ChangeBalanceAccountRequest : IPut, IReturn<NewMessageReponseBase<object>>
{
    public string AccountCode { get; set; }
    public string CurrencyCode { get; set; }
    public string TransCode { get; set; }
    public decimal Balance { get; set; }
}