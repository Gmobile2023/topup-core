using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDbGenericRepository.Models;

namespace Topup.Stock.Domains.Entities;

public class Stock : Document
{
    /// <summary>
    ///     Mã kho
    /// </summary>
    public string StockCode { get; set; }

    public string KeyCode { get; set; }

    [BsonRepresentation(BsonType.Decimal128, AllowTruncation = true)]
    public decimal ItemValue { get; set; }

    public string StockType { get; set; }

    /// <summary>
    ///     Tồn kho
    /// </summary>
    public int Inventory { get; set; }

    /// <summary>
    ///     Han muc ton kho.
    /// </summary>
    public int InventoryLimit { get; set; }

    /// <summary>
    ///     Han muc toi thieu
    /// </summary>
    public int MinimumInventoryLimit { get; set; }

    public string Description { get; set; }
    public byte Status { get; set; }
    public string ServiceCode { get; set; }
    public string CategoryCode { get; set; }
}