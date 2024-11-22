using Topup.Shared;
using ServiceStack;
using Topup.Balance.Models.Dtos;
using Topup.Balance.Models.Enums;

namespace Topup.Balance.Models.Requests;

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
public class ChangeBalanceAccountRequest : IPut, IReturn<NewMessageResponseBase<object>>
{
    public string AccountCode { get; set; }
    public string CurrencyCode { get; set; }
    public string TransCode { get; set; }
    public decimal Balance { get; set; }
}