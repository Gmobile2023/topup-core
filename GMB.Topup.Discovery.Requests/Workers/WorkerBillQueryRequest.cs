using System;
using GMB.Topup.Gw.Model.Dtos;
using GMB.Topup.Shared;
using GMB.Topup.Shared.Dtos;
using ServiceStack;
using ServiceStack.DataAnnotations;
using System.Runtime.Serialization;

namespace GMB.Topup.Discovery.Requests.Workers;
[DataContract]
[Route("/api/v1/worker/bill-query", "GET")]
public class WorkerBillQueryRequest : IGet, IUserInfoRequest, IReturn<NewMessageResponseBase<InvoiceResultDto>>
{
    [DataMember(Order = 1)] [Required] public string ReceiverInfo { get; set; }
    [DataMember(Order = 2)] [Required] public string CategoryCode { get; set; }
    [DataMember(Order = 3)] [Required] public string ProductCode { get; set; }
    [DataMember(Order = 4)] public string ServiceCode { get; set; }
    [DataMember(Order = 5)] public string TransCode { get; set; }
    [DataMember(Order = 6)] public bool IsInvoice { get; set; }
    [DataMember(Order = 7)] public string RequestIp { get; set; }
    [DataMember(Order = 8)] public DateTime RequestDate { get; set; }
    [DataMember(Order = 9)] [Required] public string PartnerCode { get; set; }
    [DataMember(Order = 10)] public string StaffAccount { get; set; }
    [DataMember(Order = 11)] public SystemAccountType AccountType { get; set; }
    [DataMember(Order = 12)] public AgentType AgentType { get; set; }
    [DataMember(Order = 13)] public string ParentCode { get; set; }
}