using System;
using HLS.Paygate.Shared.ConfigDtos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ServiceStack.Logging;
using ServiceStack.Redis;

namespace Infrastructure.RedisSentinel;

public static class RedisSentinelExtensions
{
    // private static readonly ILog Log = LogManager.GetLogger(typeof(RedisSentinelExtensions));
    public static void RegisterRedisSentinel(this IServiceCollection services, IConfiguration configuration)
    {
        var serviceConfig = new RedisSentinelConfig();
        configuration.GetSection("RedisConfig").Bind(serviceConfig);

        if (!serviceConfig.IsEnable) return;
        if (serviceConfig.IsSentinel)
        {
            //var sentinel = new ServiceStack.Redis.RedisSentinel(serviceConfig.SentinelHosts, masterName: serviceConfig.MasterName);
            var sentinel =
                new ServiceStack.Redis.RedisSentinel(serviceConfig.SentinelHosts, masterName: serviceConfig.MasterName)
                {
                    RedisManagerFactory = (master, slaves) => new RedisManagerPool(master),
                    OnFailover = manager => { Console.WriteLine("Redis Managers were Failed Over to new hosts"); },
                    OnWorkerError = ex => { Console.WriteLine($"Worker error:{ex}"); },
                    OnSentinelMessageReceived = (channel, msg) =>
                    {
                        Console.WriteLine($"Received {channel} on channel {msg} from Sentinel");
                    },
                    ScanForOtherSentinels = true,
                    RefreshSentinelHostsAfter = TimeSpan.FromMinutes(1)
                };
            services.AddSingleton(c => sentinel.Start());
        }
        else
        {
            services.AddSingleton<IRedisClientsManager>(c => new RedisManagerPool(serviceConfig.SentinelHosts));
        }
    }
}