using Topup.Shared;
using ServiceStack;

namespace Topup.Report.Model.Dtos.RequestDto;

[Route("/api/services/app/Private/GetProductInfo")]
public class GetProductInfoRequest
{
    public string ProductCode { get; set; }
}

[Route("/api/services/app/Private/GetUserFullInfoQuery")]
public class GetUserInfoQueryRequest
{
    public string AccountCode { get; set; }

    public int UserId { get; set; }

    public int AgentType { get; set; }
}

[Route("/api/services/app/Private/GetUserPeriodInfoQuery")]
public class GetUserPeriodRequest
{
    public string AgentCode { get; set; }

    public AgentType AgentType { get; set; }
}

[Route("/api/services/app/Private/GetService")]
public class GetServiceRequest
{
    public string ServiceCode { get; set; }
}

[Route("/api/services/app/Private/GetVendorTrans")]
public class GetVenderRequest
{
    public string Code { get; set; }
}

[Route("/api/services/app/Private/GetProvider")]
public class GetProviderRequest
{
    public string ProviderCode { get; set; }
}

[Route("/api/services/app/Private/GetLimitDebtAccount")]
public class GetLimitDebtAccountRequest
{
    public string AccountCode { get; set; }
}

[Route("api/v1/Private/GetSaleAssignInfo")]
public class GetSaleAssignInfoRequest
{
    public int UserId { get; set; }
}

[Route("/api/v1/balance", "GET")]
public class GetBalanceTopupRequest
{
    public string ProviderCode { get; set; }
}