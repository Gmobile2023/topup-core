using ServiceStack;

namespace HLS.Paygate.TopupGw.Contacts.ApiRequests
{
    [Route("/api/v1/check_trans", "GET")]
    public class CheckTransRequest
    {
        public string TransCodeToCheck { get; set; }
        public string ProviderCode { get; set; }
        public string ServiceCode { get; set; }
    }
}
