﻿using System.Collections.Generic;
using Funq;
using HLS.Paygate.Commission.Domain.Repositories;
using HLS.Paygate.Commission.Domain.Services;
using HLS.Paygate.Commission.Hosting.Configurations;
using HLS.Paygate.Shared.AbpConnector;
using HLS.Paygate.Shared.CacheManager;
using HLS.Paygate.Shared.Helpers;
using HLS.Paygate.Shared.UniqueIdGenerator;
using Infrastructure.AppVersion;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ServiceStack;
using ServiceStack.Api.OpenApi;
using ServiceStack.Text;
using CommissionService = HLS.Paygate.Commission.Interface.Services.CommissionService;
using HostConfig = ServiceStack.HostConfig;

[assembly: HostingStartup(typeof(AppHost))]

namespace HLS.Paygate.Commission.Hosting.Configurations;

public class AppHost : AppHostBase, IHostingStartup
{
    public AppHost() : base("NT_Commission", typeof(CommissionService).Assembly)
    {
    }

    public void Configure(IWebHostBuilder builder)
    {
        builder
            .ConfigureServices(services =>
            {
                services.AddScoped<ICommissionMongoRepository, CommissionMongoRepository>();
                services.AddScoped<ExternalServiceConnector>();
                services.AddScoped<IDateTimeHelper, DateTimeHelper>();
                services.AddScoped<ICommissionRepository, CommissionRepository>();
                services.AddScoped<ICommissionService, Domain.Services.CommissionService>();
                services.AddSingleton<ICacheManager, CacheManager>();
                services.AddSingleton<ITransCodeGenerator, TransCodeGenerator>();
                services.AddTransient<GrpcClientHepper>();
                services.AddTransient<AlarmAppVersion>();
                services.AddSingleton<CacheManager>();
            })
            .ConfigureAppHost(appHost => { })
            .Configure((context, app) =>
            {
                // Configure ASP .NET Core App
                if (!HasInit)
                    app.UseServiceStack(new AppHost());
                
                var pathBase = context.Configuration["PATH_BASE"];
                if (!string.IsNullOrEmpty(pathBase)) app.UsePathBase(pathBase);

                app.UseRouting();
            });
    }
    
    public override void Configure(Container container)
    {
        SetConfig(new HostConfig
        {
            DefaultContentType = MimeTypes.Json,
            DebugMode = AppSettings.Get(nameof(HostConfig.DebugMode), false),
            UseSameSiteCookies = true,
            GlobalResponseHeaders = new Dictionary<string, string>
            {
                { "Server", "nginx/1.4.7" },
                { "Vary", "Accept" },
                { "X-Powered-By", "NT_Commission" }
            },
            EnableFeatures = Feature.All.Remove(
                Feature.Csv | Feature.Soap11 | Feature.Soap12) // | Feature.Metadata),
        });

        ConfigurePlugin<PredefinedRoutesFeature>(feature => feature.JsonApiRoute = null);
        Plugins.Add(new GrpcFeature(App));
        Plugins.Add(new OpenApiFeature());

        JsConfig.Init(new Config
        {
            ExcludeTypeInfo = true
        });
    }
}