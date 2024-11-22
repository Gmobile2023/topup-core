using System;
using Topup.Gw.Model.Dtos;
using Topup.Shared;
using System.Runtime.Serialization;
using ServiceStack;
using ServiceStack.DataAnnotations;

namespace Topup.Discovery.Requests.Workers;

[DataContract]
[Route("/api/v1/worker/topup", "POST")]
public class WorkerTopupRequest : IPost, IUserInfoRequest, IReturn<NewMessageResponseBase<WorkerResult>>
{
    [DataMember(Order = 1)] public string ReceiverInfo { get; set; }
    [DataMember(Order = 2)] public int Amount { get; set; }
    [DataMember(Order = 3)] [Required] public string TransCode { get; set; }
    [DataMember(Order = 4)] [Required] public string CategoryCode { get; set; }
    [DataMember(Order = 5)] public Channel Channel { get; set; }
    [DataMember(Order = 6)] public string StaffUser { get; set; }
    [DataMember(Order = 7)] public string ExtraInfo { get; set; }
    [DataMember(Order = 8)] public string RequestIp { get; set; }
    [DataMember(Order = 9)] [Required] public string ServiceCode { get; set; }
    [DataMember(Order = 10)] [Required] public string ProductCode { get; set; }

    [DataMember(Order = 11)] public DateTime RequestDate { get; set; }

    [DataMember(Order = 12)] public bool IsCheckReceiverType { get; set; }
    [DataMember(Order = 13)] [Required] public string PartnerCode { get; set; }
    [DataMember(Order = 14)] public string StaffAccount { get; set; }
    [DataMember(Order = 15)] public SystemAccountType AccountType { get; set; }
    [DataMember(Order = 16)] public AgentType AgentType { get; set; }

    [DataMember(Order = 17)] public string ParentCode { get; set; }

    [DataMember(Order = 18)] public bool IsCheckPhone { get; set; }
    [DataMember(Order = 19)] public string CurrencyCode { get; set; }
    [DataMember(Order = 20)] public bool IsNoneDiscount { get; set; }
    [DataMember(Order = 21)] public bool IsCheckAllowTopupReceiverType { get; set; }
    [DataMember(Order = 22)] public string DefaultReceiverType { get; set; }
}