using HLS.Paygate.Shared;
using ServiceStack;

namespace HLS.Paygate.TopupGw.Contacts.ApiRequests
{
    [Route("/api/v1/bill", "GET")]
    public class BillQueryRequest : IGet, IReturn<NewMessageReponseBase<string>>
    {
        public string ReceiverInfo { get; set; }
        public bool IsInvoice { get; set; }
        public string TransRef { get; set; }
        public string ProductCode { get; set; }
        public string ServiceCode { get;set; }
        public string CategoryCode { get; set;}
        public string Vendor { get; set; }
        public string ProviderCode { get; set; }
    }
}
