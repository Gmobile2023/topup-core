using System.Collections.Generic;
using Funq;
using Topup.Common.Domain.Repositories;
using Topup.Common.Domain.Services;
using Hangfire;
using HealthChecks.UI.Client;
using Topup.Common.Hosting.Configurations;
using Topup.Common.Interface.Services;
using Topup.Common.Model.Dtos;
using Topup.Shared.CacheManager;
using Topup.Shared.ConfigDtos;
using Topup.Shared.Emailing;
using Topup.Shared.Helpers;
using Infrastructure.AppVersion;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ServiceStack;
using ServiceStack.Api.OpenApi;
using ServiceStack.Text;
using Topup.Common.Domain.Repositories;
using Topup.Common.Domain.Services;
using HostConfig = ServiceStack.HostConfig;

[assembly: HostingStartup(typeof(AppHost))]

namespace Topup.Common.Hosting.Configurations;

public class AppHost : AppHostBase, IHostingStartup
{
    public AppHost() : base("Common", typeof(CommonService).Assembly)
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
                    app.UseHealthChecksUI(config => { });
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
            WebHostUrl = AppSettings.GetString("HostConfig:Url"),
            ApiVersion = AppSettings.GetString("HostConfig:Version"),
            DefaultContentType = MimeTypes.Json,
            GlobalResponseHeaders = new Dictionary<string, string>
            {
                { "Vary", "Accept" },
                { "X-Powered-By", "JustForCode" }
            },
            EnableFeatures = Feature.All.Remove(
                Feature.Csv | Feature.Soap11 | Feature.Soap12) // | Feature.Metadata),
        });
        Plugins.Add(new OpenApiFeature());
        ConfigurePlugin<PredefinedRoutesFeature>(feature => feature.JsonApiRoute = null);
    }

}