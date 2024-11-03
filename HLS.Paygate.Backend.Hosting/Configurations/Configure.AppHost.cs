using System.Collections.Generic;
using Funq;
using Hangfire;
using HLS.Paygate.Backend.Hosting.Configurations;
using HLS.Paygate.Backend.Interface.Services;
using HLS.Paygate.Gw.Domain.Repositories;
using HLS.Paygate.Gw.Domain.Services;
using HLS.Paygate.Shared.CacheManager;
using HLS.Paygate.Shared.ConfigDtos;
using HLS.Paygate.Shared.Helpers;
using HLS.Paygate.Shared.UniqueIdGenerator;
using Infrastructure.AppVersion;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ServiceStack;
using ServiceStack.Api.OpenApi;
using ServiceStack.Text;
using HostConfig = ServiceStack.HostConfig;

[assembly: HostingStartup(typeof(AppHost))]

namespace HLS.Paygate.Backend.Hosting.Configurations;

public class AppHost : AppHostBase, IHostingStartup
{
    public AppHost() : base("NT_Backend", typeof(BackendService).Assembly)
    {
    }

    public void Configure(IWebHostBuilder builder)
    {
        builder
            .ConfigureServices(services =>
            {
                services.AddScoped<IPaygateMongoRepository, PaygateMongoRepository>();
                services.AddScoped<ISaleService, SaleService>();
                services.AddScoped<ICommonService, CommonService>();
                services.AddScoped<ITransactionService, TransactionService>();
                services.AddScoped<IDateTimeHelper, DateTimeHelper>();
                services.AddScoped<ILimitTransAccountService, LimitTransAccountService>();
                services.AddScoped<ITransCodeGenerator, TransCodeGenerator>();
                services.AddScoped<IBackgroundService, BackgroundService>();
                services.AddScoped<ICacheManager, CacheManager>();
                services.AddScoped<ISystemService, SystemService>();
                services.AddTransient<GrpcClientHepper>();
                services.AddTransient<BackendService>();
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

                var config = new BackendHangFireConfig();
                context.Configuration.GetSection("Hangfire").Bind(config);
                if (!config.EnableHangfire) return;
                if (config.IsRun)
                {
                    if (config.AutoCheckTrans.IsRun)
                    // */3 8-23 * * *
                    {
                        RecurringJob.AddOrUpdate<IBackgroundService>(x => x.AutoCheckTrans(),
                            config.AutoCheckTrans.CronExpression);

                        RecurringJob.AddOrUpdate<IBackgroundService>(x => x.AutoCheckGateTrans(),
                            config.AutoCheckTrans.CronExpression);
                    }

                    if (config.CheckLastTrans.IsRun)
                        // */3 8-23 * * *
                        RecurringJob.AddOrUpdate<IBackgroundService>(x => x.CheckLastTrans(),
                            config.CheckLastTrans.CronExpression);
                }
                app.UseHangfireDashboard();
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
                { "X-Powered-By", "NT_Backend" }
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