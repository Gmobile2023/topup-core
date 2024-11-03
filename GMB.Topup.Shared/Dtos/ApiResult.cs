using GMB.Topup.Shared.Dtos;
using System.Runtime.Serialization;
using System.Collections.Generic;
[DataContract]
public class WorkerResult
{
    [DataMember(Order = 1)] public string TransCode { get; set; }
    [DataMember(Order = 2)] public string TransRef { get; set; }
    [DataMember(Order = 3)] public decimal Amount { get; set; }
    [DataMember(Order = 4)] public decimal PaymentAmount { get; set; }
    [DataMember(Order = 5)] public decimal Discount { get; set; }
    [DataMember(Order = 6)] public bool Responsed { get; set; }
    [DataMember(Order = 7)] public string ReceiverType { get; set; }
    [DataMember(Order = 8)] public string ServiceCode { get; set; }
    [DataMember(Order = 9)] public decimal Fee { get; set; }
    [DataMember(Order = 10)] public int Quantity { get; set; }
    [DataMember(Order = 11)] public List<CardRequestResponseDto> CardInfo { get; set; }
}
[DataContract]
public class SaleResult
{
    [DataMember(Order = 1)] public string TransCode { get; set; } //Mã giao dịch đối tác
    [DataMember(Order = 2)] public string ReferenceCode { get; set; } //Đã giao dịch nhất trần
    [DataMember(Order = 3)] public decimal Amount { get; set; }
    [DataMember(Order = 4)] public decimal PaymentAmount { get; set; }
    [DataMember(Order = 5)] public decimal Discount { get; set; }
    [DataMember(Order = 6)] public string ReceiverType { get; set; }
    [DataMember(Order = 7)] public string ServiceCode { get; set; }
}
[DataContract]
public partial class CheckTransResult
{
   [DataMember(Order = 1)]  public string TransCode { get; set; } //Mã giao dịch đối tác
   [DataMember(Order = 2)]  public string ReferenceCode { get; set; } //Đã giao dịch nhất trần
   [DataMember(Order = 3)]  public decimal Amount { get; set; }
   [DataMember(Order = 4)]  public decimal PaymentAmount { get; set; }
   [DataMember(Order = 5)]  public decimal Discount { get; set; }
   [DataMember(Order = 6)]  public string ReceiverType { get; set; }
   [DataMember(Order = 7)]  public string ServiceCode { get; set; }
   [DataMember(Order = 8)]  public List<CardResponsePartnerDto> cards { get; set; }
}