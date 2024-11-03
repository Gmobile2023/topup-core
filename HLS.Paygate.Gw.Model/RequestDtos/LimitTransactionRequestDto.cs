using ServiceStack;

namespace HLS.Paygate.Gw.Model.RequestDtos;

[Route("/api/v1/limitration/set-limit-trans-amount", "POST")]
public class CreateOrUpdateLimitAccountTransRequest
{
    public string AccountCode { get; set; }
    public string ServiceCode { get; set; }
    public string CategoryCode { get; set; }
    public string ProductCode { get; set; }
    public decimal LimitPerDay { get; set; }
    public decimal LimitPerTrans { get; set; }
}

[Route("/api/v1/limitration/get-available-limit", "GET")]
public class GetAvailableLimitAccount
{
    public string AccountCode { get; set; }
    public string ServiceCode { get; set; }
    public string CategoryCode { get; set; }
    public string ProductCode { get; set; }
}

[Route("/api/v1/limitration/product/totalday", "GET")]
public class GetTotalPerDayProductRequest
{
    public string AccountCode { get; set; }
    public string ServiceCode { get; set; }
    public string CategoryCode { get; set; }
    public string ProductCode { get; set; }
}