using System.Collections.Generic;
using Funq;
using GMB.Topup.Gw.Domain.Repositories;
using GMB.Topup.Gw.Domain.Services;
using GMB.Topup.Shared.AbpConnector;
using GMB.Topup.Shared.CacheManager;
using GMB.Topup.Shared.Helpers;
using GMB.Topup.Shared.UniqueIdGenerator;
using GMB.Topup.Worker.Components;
using GMB.Topup.Worker.Components.Connectors;
using GMB.Topup.Worker.Components.TaskQueues;
using GMB.Topup.Worker.Components.WorkerProcess;
using GMB.Topup.Worker.Hosting.Configurations;
using Infrastructure.AppVersion;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ServiceStack;
using ServiceStack.Api.OpenApi;
using ServiceStack.Redis;
using ServiceStack.Text;
using ServiceStack.Validation;
using HostConfig = ServiceStack.HostConfig;

[assembly: HostingStartup(typeof(AppHost))]

namespace GMB.Topup.Worker.Hosting.Configurations;

public class AppHost : AppHostBase, IHostingStartup
{
    public AppHost() : base("NT_Worker", typeof(WorkerService).Assembly)
    {
    }
    
    public void Configure(IWebHostBuilder builder)
    {
        builder
            .ConfigureServices(services =>
            {
                services.AddOptions();
                services.AddScoped<IPaygateMongoRepository, PaygateMongoRepository>();
                services.AddScoped<ICommonRepository, CommonRepository>();
                services.AddScoped<ICommonService, CommonService>();
                services.AddScoped<ISaleService, SaleService>();
                services.AddScoped<ITransactionService, TransactionService>();
                services.AddScoped<ILimitTransAccountService, LimitTransAccountService>();
                services.AddScoped<ITransCodeGenerator, TransCodeGenerator>();
                services.AddScoped<IDateTimeHelper, DateTimeHelper>();
                services.AddScoped<ExternalServiceConnector>();
                services.AddScoped<TelcoConnector>();
                services.AddScoped<CheckLimitTransaction>();
                services.AddScoped<IWorkerProcess, WorkerProcess>();
                services.AddScoped<ICacheManager, CacheManager>();
                services.AddScoped<ISystemService, SystemService>();
                services.AddTransient<AlarmAppVersion>();
                services.AddTransient<GrpcClientHepper>();
                services.AddHostedService<QueuedHostedService>();
                services.AddSingleton<IBackgroundTaskQueue>(_ => new BackgroundTaskQueue(1000));
                services.AddSingleton(c => c.Resolve<IRedisClientsManager>().GetCacheClient());
            })
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
                { "X-Powered-By", "NT_TopupGw" }
            },
            EnableFeatures = Feature.All.Remove(
                Feature.Csv | Feature.Soap11 | Feature.Soap12) // | Feature.Metadata),
        });

        ConfigurePlugin<PredefinedRoutesFeature>(feature => feature.JsonApiRoute = null);
        Plugins.Add(new GrpcFeature(App));
        Plugins.Add(new OpenApiFeature());
        Plugins.Add(new ValidationFeature());

        JsConfig.Init(new Config
        {
            ExcludeTypeInfo = true
        });
    }
}