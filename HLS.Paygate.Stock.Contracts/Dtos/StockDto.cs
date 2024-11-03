using System;
using System.Linq;
using HLS.Paygate.Stock.Contracts.Dtos;
using Orleans;
using Orleans.CodeGeneration;

//[assembly: GenerateSerializer(typeof(StockDto))]

namespace HLS.Paygate.Stock.Contracts.Dtos;

[GenerateSerializer]
public class StockDto
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public string StockCode { get; set; }
    [Id(2)] public string StockType { get; set; }
    [Id(3)] public int Inventory { get; set; }
    [Id(4)] public int InventoryLimit { get; set; }
    [Id(5)] public int MinimumInventoryLimit { get; set; }
    [Id(6)] public string Description { get; set; }
    [Id(7)] public byte Status { get; set; }
    [Id(8)] public decimal ItemValue { get; set; }
    [Id(9)] public string KeyCode { get; set; }

    public string VendorCode
    {
        get
        {
            if (string.IsNullOrEmpty(KeyCode))
                return "";
            var pCode = KeyCode.Split("_");
            if (!pCode.Any() || pCode.Length == 1) return KeyCode;
            pCode.ToList().RemoveAt(pCode.Length - 1);
            return string.Join("_", pCode);
        }
    }

    [Id(10)] public string ServiceCode { get; set; }
    [Id(11)] public string CategoryCode { get; set; }
}