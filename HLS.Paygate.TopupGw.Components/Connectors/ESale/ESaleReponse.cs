using HLS.Paygate.TopupGw.Components.Connectors.ESale;
using Nest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace HLS.Paygate.TopupGw.Components.Connectors.ESale
{

    [DataContract()]
    class GetBalanceResponse
    {
        [DataMember(Name = "retCode")]
        public int RetCode { get; set; }

        [DataMember(Name = "retMsg")]
        public string RetMsg { get; set; }

        [DataMember(Name = "data")]
        public BalanceDetail Data { get; set; }
    }

    [DataContract()]
    class ESaleResponse
    {
        [DataMember(Name = "retCode")]
        public int RetCode { get; set; }

        [DataMember(Name = "retMsg")]
        public string RetMsg { get; set; }

        [DataMember(Name = "data")]
        public TransDetail Data { get; set; }
    }


    [DataContract()]
    class TransDetail
    {
        [DataMember(Name = "transId")]
        public string TransId { get; set; }
        [DataMember(Name = "eSaleTransId")]
        public string ESaleTransId { get; set; }
        [DataMember(Name = "discount")]
        public decimal Discount { get; set; }
        [DataMember(Name = "totalAmount")]
        public decimal TotalAmount { get; set; }

        [DataMember(Name = "cardsList")]
        public List<cardDetail> CardsList { get; set; }
    }

    [DataContract()]
    class BalanceDetail
    {
        [DataMember(Name = "agencyCode")]
        public string AgencyCode { get; set; }

        [DataMember(Name = "balance")]
        public decimal Balance { get; set; }

        [DataMember(Name = "zxBalance")]
        public decimal ZxBalance { get; set; }
    }

    [DataContract()]
    class CheckTransResponse
    {
        [DataMember(Name = "retCode")]
        public int RetCode { get; set; }

        [DataMember(Name = "retMsg")]
        public string RetMsg { get; set; }

        [DataMember(Name = "data")]
        public CheckTransDetail Data { get; set; }
    }

    [DataContract()]
    class CheckTransDetail
    {
        [DataMember(Name = "transId")]
        public string TransId { get; set; }
        [DataMember(Name = "eSaleTransId")]
        public string ESaleTransId { get; set; }

        [DataMember(Name = "cardsList")]
        public List<cardDetail> CardsList { get; set; }

        [DataMember(Name = "discount")]
        public decimal Discount { get; set; }
        [DataMember(Name = "totalAmount")]
        public decimal TotalAmount { get; set; }

        [DataMember(Name = "supplierCode")]
        public string SupplierCode { get; set; }

        [DataMember(Name = "unitPrice")]
        public string unitPrice { get; set; }

        [DataMember(Name = "quantity")]
        public int Quantity { get; set; }

        [DataMember(Name = "transactionDate")]
        public string TransDate { get; set; }
    }

    [DataContract()]
    class cardDetail
    {
        [DataMember(Name = "serial")]
        public string Serial { get; set; }
        [DataMember(Name = "cardCode")]
        public string CardCode { get; set; }
        [DataMember(Name = "expiredDate")]
        public string ExpiredDate { get; set; }
    }

}
