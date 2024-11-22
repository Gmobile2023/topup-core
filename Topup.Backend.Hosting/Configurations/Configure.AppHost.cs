using System.Collections.Generic;
using Funq;
using Hangfire;
using Topup.Backend.Hosting.Configurations;
using Topup.Backend.Interface.Services;
using Topup.Gw.Domain.Repositories;
using Topup.Gw.Domain.Services;
using Topup.Shared.CacheManager;
using Topup.Shared.ConfigDtos;
using Topup.Shared.Helpers;
using Topup.Shared.UniqueIdGenerator;
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

namespace Topup.Backend.Hosting.Configurations;

public class AppHost : AppHostBase, IHostingStartup
{
    public AppHost() : base("Backend", typeof(BackendService).Assembly)
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
            WebHostUrl = AppSettings.GetString("HostConfig:Url"),
            ApiVersion = AppSettings.GetString("HostConfig:Version"),
            DefaultContentType = MimeTypes.Json,
            GlobalResponseHeaders = new Dictionary<string, string>
            {
                {"Vary", "Accept"},
                {"X-Powered-By", "JustForCode"}
            },
            EnableFeatures = Feature.All.Remove(
                Feature.Csv | Feature.Soap11 | Feature.Soap12) // | Feature.Metadata),
        });
        Plugins.Add(new OpenApiFeature());
        ConfigurePlugin<PredefinedRoutesFeature>(feature => feature.JsonApiRoute = null);

        JsConfig.Init(new Config
        {
            ExcludeTypeInfo = true
        });
    }
}