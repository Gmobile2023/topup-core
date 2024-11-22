using System;
using System.Collections.Generic;
using Topup.Stock.Contracts.Enums;
using MongoDbGenericRepository.Models;

namespace Topup.Stock.Domains.Entities
{
    public class StockProviderConfig : Document
    {
        public string Provider { get; set; }
        public string ProductCode { get; set; }
        public int Quantity { get; set; }
    }
}
