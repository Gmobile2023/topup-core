using HLS.Paygate.Stock.Contracts.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HLS.Paygate.Stock.Contracts.Dtos
{
    public class StockTransRequestDto
    {
        public string Provider { get; set; }
        public string BatchCode { get; set; }
        public string TransCode { get; set; }
        public string TransCodeProvider { get; set; }
        public string ServiceCode { get; set; }
        public string CategoryCode { get; set; }
        public string ProductCode { get; set; }
        public decimal ItemValue { get; set; }
        public decimal TotalPrice { get; set; }
        public int Quantity { get; set; }
        public StockBatchStatus Status { get; set; }
        public DateTime CreatedDate { get; set; }
        public bool IsSyncCard { get; set; }
        public DateTime? ExpiredDate { get; set; }
    }
}
