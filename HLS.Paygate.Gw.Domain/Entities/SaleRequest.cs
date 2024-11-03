using System;
using HLS.Paygate.Shared;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDbGenericRepository.Models;
using PaygateException = HLS.Paygate.Shared.Exceptions.PaygateException;

namespace HLS.Paygate.Gw.Domain.Entities;

public class SaleRequest : Document
{
    public string ReceiverInfo { get; set; }
    public string ReceiverType { get; set; }

    [BsonRepresentation(BsonType.Decimal128, AllowTruncation = true)]
    public decimal Amount { get; set; }

    [BsonRepresentation(BsonType.Decimal128, AllowTruncation = true)]
    public decimal Price { get; set; }

    public bool IsDiscountPaid { get; set; }

    [BsonRepresentation(BsonType.Int32)] public SaleRequestStatus Status { get; set; }

    public SaleRequestType SaleRequestType { get; set; }

    [BsonRepresentation(BsonType.Int32)] public SaleType SaleType { get; set; }

    public DateTime CreatedTime { get; set; }
    public DateTime? RequestDate { get; set; }

    [BsonRepresentation(BsonType.Int32)] public Channel Channel { get; set; }

    // public DateTime EndProcessTime { get; set; }
    public string PartnerCode { get; set; }
    public string ParentCode { get; set; }

    public string TransRef { get; set; }
    public string TransCode { get; set; }
    public string Vendor { get; set; }
    public string Provider { get; set; }
    public string ServiceCode { get; set; }
    public string PaymentTransCode { get; set; }

    public DateTime? ResponseDate { get; set; }

    [BsonRepresentation(BsonType.Decimal128, AllowTruncation = true)]
    public decimal PaymentAmount { get; set; }

    [BsonRepresentation(BsonType.Decimal128, AllowTruncation = true)]
    public decimal? DiscountRate { get; set; }

    public decimal? FixAmount { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal? Fee { get; set; }
    public string CurrencyCode { get; set; }
    public string ProductCode { get; set; } //Sản phẩm
    public string CategoryCode { get; set; }
    public int Quantity { get; set; }
    public string Email { get; set; }
    public string StaffAccount { get; set; }
    public string StaffUser { get; set; }
    public string ExtraInfo { get; set; }
    public string ProviderTransCode { get; set; }
    public string ProviderResponseCode { get; set; }//Mã gd ncc trả về. chỗ này đặt sai tên
    public string ReceiverTypeResponse { get; set; }
    public string RequestIp { get; set; }
    public string ParentProvider { get; set; }
    public string ReferenceCode { get; set; }
    public double ProcessedTime { get; set; }
    public bool IsCheckReceiverTypeSuccess { get; set; }
    [BsonRepresentation(BsonType.Int32)] public AgentType AgentType { get; set; }

    public int? SyncStatus { get; set; }

    public void SetProcessing()
    {
        switch (Status)
        {
            case SaleRequestStatus.Success:
            case SaleRequestStatus.Canceled:
            case SaleRequestStatus.Failed:
            case SaleRequestStatus.TimeOver:
                throw new PaygateException("cannot_process_completed_request",
                    $"Cannot process completed request with id: '{Id}'.");
            case SaleRequestStatus.InProcessing:
                throw new PaygateException("cannot_process_processing_request",
                    $"Cannot process processing request with id: '{Id}'.");
            default:
                Status = SaleRequestStatus.InProcessing;
                break;
        }
    }
}

public class SaleItem
    : Document
{
    [BsonRepresentation(BsonType.Decimal128, AllowTruncation = true)]
    public decimal Amount { get; set; }

    [BsonRepresentation(BsonType.Int32)] public byte Status { get; set; }

    public string TransCode { get; set; }
    public string SaleTransCode { get; set; }
    public string CardTransCode { get; set; }
    public string CardCode { get; set; }
    public int CardValue { get; set; }
    public string Serial { get; set; }
    public string SaleType { get; set; }
    public string Vendor { get; set; }
    public string ProductCode { get; set; }

    public string ProductProvider { get; set; }

    // public string PaymentSms { get; set; }
    // public string ShortCode { get; set; }
    public string ServiceCode { get; set; }
    public string SupplierCode { get; set; }
    public string ExtraInfo { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime CardExpiredDate { get; set; }
}

public class TopupItem : Document
{
    public string SaleTransCode { get; set; }
    public DateTime CreatedTime { get; set; }
    public string TransCode { get; set; }
    public int Amount { get; set; }
    public string ReceiverInfo { get; set; }
    public SaleRequestStatus Status { get; set; }
    public string TopupType { get; set; }
    public string Vendor { get; set; }
    public string ProductCode { get; set; }
    public string PartnerCode { get; set; }
    public string ServiceCode { get; set; }
    public string SupplierCode { get; set; }
}