﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Funq;
using Topup.Gw.Domain.Repositories;
using Topup.Gw.Domain.Services;
using Topup.Gw.Hosting.Configurations;
using Topup.Gw.Interface.Services;
using Topup.Shared;
using Topup.Shared.AbpConnector;
using Topup.Shared.CacheManager;
using Topup.Shared.Helpers;
using Topup.Shared.UniqueIdGenerator;
using Topup.Shared.Utils;
using Infrastructure.AppVersion;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ServiceStack;
using ServiceStack.Api.OpenApi;
using ServiceStack.Redis;
using ServiceStack.Text;
using HostConfig = ServiceStack.HostConfig;

[assembly: HostingStartup(typeof(AppHost))]

namespace Topup.Gw.Hosting.Configurations;

public class AppHost : AppHostBase, IHostingStartup
{
    public AppHost() : base("Sale", typeof(TopupService).Assembly)
    {
    }

    public void Configure(IWebHostBuilder builder)
    {
        builder
            .ConfigureServices((context, services) =>
            {
                services.AddScoped<IPaygateMongoRepository, PaygateMongoRepository>();
                services.AddScoped<ISaleService, SaleService>();
                services.AddScoped<IDateTimeHelper, DateTimeHelper>();
                services.AddScoped<ICommonService, CommonService>();
                services.AddScoped<ICommonRepository, CommonRepository>();
                services.AddScoped<ITransCodeGenerator, TransCodeGenerator>();
                services.AddScoped<IPayBatchService, PayBatchService>();
                services.AddScoped<ICacheManager, CacheManager>();
                services.AddScoped<ISystemService, SystemService>();
                services.AddScoped<IValidateServiceBase, ValidateServiceBase>();
                services.AddScoped<ExternalServiceConnector>();
                services.AddTransient<AlarmAppVersion>();
                services.AddTransient<GrpcClientHepper>();
                //services.AddSingleton(c => c.Resolve<IRedisClientsManager>().GetCacheClient());
            })
            .ConfigureAppHost(appHost => { })
            .Configure((context, app) =>
            {
                app.UseAuthentication();
                // Configure ASP .NET Core App
                if (!HasInit)
                    app.UseServiceStack(new AppHost());
            });
    }

    public override void Configure(Container container)
    {
        Plugins.Add(new GrpcFeature(App));
        Plugins.Add(new OpenApiFeature());
        Config.GlobalResponseHeaders.Remove(HttpHeaders.XPoweredBy);
        Config.GlobalResponseHeaders.Add(HttpHeaders.XPoweredBy, "JustForCode");
        SetConfig(new HostConfig
        {
            DefaultContentType = MimeTypes.Json,
            DebugMode = AppSettings.Get(nameof(HostConfig.DebugMode), false),
            UseSameSiteCookies = true,
            GlobalResponseHeaders = new Dictionary<string, string>
            {
                { "Server", "nginx/1.4.7" },
                { "Vary", "Accept" },
                { "X-Powered-By", "NT_Sale" }
            },
            AdminAuthSecret = "11012233",
            EnableFeatures = Feature.All.Remove(
                Feature.Csv | Feature.Soap11 | Feature.Soap12) // | Feature.Metadata),
        });

        ConfigurePlugin<PredefinedRoutesFeature>(feature => feature.JsonApiRoute = null);

        JsConfig.Init(new Config
        {
            ExcludeTypeInfo = true
        });
        GlobalResponseFiltersAsync.Add((req, res, responseDto) =>
        {
            if (responseDto != null)
            {
                var properties = responseDto.GetType().GetProperties();
                var signature = properties.FirstOrDefault(p => p.Name.Contains("Signature"));
                var responseStatus = properties.FirstOrDefault(p => p.Name.Contains("ResponseStatus"));
                var sign = string.Empty;
                if (responseStatus?.PropertyType == typeof(ResponseStatusApi))
                {
                    var resStatus = (ResponseStatusApi)responseStatus.GetValue(responseDto, null);

                    if (resStatus != null)
                        sign = Cryptography.Sign(string.Join("|", resStatus.ErrorCode, resStatus.TransCode),
                            "NT_PrivateKey.pem");
                }
                if (signature != null) signature.SetValue(responseDto, sign, null);
            }
            return Task.CompletedTask;
        });
    }
}