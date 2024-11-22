using System;
using System.Runtime.Serialization;
using Topup.Shared;
using ServiceStack;

namespace Topup.Discovery.Requests.Backends;
[DataContract]
[Route("/api/v1/cardBatchSale", "GET")]
public class GetCardBatchRequest : IGet, IReturn<ResponseMesssageObject<string>>
{
    [DataMember(Order = 1)] public DateTime Date { get; set; }
    [DataMember(Order = 2)] public string Provider { get; set; }
}

[Route("/api/v1/backend/cardBatchSale", "GET")]
public class GetReportCardBatchRequest : IGet, IReturn<ResponseMesssageObject<string>>
{
    public DateTime Date { get; set; }
    public string Provider { get; set; }
}