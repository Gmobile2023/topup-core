using System;
using ServiceStack;
using HLS.Paygate.Gw.Model.Commands;
using HLS.Paygate.Shared;

namespace HLS.Paygate.Stock.Contracts.ApiRequests
{
    [Route("/api/v1/stock/card/get_for_mapping", "GET")]
    public class GetCardForMappingRequest : IPatch, IReturn<MessageResponseBase>
    {
        public string Vendor { get; set; }
        public int Amount { get; set; }
        public bool CardTimeOut { get; set; }
    }

    [Route("/api/v1/stock/card/back_car_for_mapping", "POST")]
    public class BackCardStockMappingRequest : IPatch, IReturn<MessageResponseBase>
    {
        public string Vendor { get; set; }
        public int Amount { get; set; }
        public string CardCode { get; set; }
        public string Serial { get; set; }
        public string StockCode { get; set; }
    }

    [Route("/api/v1/stock/card/card_confirm", "POST")]
    public class CardConfirmRequest : IPost, IReturn<MessageResponseBase>
    {
        public Guid CardId { get; set; }
        public string StockCode { get;set; }
        public CardConfirmType ConfirmType { get;set; }
    }
}