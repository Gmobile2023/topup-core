﻿using Topup.TopupGw.Hosting.Configurations;
using Infrastructure.RedisSentinel;
using Microsoft.AspNetCore.Hosting;
using ServiceStack;
using ServiceStack.Redis;

[assembly: HostingStartup(typeof(ConfigureRedis))]

namespace Topup.TopupGw.Hosting.Configurations;

public class ConfigureRedis : IHostingStartup
{
    public void Configure(IWebHostBuilder builder)
    {
        builder.ConfigureServices((context, services) => { services.RegisterRedisSentinel(context.Configuration); })
            .ConfigureAppHost(appHost =>
            {
                appHost.GetPlugin<SharpPagesFeature>()?.ScriptMethods.Add(new RedisScripts());
            });
    }
}