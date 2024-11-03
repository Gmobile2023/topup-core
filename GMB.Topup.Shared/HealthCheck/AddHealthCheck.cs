using System;
using System.Linq;
using GMB.Topup.Shared.ConfigDtos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GMB.Topup.Shared.HealthCheck;

public static class AddHealthCheck
{
    public static IHealthChecksBuilder AddPaygateHealthCheck(this IServiceCollection services,
        HealthCheckConfig configuration)
    {
        var builder = services.AddHealthChecks();
        //builder.AddCheck<RedisHeathCheck>("redis", tags: new string[] {"redis"});
        //builder.AddCheck<RabbitMqHeathCheck>("RabbitMq", tags: new string[] {"rabbitmq"});
        // builder.AddRedis(
        //     configuration["RedisConfig:Address"],
        //     name: "redis",
        //     tags: new string[] {"redis"});
        //builder.AddRabbitMQ(
        //    $"amqp://{configuration["RabbitMq:Username"]}:{configuration["RabbitMq:Password"]}@{configuration["RabbitMq:Host"]}/{configuration["RabbitMq:VirtualHost"]}",
        //    name: "rabbitMq",
        //    tags: new string[] {"rabbitmq"});
        //builder.AddMongoDb(configuration["ConnectionStrings:Mongodb"],
        //    name: "mongoDb",
        //    tags: new string[] {"mongodb"});

        //var uri = new Uri(configuration["ConsulConfig:ServiceDiscoveryAddress"]);
        //builder.AddConsul(options =>
        //    {
        //        options.HostName = uri.Host;
        //        options.Port = uri.Port;
        //    },
        //    name: "consul",
        //    tags: new string[] {"consul"});
        builder.AddIdentityServer(new Uri(configuration.CheckEndpoints.IdentityServer),
            name: "IdentityServer",
            tags: new string[] { "authen" });

        //builder.AddElasticsearch(configuration["LoggingConfig:Url"],
        //    name: "elastic",
        //    tags: new string[] {"elk"});

        if (!configuration.ServiceChecks.Any()) return builder;
        foreach (var item in configuration.ServiceChecks)
        {
            var timeout = item.Timeout > 0 ? item.Timeout : 10;
            builder.AddTcpHealthCheck(options => { options.AddHost(item.Host, item.Port); },
                timeout: TimeSpan.FromSeconds(timeout),
                name: item.Name);
        }

        return builder;
    }
}