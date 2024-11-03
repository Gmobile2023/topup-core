using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using GMB.Topup.Shared;
using GMB.Topup.Shared.Dtos;
using ServiceStack;

namespace GMB.Topup.Discovery.Requests.Stocks;

[DataContract]
[Route("/api/v1/stock/import")]
public class StockCardImportRequest : IPost, IReturn<List<CardRequestResponseDto>>
{
    [DataMember(Order = 1)] public string StockCode { get; set; }
    [DataMember(Order = 2)] public string ProductCode { get; set; }
    [DataMember(Order = 3)] public int CardValue { get; set; }
    [DataMember(Order = 4)] public int Amount { get; set; }
    [DataMember(Order = 5)] public string BatchCode { get; set; }
    [DataMember(Order = 6)] public string Description { get; set; }
}

[DataContract]
[Route("/api/v1/stock/cardBatchSaleProvider", "GET")]
public class GetCardBatchSaleProviderRequest : IGet, IReturn<ResponseMesssageObject<string>>
{
    [DataMember(Order = 1)] public DateTime Date { get; set; }
    [DataMember(Order = 2)] public string Provider { get; set; }
}


[Route("/api/v1/stock/ProviderConfig", "GET")]
public class GetProviderConfigRequest : IGet, IReturn<ResponseMesssageObject<string>>
{ 
    public string Provider { get; set; }
    public string ProductCode { get; set; }
    public int Quantity { get; set; }
}


[Route("/api/v1/stock/ProviderConfig", "POST")]
public class CreateProviderConfigRequest : IPost, IReturn<ResponseMesssageObject<string>>
{
    public string Provider { get; set; }
    public string ProductCode { get; set; }
    public int Quantity { get; set; }
}

[Route("/api/v1/stock/ProviderConfig", "PUT")]
public class EditProviderConfigRequest : IPut, IReturn<ResponseMesssageObject<string>>
{
    public string Provider { get; set; }
    public string ProductCode { get; set; }
    public int Quantity { get; set; }
}

