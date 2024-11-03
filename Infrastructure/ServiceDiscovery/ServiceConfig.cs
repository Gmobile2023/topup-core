using System;

namespace Infrastructure.ServiceDiscovery;

public class ServiceConfig
{
    public Uri ServiceDiscoveryAddress { get; set; }
    public Uri ServiceAddress { get; set; }
    public string ServiceName { get; set; }
    public string ServiceId { get; set; }
    public bool IsUseConsul { get; set; }
    public bool PingEnabled { get; set; }
    public string PingEndpoint { get; set; }
    public int PingInterval { get; set; }
    public int RemoveAfterInterval { get; set; }
    public int RequestRetry { get; set; }
}