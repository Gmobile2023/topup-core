﻿
using GMB.Topup.Stock.Hosting.Configurations;
using Infrastructure.Logging;
using Microsoft.AspNetCore.Hosting;

[assembly: HostingStartup(typeof(ConfigureLog))]

namespace GMB.Topup.Stock.Hosting.Configurations;

public class ConfigureLog : IHostingStartup
{
    public void Configure(IWebHostBuilder builder)
    {
        builder.ConfigureServices((context, services) => { services.RegisterLogging(context.Configuration); });
    }
}