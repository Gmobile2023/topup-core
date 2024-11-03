using System;
using ServiceStack;
using HLS.Paygate.Gw.Model.Enums;
using HLS.Paygate.Shared;

namespace HLS.Paygate.Stock.Contracts.ApiRequests
{
    [Route("/api/v1/stock/cardrequest", "GET")]
    public class CardRequestGetList : PaggingBase, IGet, IReturn<MessagePagedResponseBase>
    {
        public string BatchCode { get; set; }
        public DateTime FromImportDate { get; set; }
        public DateTime ToImportDate { get; set; }
        public DateTime FromExpiredDate { get; set; }
        public DateTime ToExpiredDate { get; set; }
        public DateTime FromExportedDate { get; set; }
        public DateTime ToExportedDate { get; set; }
        public CardRequestStatus Status { get; set; }
        public string Serial { get; set; }
        public string TransCode { get; set; }
        public string TransRef { get; set; }
        public string PartnerCode { get; set; }
        public string Vendor { get; set; }
        public int CardValue { get; set; }
    }
}