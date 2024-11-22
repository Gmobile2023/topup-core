using System;
using Topup.Gw.Model.Dtos;
using Topup.Shared;
using System.Runtime.Serialization;
using ServiceStack;
using ServiceStack.DataAnnotations;

namespace Topup.Discovery.Requests.Workers;
[DataContract]
[Route("/api/v1/worker/pay-bill", "POST")]
public class WorkerPayBillRequest : IPost, IUserInfoRequest, IReturn<NewMessageResponseBase<WorkerResult>>
{
    [DataMember(Order = 1)] [Required] public string ReceiverInfo { get; set; }
    [DataMember(Order = 2)] [Required] public decimal Amount { get; set; }
    [DataMember(Order = 3)] [Required] public string TransCode { get; set; }
    [DataMember(Order = 4)] [Required] public string CategoryCode { get; set; }
    [DataMember(Order = 5)] [Required] public string ProductCode { get; set; }
    [DataMember(Order = 6)] [Required] public string ServiceCode { get; set; }
    [DataMember(Order = 7)] [Required] public Channel Channel { get; set; }
    [DataMember(Order = 8)] public string RequestIp { get; set; }
    [DataMember(Order = 9)] public string ExtraInfo { get; set; }
    [DataMember(Order = 10)] public bool IsInvoice { get; set; }
    [DataMember(Order = 11)] public DateTime RequestDate { get; set; }
    [DataMember(Order = 12)] [Required] public string PartnerCode { get; set; }
    [DataMember(Order = 13)] public string StaffAccount { get; set; }
    [DataMember(Order = 14)] public SystemAccountType AccountType { get; set; }
    [DataMember(Order = 15)] public AgentType AgentType { get; set; }
    [DataMember(Order = 16)] public string ParentCode { get; set; }
}