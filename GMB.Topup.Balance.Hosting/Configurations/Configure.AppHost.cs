using System.Collections.Generic;
using Funq;
using GMB.Topup.Balance.Components.Services;
using GMB.Topup.Balance.Domain.Repositories;
using GMB.Topup.Balance.Domain.Services;
using GMB.Topup.Balance.Hosting.Configurations;
using GMB.Topup.Shared.CacheManager;
using GMB.Topup.Shared.Helpers;
using GMB.Topup.Shared.UniqueIdGenerator;
using Infrastructure.AppVersion;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ServiceStack;
using ServiceStack.Api.OpenApi;
using ServiceStack.Caching;
using ServiceStack.Text;
using HostConfig = ServiceStack.HostConfig;

[assembly: HostingStartup(typeof(AppHost))]

namespace GMB.Topup.Balance.Hosting.Configurations;

public class AppHost : AppHostBase, IHostingStartup
{
    public AppHost() : base("NT_Balance", typeof(MainService).Assembly)
    {
    }

    public void Configure(IWebHostBuilder builder)
    {
        builder
            .ConfigureServices(services =>
            {
                services.AddTransient<ITransCodeGenerator, TransCodeGenerator>();
                services.AddTransient<MainService>();
                services.AddTransient<IBalanceService, BalanceService>();
                services.AddTransient<ITransactionService, TransactionService>();
                services.AddTransient<IBalanceMongoRepository, BalanceMongoRepository>();
                //services.AddScoped<ITransactionReportService, TransactionReportService>();
                services.AddTransient<ICacheManager, CacheManager>();
                services.AddTransient<AlarmAppVersion>();
                services.AddTransient<GrpcClientHepper>();
            })
            .ConfigureAppHost(appHost => { })
            .Configure(app =>
            {
                // Configure ASP .NET Core App
                if (!HasInit)
                    app.UseServiceStack(new AppHost());
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
                { "X-Powered-By", "NT_Balance" }
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