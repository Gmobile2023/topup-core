using System;
using Orleans;

namespace HLS.Paygate.Stock.Contracts.Dtos;

[GenerateSerializer]
public class CardDto
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public string StockCode { get; set; }
    [Id(2)] public string BatchCode { get; set; }
    [Id(3)] public string CardCode { get; set; }
    [Id(4)] public string Serial { get; set; }
    [Id(5)] public DateTime ExpiredDate { get; set; }
    [Id(6)] public DateTime ImportedDate { get; set; }
    [Id(7)] public DateTime? ExportedDate { get; set; }
    [Id(8)] public byte Status { get; set; }
    [Id(9)] public DateTime? UsedDate { get; set; }
    [Id(10)] public string ExportTransCode { get; set; }
    [Id(11)] public string CardTransCode { get; set; }
    [Id(12)] public int CardValue { get; set; }
    [Id(13)] public string ProductCode { get; set; }

    [Id(14)] public string ProviderCode { get; set; }
    [Id(15)] public string ServiceCode { get; set; }
    [Id(16)] public string CategoryCode { get; set; }
    [Id(17)] public string TransCode { get; set; }
}

public class CardSimpleDto
{
    public string CardCode { get; set; }
    public string ProductCode { get; set; }
    public string Serial { get; set; }
    public DateTime ExpiredDate { get; set; }
    public decimal CardValue { get; set; }
}