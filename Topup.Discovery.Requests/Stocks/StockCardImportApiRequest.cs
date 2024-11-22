using System.Collections.Generic;
using Topup.Shared;
using ServiceStack;
using ServiceStack.DataAnnotations;
using System.Runtime.Serialization;
using System;

namespace Topup.Discovery.Requests.Stocks;
[DataContract]
[Route("/api/v1/stock/cards-api-import", "POST")]
public class StockCardImportApiRequest : IPost, IReturn<NewMessageResponseBase<List<NewMessageResponseBase<string>>>>
{
    [DataMember(Order = 1)] [Required] public string Provider { get; set; }
    [DataMember(Order = 2)] public string Description { get; set; }
    [DataMember(Order = 3)] [Required] public List<CardImportApiItemRequest> CardItems { get; set; }
    [DataMember(Order = 4)] public DateTime ? ExpiredDate { get; set; }
}

[DataContract]
[Route("/api/v1/stock/cards-api-check-trans", "POST")]
public class StockCardApiCheckTransRequest : IPost, IReturn<NewMessageResponseBase<string>>
{
    [DataMember(Order = 1)][Required] public string Provider { get; set; }
    [DataMember(Order = 2)][Required] public string TransCodeProvider { get; set; }
}

[DataContract]
public class CardImportApiItemRequest
{
    [DataMember(Order = 1)] public string ServiceCode { get; set; }
    [DataMember(Order = 2)] public string CategoryCode { get; set; }
    [DataMember(Order = 3)] public string ProductCode { get; set; }
    [DataMember(Order = 4)] public decimal CardValue { get; set; }
    [DataMember(Order = 5)] public int Quantity { get; set; }
    [DataMember(Order = 6)] public float Discount { get; set; }
    [DataMember(Order = 7)] public string TransCode { get; set; }
    [DataMember(Order = 8)] public string TransCodeProvider { get; set; }
}