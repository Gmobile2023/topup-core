namespace Topup.Shared.ConfigDtos;

public class ServiceUrlConfig
{
    public GrpcServices GrpcServices { get; set; }
    public string GatewayPrivate { get; set; }
    public string GrpcGateway { get; set; }
}

public class GrpcServices
{
    public string Common { get; set; }
    public string Worker { get; set; }
    public string Sale { get; set; }
    public string Stock { get; set; }
    public string TopupGateway { get; set; }
    public string Backend { get; set; }
    public string Balance { get; set; }
    public string Commission { get; set; }
    public string Report { get; set; }
    public string MobileInfo { get; set; }
}
public static class GrpcServiceName
{
    public static string Common = "Common";
    public static string Worker = "Worker";
    public static string Sale = "Sale";
    public static string Stock = "Stock";
    public static string TopupGateway = "TopupGateway";
    public static string Backend = "Backend";
    public static string Balance = "Balance";
    public static string Commission = "Commission";
    public static string Report = "Report";
    public static string MobileInfo = "MobileInfo";
}