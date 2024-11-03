using GMB.Topup.Shared;
using ServiceStack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GMB.Topup.Stock.Contracts.Enums;

namespace GMB.Topup.Stock.Contracts.ApiRequests
{

    [Route("/api/v1/stock/stockTrans/list", "GET")]
    public class CardStockTransListRequest : PaggingBase, IGet, IReturn<MessagePagedResponseBase>
    {
        public string Provider { get; set; }
        public string BatchCode { get; set; }
        public string TransCode { get; set; }
        public string TransCodeProvider { get; set; }
        public string ServiceCode { get; set; }
        public string CategoryCode { get; set; }
        public string ProductCode { get; set; }
        public StockBatchStatus Status { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
    }

}
