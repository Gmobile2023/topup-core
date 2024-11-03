using System;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.ServiceDiscovery;

public static class ServiceConfigExtensions
{
    public static ServiceConfig GetConsulConfig(this IConfiguration configuration)
    {
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));

        var serviceConfig = new ServiceConfig
        {
            ServiceDiscoveryAddress = new Uri(configuration["ConsulConfig:ServiceDiscoveryAddress"]),
            ServiceAddress = new Uri(configuration["ConsulConfig:ServiceAddress"]),
            ServiceName = configuration["ConsulConfig:ServiceName"],
            ServiceId = configuration["ConsulConfig:ServiceId"],
            IsUseConsul = bool.Parse(configuration["ConsulConfig:IsUseConsul"]),
            PingEnabled = bool.Parse(configuration["ConsulConfig:PingEnabled"]),
            PingEndpoint = configuration["ConsulConfig:PingEndpoint"],
            PingInterval = int.Parse(configuration["ConsulConfig:PingInterval"]),
            RemoveAfterInterval = int.Parse(configuration["ConsulConfig:RemoveAfterInterval"]),
            RequestRetry = int.Parse(configuration["ConsulConfig:RequestRetry"])
        };
        return serviceConfig;
    }
}