﻿using System.Collections.Generic;
using Funq;
using Hangfire;
using HLS.Paygate.Gw.Hosting.Configurations;
using HLS.Paygate.Kpp.Domain.DataExporting.Excel.EpPlus;
using HLS.Paygate.Kpp.Domain.Exporting;
using HLS.Paygate.Kpp.Domain.Repositories;
using HLS.Paygate.Kpp.Domain.Services;
using HLS.Paygate.Kpp.Interface.Services;
using HLS.Paygate.Shared.CacheManager;
using HLS.Paygate.Shared.Emailing;
using HLS.Paygate.Shared.Helpers;
using Infrastructure.AppVersion;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ServiceStack;
using ServiceStack.Api.OpenApi;
using ServiceStack.Text;
using HostConfig = ServiceStack.HostConfig;

[assembly: HostingStartup(typeof(AppHost))]

namespace HLS.Paygate.Gw.Hosting.Configurations;

public class AppHost : AppHostBase, IHostingStartup
{
    public AppHost() : base("KppService", typeof(KppService).Assembly)
    {
    }

    public void Configure(IWebHostBuilder builder)
    {
        builder
            .ConfigureServices(services =>
            {                
                services.AddScoped<IDateTimeHelper, DateTimeHelper>();           
                services.AddScoped<IEmailTemplateProvider, EmailTemplateProvider>();
                services.AddScoped<IEmailSender, EmailSender>();
                services.AddScoped<ICacheManager, CacheManager>();
                services.AddScoped<IKppMongoRepository, KppMongoRepository>();
                services.AddScoped<IKppPosgreRepository, KppPosgreRepository>();
                services.AddScoped<IExportingService, ExportingService>();
                services.AddScoped<IExportDataExcel, ExportDataExcel>();
                services.AddScoped<IEpPlusExcelExporterBase, EpPlusExcelExporterBase>();
                services.AddScoped<IAutoKppService, AutoKppService>();
                //services.AddTransient<AlarmAppVersion>();
                //services.AddTransient<GrpcClientHepper>();
            })
            .ConfigureAppHost(appHost => { })
            .Configure((context, app) =>
           {              
                if (!HasInit)
                    app.UseServiceStack(new AppHost());
                
                if (bool.Parse(context.Configuration["Hangfire:EnableHangfire"]))
                {
                    var time = int.Parse(context.Configuration["Hangfire:TimeRun"]);

                    RecurringJob.AddOrUpdate<IAutoKppService>(x => x.SysAutoFile(),
                        $"0 {time + 1} * * *");
                    app.UseHangfireDashboard();
                }
            });
    }

    public override void Configure(Container container)
    {
        SetConfig(new HostConfig
        {
            DefaultContentType = MimeTypes.Json,
            DebugMode = true,// AppSettings.Get(nameof(HostConfig.DebugMode), false),
            StrictMode = true,
            UseSameSiteCookies = true,
            GlobalResponseHeaders = new Dictionary<string, string>
            {
                { "Server", "nginx/1.4.7" },
                { "Vary", "Accept" },
                { "X-Powered-By", "NT_Report" }
            },
            EnableFeatures = Feature.All.Remove(
                Feature.Csv | Feature.Soap11 | Feature.Soap12)//| Feature.Metadata)
        });

        ConfigurePlugin<PredefinedRoutesFeature>(feature => feature.JsonApiRoute = null);
        //Plugins.Add(new GrpcFeature(App));
        Plugins.Add(new OpenApiFeature());

        JsConfig.Init(new Config
        {
            ExcludeTypeInfo = true
        });
    }
}