using System.Collections.Generic;

namespace HLS.Paygate.Shared.ConfigDtos;

public class HealthCheckConfig
{
    public bool HealthChecksEnabled { get; set; }
    public string Url { get; set; }
    public HealthChecksUiConfig HealthChecksUI { get; set; }
    public List<ServiceCheck> ServiceChecks { get; set; }
    public HealthCheckEndpoints CheckEndpoints { get; set; }
}

public class HealthChecksUiConfig
{
    public bool HealthChecksUIEnabled { get; set; }
    public bool IsCheckService { get; set; }
    public int EvaluationTimeOnSeconds { get; set; }
    public int MinimumSecondsBetweenFailureNotifications { get; set; }
}

public class ServiceCheck
{
    public string Host { get; set; }
    public string Name { get; set; }
    public int Port { get; set; }
    public int Timeout { get; set; }
}

public class HealthCheckEndpoints
{
    public string IdentityServer { get; set; }
}