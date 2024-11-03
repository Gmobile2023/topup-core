using System.Collections.Generic;
using MongoDbGenericRepository.Models;

namespace GMB.Topup.TopupGw.Domains.Entities
{
    public class ProviderSalePrice : Document
    {
        public string ProviderCode { get; set; }
        public string ProviderType { get; set; }
        public string TopupType { get; set; }
        public decimal CardValue { get; set; }
        public decimal CardPrice { get; set; }      
        public string CardValueName { get; set; }
       
    }
}
