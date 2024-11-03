using System.Runtime.Serialization;
using GMB.Topup.Shared;
using GMB.Topup.Shared.Dtos;
using ServiceStack;

namespace GMB.Topup.Discovery.Requests.Reports;
[DataContract]
[Route("/api/v1/report/account/info")]
public class ReportGetAccountInfoRequest : IReturn<NewMessageResponseBase<AccountInfoDto>>
{
    [DataMember(Order = 1)] public string AccountCode { get; set; }
}

[DataContract]
[Route("/api/v1/balance/check", "GET")]
public class BalanceCheckRequest : IReturn<decimal>
{
    [DataMember(Order = 1)] public string AccountCode { get; set; }
    [DataMember(Order = 2)] public string CurrencyCode { get; set; }
}