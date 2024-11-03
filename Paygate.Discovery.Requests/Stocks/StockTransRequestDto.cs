using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Paygate.Discovery.Requests.Stocks
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
        public decimal TotalPrice { get; set; }
        public decimal ItemValue { get; set; }
        public int Quantity { get; set; }
        public int Status { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
