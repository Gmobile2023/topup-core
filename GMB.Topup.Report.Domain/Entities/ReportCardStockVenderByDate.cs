using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDbGenericRepository.Models;

namespace GMB.Topup.Report.Domain.Entities;

public class ReportCardStockProviderByDate : Document
{
    public string StockCode { get; set; }
    public long CardValue { get; set; }
    public string ProviderCode { get; set; }
    public string ProductCode { get; set; }
    public string CategoryCode { get; set; }
    public string ServiceCode { get; set; }
    public string StockType { get; set; }

    [BsonRepresentation(BsonType.Int32, AllowTruncation = true)]
    public long Increase { get; set; }

    [BsonRepresentation(BsonType.Int32, AllowTruncation = true)]
    public long Decrease { get; set; }

    [BsonRepresentation(BsonType.Int32, AllowTruncation = true)]
    public long InventoryBefore { get; set; }

    [BsonRepresentation(BsonType.Int32, AllowTruncation = true)]
    public long InventoryAfter { get; set; }

    [BsonRepresentation(BsonType.Int32, AllowTruncation = true)]
    public long IncreaseSupplier { get; set; }

    [BsonRepresentation(BsonType.Int32, AllowTruncation = true)]
    public long IncreaseOther { get; set; }

    [BsonRepresentation(BsonType.Int32, AllowTruncation = true)]
    public long Sale { get; set; }

    [BsonRepresentation(BsonType.Int32, AllowTruncation = true)]
    public long ExportOther { get; set; }

    [BsonRepresentation(BsonType.Int32)] public byte Status { get; set; }

    public string ModifiedBy { get; set; }
    public string CreatedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public DateTime CreatedDate { get; set; }
    public string ShortDate { get; set; }
    public string Description { get; set; }
}