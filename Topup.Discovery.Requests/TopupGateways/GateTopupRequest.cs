using System;
using System.Runtime.Serialization;
using Topup.Shared;
using ServiceStack;

namespace Topup.Discovery.Requests.TopupGateways;

[DataContract]
[Route("/api/v1/topup", "POST")]
public class GateTopupRequest : IPost, IReturn<NewMessageResponseBase<ResponseProvider>>
{
    [DataMember(Order = 1)] public string ServiceCode { get; set; }
    [DataMember(Order = 2)] public string CategoryCode { get; set; }
    [DataMember(Order = 3)] public string Vendor { get; set; }
    [DataMember(Order = 4)] public decimal Amount { get; set; }
    [DataMember(Order = 5)] public string ReceiverInfo { get; set; }
    [DataMember(Order = 6)] public string TransRef { get; set; }
    [DataMember(Order = 7)] public string ProviderCode { get; set; }
    [DataMember(Order = 8)] public DateTime RequestDate { get; set; }
    [DataMember(Order = 9)] public string ProductCode { get; set; }
    [DataMember(Order = 10)] public string PartnerCode { get; set; }
    [DataMember(Order = 11)] public string ReferenceCode { get; set; }
    [DataMember(Order = 12)] public string TransCodeProvider { get; set; }

    //nhannv: 
    //Thoi gian xu ly giao dich
    [DataMember(Order = 13)] public int? ProviderSetTransactionTimeout { get; set; }
    //Thoi gian check ket qua
    [DataMember(Order = 14)] public int? ProviderMaxWaitingTimeout { get; set; }
    /// <summary>
    /// Trả kết quả ngay khi tiếp nhận thành công giao dịch
    /// </summary>
    [DataMember(Order = 15)] public bool IsEnableResponseWhenJustReceived { get;set ; }
    /// <summary>
    /// Trạng thái trả ra tiếp nhận giao dịch
    /// </summary>
    [DataMember(Order = 16)] public string StatusResponseWhenJustReceived { get; set; }
    [DataMember(Order = 17)] public int? WaitingTimeResponseWhenJustReceived { get; set; }

}