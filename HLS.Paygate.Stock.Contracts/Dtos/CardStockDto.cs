using System;
using System.Linq;

namespace HLS.Paygate.Stock.Contracts.Dtos
{
    public class CardStockDto
    {
        public string Id { get; set; }
        public string StockCode { get; set; }
        public int Inventory { get; set; }
        public int InventoryLimit { get; set; }
        public int MinimumInventoryLimit { get; set; }
        public string Description { get; set; }
        public byte Status { get; set; }
        public decimal CardValue { get; set; }
        public string ProductCode { get; set; }

        public string VendorCode
        {
            get
            {
                if(string.IsNullOrEmpty(this.ProductCode))
                    return "";
                var pCode = ProductCode.Split("_");
                if (!pCode.Any() || pCode.Length == 1) return ProductCode;
                pCode.ToList().RemoveAt(pCode.Length - 1); 
                return string.Join("_", pCode); 
            }
        }
    }
}