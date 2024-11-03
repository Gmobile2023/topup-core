using System.Collections.Generic;
using Hangfire;
using GMB.Topup.Shared.Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using MobileCheck.Services;
using MobileCheck.Configurations;
using ServiceStack;
using ServiceStack.Api.OpenApi;
using ServiceStack.Text;
using ServiceStack.Validation;

[assembly: HostingStartup(typeof(AppHost))]

namespace MobileCheck.Configurations;

public class AppHost : AppHostBase, IHostingStartup
{
    public AppHost() : base("MobileCheck", typeof(MainService).Assembly)
    {
    }

    public void Configure(IWebHostBuilder builder)
    {
        builder
            .ConfigureServices(services =>
            {
                // Configure ASP.NET Core IOC Dependencies
                services.AddTransient<CheckMobileHttpClient>();
                services.AddTransient<GrpcClientHepper>();
                services.AddTransient<CheckMobileProcess>();
            })
            .Configure((context, app) =>
            {
                // Configure ASP .NET Core App
                if (!HasInit)
                    app.UseServiceStack(new AppHost());

                var pathBase = context.Configuration["PATH_BASE"];
                if (!string.IsNullOrEmpty(pathBase)) app.UsePathBase(pathBase);

                app.UseRouting();
                RecurringJob.AddOrUpdate<CheckMobileProcess>("MyAutoCheck", x => x.CheckMobileJob(),
                    Cron.Minutely());
            });
    }


    public override void Configure()
    {
        // Configure ServiceStack, Run custom logic after ASP.NET Core Startup
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

    // private static IAsyncPolicy<HttpResponseMessage> GetPolicy()
    // {
    //     return HttpPolicyExtensions
    //         .HandleTransientHttpError()
    //         .OrResult(msg => msg.StatusCode == HttpStatusCode.NotFound)
    //         .WaitAndRetryForeverAsync(_ => TimeSpan.FromSeconds(3));
    // }
}