using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using HLS.Paygate.Gw.Model.Dtos;
using HLS.Paygate.Shared;
using HLS.Paygate.Shared.Dtos;
using ServiceStack;
using ServiceStack.DataAnnotations;

namespace Paygate.Discovery.Requests.Workers;

[DataContract]
[Route("/api/v1/worker/pincode", "POST")]
public class WorkerPinCodeRequest : IPost, IUserInfoRequest,
    IReturn<NewMessageReponseBase<List<CardRequestResponseDto>>>
{
    [DataMember(Order = 1)] [Required] public string TransCode { get; set; }
    [DataMember(Order = 2)] [Required] public string CategoryCode { get; set; }
    [DataMember(Order = 3)] public Channel Channel { get; set; }
    [DataMember(Order = 4)] [Required] public int Quantity { get; set; }
    [DataMember(Order = 5)] [Required] public int CardValue { get; set; }
    [DataMember(Order = 6)] public string Email { get; set; }
    [DataMember(Order = 7)] public string RequestIp { get; set; }
    [DataMember(Order = 8)] public DateTime RequestDate { get; set; }
    [DataMember(Order = 9)] public string StaffUser { get; set; }
    [DataMember(Order = 10)] public string ExtraInfo { get; set; }
    [DataMember(Order = 11)] public string ProductCode { get; set; }
    [DataMember(Order = 12)] public string ServiceCode { get; set; }
    [DataMember(Order = 13)] [Required] public string PartnerCode { get; set; }
    [DataMember(Order = 14)] public SystemAccountType AccountType { get; set; }
    [DataMember(Order = 15)] public AgentType AgentType { get; set; }
    [DataMember(Order = 16)] public string ParentCode { get; set; }
    [DataMember(Order = 17)] public string StaffAccount { get; set; }
}