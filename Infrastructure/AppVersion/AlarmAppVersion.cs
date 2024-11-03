using System;
using System.Net;
using System.Reflection;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Paygate.Contracts.Commands.Commons;
using Paygate.Contracts.Requests.Commons;

namespace Infrastructure.AppVersion;

public class AlarmAppVersion
{
    private readonly IBus _bus;
    private readonly IConfiguration _configuration;

    public AlarmAppVersion(IBus bus, IConfiguration configuration)
    {
        _bus = bus;
        _configuration = configuration;
    }

    public void AlarmVersion()
    {
        var config = new HostConfig();
        _configuration.GetSection("HostConfig").Bind(config);
        var version = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        Console.WriteLine($"Starting {config.AppName} with Version:{version}");
        _bus.Publish<SendBotMessage>(new
        {
            Message =
                $"Service {config.AppName} đã được update với phiên bản:{version}",
            Module = Dns.GetHostName(),
            MessageType = BotMessageType.Message,
            Title = $"Starting {config.AppName} Service",
            BotType = BotType.Dev,
            TimeStamp = DateTime.Now,
            CorrelationId = Guid.NewGuid()
        });
    }
}