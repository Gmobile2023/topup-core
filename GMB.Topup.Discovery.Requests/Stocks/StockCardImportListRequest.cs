using System.Collections.Generic;
using System.Runtime.Serialization;
using GMB.Topup.Shared;
using GMB.Topup.Shared.Dtos;
using ServiceStack;

namespace GMB.Topup.Discovery.Requests.Stocks;

[DataContract]
public class StockCardImportListRequest : IPost, IReturn<NewMessageResponseBase<string>>
{
    [DataMember(Order = 1)] public string BatchCode { get; set; }
    [DataMember(Order = 2)] public string ProductCode { get; set; }
    [DataMember(Order = 3)] public List<CardItemsImport> CardItems { get; set; }
}