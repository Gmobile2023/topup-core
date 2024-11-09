﻿using System.Collections.Generic;
using Funq;
using GMB.Topup.Common.Domain.Repositories;
using GMB.Topup.Common.Domain.Services;
using Hangfire;
using HealthChecks.UI.Client;
using GMB.Topup.Common.Hosting.Configurations;
using GMB.Topup.Common.Interface.Services;
using GMB.Topup.Common.Model.Dtos;
using GMB.Topup.Shared.CacheManager;
using GMB.Topup.Shared.ConfigDtos;
using GMB.Topup.Shared.Emailing;
using GMB.Topup.Shared.Helpers;
using Infrastructure.AppVersion;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ServiceStack;
using ServiceStack.Api.OpenApi;
using ServiceStack.Text;
using HostConfig = ServiceStack.HostConfig;

[assembly: HostingStartup(typeof(AppHost))]

namespace GMB.Topup.Common.Hosting.Configurations;

public class AppHost : AppHostBase, IHostingStartup
{
    public AppHost() : base("NT_Common", typeof(CommonService).Assembly)
    {
    }

    public void Configure(IWebHostBuilder builder)
    {
        builder
            .ConfigureServices(services =>
            {
                services.AddScoped<ICommonMongoRepository, CommonMongoRepository>();
                services.AddScoped<IDateTimeHelper, DateTimeHelper>();
                services.AddScoped<IEmailTemplateProvider, EmailTemplateProvider>();
                services.AddScoped<IEmailSender, EmailSender>();
                services.AddSingleton<ICacheManager, CacheManager>();
                services.AddScoped<IBotMessageService, BotMessageService>();
                services.AddScoped<INotificationSevice, NotificationSevice>();
                services.AddScoped<IAuditLogService, AuditLogService>();
                services.AddScoped<ICommonAppService, CommonAppService>();
                services.AddScoped<ICmsService, CmsService>();
                services.AddTransient<AlarmAppVersion>();
                services.AddTransient<GrpcClientHepper>();
            })
            .ConfigureAppHost(appHost => { })
            .Configure((context, app) =>
            {
                // Configure ASP .NET Core App
                if (!HasInit)
                    app.UseServiceStack(new AppHost());

                // var pathBase = context.Configuration["PATH_BASE"];
                // if (!string.IsNullOrEmpty(pathBase)) app.UsePathBase(pathBase);


                var healthCheckConfig = new HealthCheckConfig();
                context.Configuration.GetSection("HealthChecks").Bind(healthCheckConfig);
                if (healthCheckConfig.HealthChecksEnabled)
                    app.UseRouting().UseEndpoints(config =>
                    {
                        config.MapHealthChecks("/health", new HealthCheckOptions
                        {
                            Predicate = _ => true,
                            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
                        });
                        config.MapHealthChecksUI();
                    });
                if (healthCheckConfig.HealthChecksEnabled && healthCheckConfig.HealthChecksUI.HealthChecksUIEnabled)
                    app.UseHealthChecksUI(config =>
                    {
                    });
                var hangfireConfig = new CommonHangFireConfig();
                context.Configuration.GetSection("Hangfire").Bind(hangfireConfig);
                //hangfire=> chỗ này bind ra object
                if (!hangfireConfig.EnableHangfire) return;
                if (hangfireConfig.IsRun)
                {
                    // if (hangfireConfig.AutoQueryBill.IsRun)
                    // {
                    //     RecurringJob.AddOrUpdate<ICommonAppService>(x => x.AutoCheckPayBill(),
                    //         hangfireConfig.AutoQueryBill.IsTest ? hangfireConfig.AutoQueryBill.CronExpressionTest : hangfireConfig.AutoQueryBill.CronExpression);
                    // }

                    if (hangfireConfig.AutoCheckMinBalance.IsRun)
                    {
                        RecurringJob.AddOrUpdate<ICommonAppService>(x => x.WarningBalance(),
                            hangfireConfig.AutoCheckMinBalance.CronExpression);
                    }
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
                { "X-Powered-By", "NT_Common" }
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