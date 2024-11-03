using System;
using System.Runtime.Serialization;
using GMB.Topup.Shared;


namespace GMB.Topup.Gw.Model.Dtos;

public class SaleRequestDto
{
    public SaleRequestDto()
    {
        Id = Guid.NewGuid();
        CreatedTime = DateTime.Now;
        Quantity = 1;
    }

    public SaleRequestDto(Guid id)
    {
        Id = id;
        CreatedTime = DateTime.Now;
        Quantity = 1;
    }

    [DataMember(Order = 1)] public Guid Id { get; set; }
    [DataMember(Order = 2)] public string ReceiverInfo { get; set; }
    [DataMember(Order = 3)] public string ReceiverType { get; set; }
    [DataMember(Order = 4)] public bool IsDiscountPaid { get; set; }
    [DataMember(Order = 5)] public SaleRequestType SaleRequestType { get; set; }

    [DataMember(Order = 6)] public SaleType SaleType { get; set; }

    //public int Timeout { get; set; }
    [DataMember(Order = 7)] public decimal Amount { get; set; }
    [DataMember(Order = 8)] public decimal Price { get; set; }
    [DataMember(Order = 9)] public SaleRequestStatus Status { get; set; }
    [DataMember(Order = 10)] public DateTime CreatedTime { get; set; }
    [DataMember(Order = 11)] public DateTime? RequestDate { get; set; }
    [DataMember(Order = 12)] public DateTime? ResponseDate { get; set; }

    [DataMember(Order = 13)] public string Provider { get; set; }
    // public DateTime EndProcessTime { get; set; }

    [DataMember(Order = 14)] public string Vendor { get; set; }
    [DataMember(Order = 15)] public string PartnerCode { get; set; }
    [DataMember(Order = 16)] public string ParentCode { get; set; }
    [DataMember(Order = 17)] public string TransRef { get; set; }
    [DataMember(Order = 18)] public string TransCode { get; set; }
    [DataMember(Order = 19)] public string ProductCode { get; set; }
    [DataMember(Order = 20)] public string ProductProvider { get; set; }
    [DataMember(Order = 21)] public string ServiceCode { get; set; }
    [DataMember(Order = 22)] public string PaymentTransCode { get; set; }
    [DataMember(Order = 23)] public decimal PaymentAmount { get; set; }
    [DataMember(Order = 24)] public decimal? DiscountRate { get; set; }
    [DataMember(Order = 25)] public decimal? FixAmount { get; set; }
    [DataMember(Order = 26)] public decimal? DiscountAmount { get; set; }
    [DataMember(Order = 27)] public decimal? Fee { get; set; }
    [DataMember(Order = 28)] public string CurrencyCode { get; set; }
    [DataMember(Order = 29)] public string CategoryCode { get; set; }
    [DataMember(Order = 30)] public int Quantity { get; set; }
    [DataMember(Order = 31)] public string Email { get; set; }
    [DataMember(Order = 32)] public decimal RevertAmount { get; set; }
    [DataMember(Order = 33)] public string StaffAccount { get; set; }
    [DataMember(Order = 34)] public string StaffUser { get; set; }
    [DataMember(Order = 35)] public string ExtraInfo { get; set; }
    [DataMember(Order = 36)] public Channel Channel { get; set; }
    [DataMember(Order = 37)] public string ProviderTransCode { get; set; }
    [DataMember(Order = 38)] public string RequestIp { get; set; }

    [DataMember(Order = 39)] public AgentType AgentType { get; set; }

    [DataMember(Order = 40)] public int? SyncStatus { get; set; }

    [DataMember(Order = 41)]
    public string ProviderResponseCode { get; set; } //Cai này để lưu mã gd ncc trả về. đặt sai tên

    [DataMember(Order = 42)] public string ReceiverTypeResponse { get; set; }
    [DataMember(Order = 43)] public string ParentProvider { get; set; }
    [DataMember(Order = 44)] public bool IsCheckReceiverTypeSuccess { get; set; }
    [DataMember(Order = 45)] public string ReferenceCode { get; set; }
    [DataMember(Order = 46)] public double ProcessedTime { get; set; }

    // public string BatchTransCode { get; set; }
    // public string WorkerApp { get; set; }      
}

public class SaleItemDto //: ITopupItemRequest
{
    public Guid Id { get; set; }
    public string SaleTransCode { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime CardExpiredDate { get; set; }
    public string TransCode { get; set; }
    public int Amount { get; set; }
    public string CardCode { get; set; }
    public string Serial { get; set; }
    public int CardValue { get; set; }
    public SaleRequestStatus Status { get; set; }
    public string SaleType { get; set; }
    public string Vendor { get; set; }
    public string ProductCode { get; set; }
    public string PartnerCode { get; set; }
    public string ServiceCode { get; set; }
    public string SupplierCode { get; set; }
}

public class PayBatchItemDto
{
    public Guid Id { get; set; }
    public DateTime CreatedTime { get; set; }
    public int Amount { get; set; }
    public decimal PaymentAmount { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal? Fee { get; set; }
    public int Quantity { get; set; }
    public string ReceiverInfo { get; set; }
    public SaleRequestStatus Status { get; set; }

    public string PartnerCode { get; set; }
    public string ProductCode { get; set; }
    public string ProductName { get; set; }

    public string CategoryCode { get; set; }

    public string CategoryName { get; set; }
    public string ServiceCode { get; set; }
    public string ServiceName { get; set; }
    public string TransRef { get; set; }
}