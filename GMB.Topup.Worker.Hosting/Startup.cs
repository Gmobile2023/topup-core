using HLS.Paygate.Gw.Domain.Repositories;
using HLS.Paygate.Gw.Domain.Services;
using HLS.Paygate.Shared.AbpConnector;
using HLS.Paygate.Shared.CacheManager;
using HLS.Paygate.Shared.Helpers;
using HLS.Paygate.Shared.UniqueIdGenerator;
using HLS.Paygate.Worker.Components.Connectors;
using HLS.Paygate.Worker.Components.TaskQueues;
using HLS.Paygate.Worker.Components.WorkerProcess;
using Infrastructure.AppVersion;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using ServiceStack;

// using HLS.Paygate.Worker.Components.BackgroundJobs;
// using HLS.Paygate.Worker.Components.Processes;

namespace HLS.Paygate.Worker.Hosting;

public class Startup : ModularStartup
{
    // public Startup(IConfiguration configuration) : base(configuration)
    // {
    //     Configuration = configuration;
    // }

    // This method gets called by the runtime. Use this method to add services to the container.
    // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
    public new void ConfigureServices(IServiceCollection services)
    {
        // services.Add(ServiceDescriptor.Singleton(Configuration));
        IocContainerRegistration(services);
        //services.AddHostedService<QueuedHostedService2>();
        //services.AddSingleton<IBackgroundTaskQueue2>(_ => new BackgroundTaskQueue2());
        services.AddHostedService<QueuedHostedService>();
        services.AddSingleton<IBackgroundTaskQueue>(_ => new BackgroundTaskQueue(1000));
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, ILoggerFactory loggerFactory, IWebHostEnvironment env,
        AlarmAppVersion version)
    {
        // var connectionFactory = serviceProvider.GetService<IPaygateConnectionFactory>();
        // using (var db = connectionFactory.Open())
        // {
        //     OrmLiteConfig.DialectProvider.NamingStrategy = new OrmLiteNamingStrategyBase();
        //     OrmLiteConfig.DialectProvider.GetStringConverter().UseUnicode = true;
        // }
        if (!env.IsDevelopment()) version.AlarmVersion();
        var appHost = new AppHost(Configuration)
        {
            AppSettings = new NetCoreAppSettings(Configuration)
        };
        loggerFactory.AddSerilog();
        //loggerFactory.AddNLog();
        app.UseServiceStack(appHost);

        var pathBase = Configuration["PATH_BASE"];
        if (!string.IsNullOrEmpty(pathBase)) app.UsePathBase(pathBase);
    }


    private void IocContainerRegistration(IServiceCollection services)
    {
        // var sentinelHosts =
        //     Configuration["RedisConfig:Address"]
        //         .Split(',', ';', '|');
        // var sentinel = new RedisSentinel(sentinelHosts, Configuration["RedisConfig:MasterName"])
        // {
        //     RedisManagerFactory = (master, slaves) => new RedisManagerPool(master)
        // };

        //services.AddSingleton(x => sentinel.Start());

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
        services.AddTransient<AlarmAppVersion>();
        services.AddTransient<GrpcClientHepper>(); 
    }
}

public static class CustomExtensionMethods
{
    public static IServiceCollection AddCustomHealthCheck(this IServiceCollection services,
        IConfiguration configuration)
    {
        var hcBuilder = services.AddHealthChecks();

        hcBuilder.AddCheck("self", () => HealthCheckResult.Healthy());

        return services;
    }
}