﻿using Topup.Worker.Hosting.Configurations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using ServiceStack;

[assembly: HostingStartup(typeof(ConfigureGrpc))]

namespace Topup.Worker.Hosting.Configurations;

public class ConfigureGrpc : IHostingStartup
{
    public void Configure(IWebHostBuilder builder)
    {
        builder.ConfigureServices((context, services) => { services.AddServiceStackGrpc(); })
            .ConfigureAppHost(appHost => { appHost.GetApp().UseRouting(); });
    }
}