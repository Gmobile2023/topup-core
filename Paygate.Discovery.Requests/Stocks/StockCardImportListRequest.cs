using System.Collections.Generic;
using System.Runtime.Serialization;
using HLS.Paygate.Shared;
using HLS.Paygate.Shared.Dtos;
using ServiceStack;

namespace Paygate.Discovery.Requests.Stocks;

[DataContract]
public class StockCardImportListRequest : IPost, IReturn<NewMessageReponseBase<string>>
{
    [DataMember(Order = 1)] public string BatchCode { get; set; }
    [DataMember(Order = 2)] public string ProductCode { get; set; }
    [DataMember(Order = 3)] public List<CardItemsImport> CardItems { get; set; }
}