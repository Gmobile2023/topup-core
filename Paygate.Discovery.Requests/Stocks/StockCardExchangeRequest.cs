using ServiceStack;
using System.Runtime.Serialization;

namespace Paygate.Discovery.Requests.Stocks;

[DataContract]
[Route("/api/v1/stock/exchange")]
public class StockCardExchangeRequest : IPost, IReturn<object>
{
    [DataMember(Order = 1)] public string SrcStockCode { get; set; }
    [DataMember(Order = 2)] public string DesStockCode { get; set; }
    [DataMember(Order = 3)] public string ProductCode { get; set; }
    [DataMember(Order = 4)] public int Amount { get; set; }
    [DataMember(Order = 5)] public string BatchCode { get; set; }
    [DataMember(Order = 6)] public string Description { get; set; }
}