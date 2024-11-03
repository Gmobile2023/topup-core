using System;
using GMB.Topup.Shared;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDbGenericRepository.Models;
using PaygateException = GMB.Topup.Shared.Exceptions.PaygateException;

namespace GMB.Topup.Gw.Domain.Entities;

public class BatchLotRequest : Document
{
    [BsonRepresentation(BsonType.Int32)] public BatchLotRequestStatus Status { get; set; }

    public SaleRequestType SaleRequestType { get; set; }
    public DateTime CreatedTime { get; set; }

    [BsonRepresentation(BsonType.Int32)] public Channel Channel { get; set; }

    public DateTime? EndProcessTime { get; set; }
    public string PartnerCode { get; set; }
    public string BatchCode { get; set; }

    public string Email { get; set; }
    public string StaffAccount { get; set; }
    public string ExtraInfo { get; set; }
    public string BatchType { get; set; }
    public string BatchName { get; set; }

    [BsonRepresentation(BsonType.Decimal128, AllowTruncation = true)]
    public decimal PaymentAmount { get; set; }

    public int Quantity { get; set; }

    public void SetProcessing()
    {
        switch (Status)
        {
            case BatchLotRequestStatus.Completed:
            case BatchLotRequestStatus.Init:
            case BatchLotRequestStatus.Stop:
            default:
                Status = BatchLotRequestStatus.Process;
                break;
        }
    }
}

public class BatchDetail : Document
{
    public BatchLotRequestStatus BatchStatus { get; set; }

    public string ReceiverInfo { get; set; }

    [BsonRepresentation(BsonType.Int32)] public SaleRequestStatus Status { get; set; }

    public SaleRequestType SaleRequestType { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime? UpdateTime { get; set; }
    public string PartnerCode { get; set; }
    public string TransRef { get; set; }
    public string BatchCode { get; set; }
    public string Vendor { get; set; }
    public string Provider { get; set; }
    public string ProductCode { get; set; }
    public string ProductName { get; set; }
    public string CategoryCode { get; set; }
    public string CategoryName { get; set; }
    public string ServiceCode { get; set; }
    public string ServiceName { get; set; }

    [BsonRepresentation(BsonType.Decimal128, AllowTruncation = true)]
    public decimal Amount { get; set; }

    [BsonRepresentation(BsonType.Decimal128, AllowTruncation = true)]
    public decimal PaymentAmount { get; set; }

    public decimal? DiscountAmount { get; set; }
    public decimal? Fee { get; set; }

    public int Quantity { get; set; }
    public string Email { get; set; }
    public string StaffAccount { get; set; }
    public string ExtraInfo { get; set; }

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