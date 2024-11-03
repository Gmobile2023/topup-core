using System;
using HLS.Paygate.Shared;
using ServiceStack;
using System.Runtime.Serialization;

namespace Paygate.Discovery.Requests.Balance;

[Route("/api/v1/transaction/report/transactions", "GET")]
public class TransactionReportsRequest : PaggingBase, IReturn<MessagePagedResponseBase>
{
    public TransactionType? TransType { get; set; }
    public string TransCode { get; set; }
    public string TransRef { get; set; }
    public string SrcAccount { get; set; }
    public string DesAccount { get; set; }
    public string TransNote { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

[Route("/api/v1/transaction/report/transaction/{TransCode}", "GET")]
public class TransactionReportRequest : IReturn<MessageResponseBase>
{
    public string TransCode { get; set; }
}
[DataContract]
[Route("/api/v1/balance/report/balanceHistories", "GET")]
public class BalanceHistoriesRequest :IGet, IReturn<ResponseMesssageObject<string>>
{
    [DataMember(Order = 1)] public DateTime FromDate { get; set; }
    [DataMember(Order = 2)] public DateTime ToDate { get; set; }
    [DataMember(Order = 3)] public string AccountCode { get; set; }
    [DataMember(Order = 4)] public string CurrencyCode { get; set; }
    [DataMember(Order = 5)] public string TransCode { get; set; }
    [DataMember(Order = 6)] public string TransRef { get; set; }
}


[DataContract]
[Route("/api/v1/balance/report/balanceDay", "GET")]
public class BalanceDayRequest : IReturn<ResponseMesssageObject<string>>
{
    [DataMember(Order = 1)] public string AccountCode { get; set; }
    [DataMember(Order = 2)] public DateTime Date { get; set; }
}

[Route("/api/v1/balance/report/accountCode", "GET")]
public class BalanceAccountCodesRequest : IReturn<MessageResponseBase>
{
    public string AccountCode { get; set; }

    public string CurrencyCode { get; set; }
}
[DataContract]
[Route("/api/v1/balance/report/BalanceMaxDate", "GET")]
public class BalanceMaxDateRequest : IReturn<decimal>
{
  [DataMember(Order = 1)]  public string AccountCode { get; set; }
  [DataMember(Order = 2)]  public DateTime MaxDate { get; set; }
}
