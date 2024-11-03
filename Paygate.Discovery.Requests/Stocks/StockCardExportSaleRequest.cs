using System.Collections.Generic;
using System.Runtime.Serialization;
using HLS.Paygate.Shared;
using HLS.Paygate.Shared.Dtos;
using ServiceStack;

namespace Paygate.Discovery.Requests.Stocks;

[DataContract]
[Route("/api/v1/stock/export-to-sale")]
public class StockCardExportSaleRequest : IPost, IReturn<NewMessageReponseBase<List<CardRequestResponseDto>>>
{

    [DataMember(Order = 1)] public string StockCode { get; set; }
    [DataMember(Order = 2)] public string ProductCode { get; set; }
    [DataMember(Order = 3)] public int Amount { get; set; }
    [DataMember(Order = 4)] public string BatchCode { get; set; }
    [DataMember(Order = 5)] public string Description { get; set; }
    [DataMember(Order = 6)] public string TransCode { get; set; }
}