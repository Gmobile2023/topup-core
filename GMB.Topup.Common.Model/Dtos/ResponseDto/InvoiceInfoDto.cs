using System.Runtime.Serialization;

namespace GMB.Topup.Common.Model.Dtos.ResponseDto;

public class SendNotificationData
{
    [DataMember(Name = "PartnerCode")] public string PartnerCode { get; set; }
    [DataMember(Name = "StaffAccount")] public string StaffAccount { get; set; }
    [DataMember(Name = "TransType")] public string TransType { get; set; }
    [DataMember(Name = "ServiceCode")] public string ServiceCode { get; set; }
    [DataMember(Name = "CategoryCode")] public string CategoryCode { get; set; }
    [DataMember(Name = "ProductCode")] public string ProductCode { get; set; }
    [DataMember(Name = "TransCode")] public string TransCode { get; set; }
    [DataMember(Name = "Amount")] public decimal Amount { get; set; }
    [DataMember(Name = "Payload")] public string Payload { get; set; }
}

public class InvoiceBillDto
{
    public string Email { get; set; }

    public string FullName { get; set; }

    //Max KH
    public string CustomerReference { get; set; }

    public string Address { get; set; }

    //Kỳ thanh toán
    public string Period { get; set; }
    public string ProductName { get; set; }
    public string ProductCode { get; set; }
}