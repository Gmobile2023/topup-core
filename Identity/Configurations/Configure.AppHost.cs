using System.Collections.Generic;
using Identity.BussinessService;
using Identity.Configurations;
using Identity.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ServiceStack;
using ServiceStack.Api.OpenApi;
using ServiceStack.Text;
using ServiceStack.Validation;
using Topup.Shared.Helpers;

[assembly: HostingStartup(typeof(AppHost))]

namespace Identity.Configurations;

public class AppHost : AppHostBase, IHostingStartup
{
    public AppHost() : base("Identity", typeof(MainService).Assembly)
    {
    }

    public void Configure(IWebHostBuilder builder)
    {
        builder
            .ConfigureServices(services =>
            {
                // Configure ASP.NET Core IOC Dependencies
                services.AddTransient<GrpcClientHepper>();
                services.AddScoped<IIdentityService, IdentityService>();
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
                { "X-Powered-By", "GMB" }
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