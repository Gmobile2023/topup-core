using System;
using System.Runtime.Serialization;
using Topup.Gw.Model.Dtos;
using Topup.Shared;
using ServiceStack;

namespace Topup.Discovery.Requests.Backends;

[DataContract]
[Route("/api/v1/topupSale", "GET")]
public class GetSaleTopupRequest : IGet, IReturn<ResponseMesssageObject<string>>
{
    [DataMember(Order = 1)] public DateTime FromDate { get; set; }
    [DataMember(Order = 2)] public DateTime ToDate { get; set; }
    [DataMember(Order = 3)] public string PartnerCode { get; set; }
    [DataMember(Order = 4)] public string Provider { get; set; }
}


[Route("/api/v1/backend/topupSale", "GET")]
public class GetReportSaleTopupRequest : IGet, IReturn<ResponseMesssageObject<string>>
{
     public DateTime FromDate { get; set; }
     public DateTime ToDate { get; set; }
     public string PartnerCode { get; set; }
     public string Provider { get; set; }
}