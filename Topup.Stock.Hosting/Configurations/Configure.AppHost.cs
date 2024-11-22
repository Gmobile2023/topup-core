using System.Collections.Generic;
using Funq;
using Topup.Shared.CacheManager;
using Topup.Shared.Helpers;
using Topup.Stock.Components.ApiServices;
using Topup.Stock.Components.StockProcess;
using Topup.Stock.Domains.BusinessServices;
using Topup.Stock.Domains.Repositories;
using Topup.Stock.Hosting.Configurations;
using Infrastructure.AppVersion;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ServiceStack;
using ServiceStack.Api.OpenApi;
using ServiceStack.Text;
using HostConfig = ServiceStack.HostConfig;

[assembly: HostingStartup(typeof(AppHost))]

namespace Topup.Stock.Hosting.Configurations;

public class AppHost : AppHostBase, IHostingStartup
{
    public AppHost() : base("Stock", typeof(MainService).Assembly)
    {
    }

    public void Configure(IWebHostBuilder builder)
    {
        builder
            .ConfigureServices(services =>
            {
                services.AddScoped<ICardService, CardService>();
                services.AddScoped<IStockAirtimeBusinessServices, StockAirtimeBusinessServices>();
                services.AddScoped<ICardStockService, CardStockService>();
                services.AddScoped<ICardMongoRepository, CardMongoRepository>();
                services.AddScoped<IDateTimeHelper, DateTimeHelper>();
                services.AddScoped<ICacheManager, CacheManager>();
                services.AddScoped<IStockProcess, StockProcess>();
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
                { "X-Powered-By", "NT_Stock" }
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