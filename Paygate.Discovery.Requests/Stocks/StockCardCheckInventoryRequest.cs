using HLS.Paygate.Shared;
using ServiceStack;
using System.Runtime.Serialization;

namespace Paygate.Discovery.Requests.Stocks;

[DataContract]
[Route("/api/v1/stock/check")]
public class StockCardCheckInventoryRequest : IPost, IReturn<NewMessageReponseBase<int>>
{
    [DataMember(Order = 1)] public string StockCode { get; set; }
    [DataMember(Order = 2)] public string ProductCode { get; set; }
    [DataMember(Order = 3)] public decimal CardValue { get; set; }
}