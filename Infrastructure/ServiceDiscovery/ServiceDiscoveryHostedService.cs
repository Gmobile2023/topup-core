using System;
using System.Threading;
using System.Threading.Tasks;
using Consul;
using Microsoft.Extensions.Hosting;

namespace Infrastructure.ServiceDiscovery;

public class ServiceDiscoveryHostedService : IHostedService
{
    private readonly IConsulClient _client;
    private readonly ServiceConfig _config;
    private string _registrationId;

    public ServiceDiscoveryHostedService(IConsulClient client, ServiceConfig config)
    {
        _client = client;
        _config = config;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _registrationId = $"{_config.ServiceName}-{Guid.NewGuid()}";

        var registration = new AgentServiceRegistration
        {
            ID = _registrationId,
            Name = _config.ServiceName,
            Address = _config.ServiceAddress.Host,
            Port = _config.ServiceAddress.Port
            //Tags = new[] { $"urlprefix-/{_config.ServiceName}" }
        };
        if (_config.PingEnabled)
        {
            var httpCheck = new AgentServiceCheck
            {
                Interval = TimeSpan.FromSeconds(_config.PingInterval),
                DeregisterCriticalServiceAfter = TimeSpan.FromSeconds(_config.RemoveAfterInterval),
                HTTP =
                    $"{_config.ServiceAddress.Scheme}://{_config.ServiceAddress.Host}:{_config.ServiceAddress.Port}{_config.PingEndpoint}"
                //Timeout = TimeSpan.FromSeconds(5)
            };
            registration.Checks = new[] {httpCheck};
        }

        await _client.Agent.ServiceDeregister(registration.ID, cancellationToken);
        await _client.Agent.ServiceRegister(registration, cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _client.Agent.ServiceDeregister(_registrationId, cancellationToken);
    }
}