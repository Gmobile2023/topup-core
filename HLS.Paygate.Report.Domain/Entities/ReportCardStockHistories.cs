using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDbGenericRepository.Models;

namespace HLS.Paygate.Report.Domain.Entities;

public class ReportCardStockHistories : Document
{
    public string StockCode { get; set; }
    public int CardValue { get; set; }
    public string Serial { get; set; }
    public string ProductCode { get; set; }
    public string CategoryCode { get; set; }
    public string StockType { get; set; }
    public string Vendor { get; set; }

    [BsonRepresentation(BsonType.Int32, AllowTruncation = true)]
    public int Increase { get; set; }

    [BsonRepresentation(BsonType.Int32, AllowTruncation = true)]
    public int Decrease { get; set; }

    [BsonRepresentation(BsonType.Int32, AllowTruncation = true)]
    public int InventoryBefore { get; set; }

    [BsonRepresentation(BsonType.Int32, AllowTruncation = true)]
    public int InventoryAfter { get; set; }

    [BsonRepresentation(BsonType.Int32)] public byte Status { get; set; }

    public string ModifiedBy { get; set; }
    public string CreatedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public DateTime CreatedDate { get; set; }
    public string Description { get; set; }
    public string PartnerCode { get; set; }
    public string TransType { get; set; }
    public string TransRef { get; set; }
    public string TransCode { get; set; }
    public string InventoryType { get; set; }

    public string ServiceCode { get; set; }
}

public class ReportCardStockImExPortDto
{
    public string KeyCode { get; set; }
    public string StoreCode { get; set; }
    public string ServiceName { get; set; }
    public string CategoryCode { get; set; }
    public string CategoryName { get; set; }
    public string ProductCode { get; set; }
    public string ProductName { get; set; }
    public string ProviderCode { get; set; }
    public string ProviderName { get; set; }
    public int CardValue { get; set; }
    public int IncreaseSupplier { get; set; }
    public int IncreaseOther { get; set; }
    public int Sale { get; set; }
    public int ExportOther { get; set; }
    public int Before { get; set; }
    public int After { get; set; }
    public int Current { get; set; }

    public DateTime CreatedDay { get; set; }
}

public class ReportCardStockDayDto
{
    public string ProviderCode { get; set; }
    public string ServiceName { get; set; }
    public string CategoryName { get; set; }    
    public string ProductCode { get; set; }
    public string ProductName { get; set; }
    public int CardValue { get; set; }
    public int Before_Sale { get; set; }
    public int Import_Sale { get; set; }
    public int Export_Sale { get; set; }
    public int After_Sale { get; set; }
    public decimal Monney_Sale { get; set; }
    public int Before_Temp { get; set; }
    public int Import_Temp { get; set; }
    public int Export_Temp { get; set; }
    public int After_Temp { get; set; }
    public decimal Monney_Temp { get; set; }
}