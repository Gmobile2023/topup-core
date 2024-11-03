using System;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.RedisSentinel;

public static class SentinelConfigExtensions
{
    public static SentinelConfigDto GetRedisConfig(this IConfiguration configuration)
    {
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));

        var serviceConfig = new SentinelConfigDto
        {
            Server = configuration["RedisConfig:Address"],
            MasterName = configuration["RedisConfig:MasterName"],
            RedisServer = configuration["RedisConfig:RedisServer"]
        };
        return serviceConfig;
    }
}