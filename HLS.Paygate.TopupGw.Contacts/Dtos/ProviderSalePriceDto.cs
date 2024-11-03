using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HLS.Paygate.TopupGw.Contacts.Dtos
{
    public class ProviderSalePriceDto : DocumentDto
    {
        public string ProviderCode { get; set; }
        public string ProviderType { get; set; }
        public string TopupType { get; set; }
        public decimal CardValue { get; set; }
        public decimal CardPrice { get; set; }       
        public string CardValueName { get; set; }
        public string DataPackageName{get;set;}
    }
}
