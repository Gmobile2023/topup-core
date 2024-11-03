using System.Collections.Generic;
using Funq;
using Hangfire;
using GMB.Topup.Report.Domain.Connectors;
using GMB.Topup.Report.Domain.DataExporting.Excel.EpPlus;
using GMB.Topup.Report.Domain.Exporting;
using GMB.Topup.Report.Domain.Repositories;
using GMB.Topup.Report.Domain.Services;
using GMB.Topup.Report.Hosting.Configurations;
using GMB.Topup.Report.Interface.Services;
using GMB.Topup.Shared.CacheManager;
using GMB.Topup.Shared.Emailing;
using GMB.Topup.Shared.Helpers;
using Infrastructure.AppVersion;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ServiceStack;
using ServiceStack.Api.OpenApi;
using ServiceStack.Text;
using HostConfig = ServiceStack.HostConfig;

[assembly: HostingStartup(typeof(AppHost))]

namespace GMB.Topup.Report.Hosting.Configurations;

public class AppHost : AppHostBase, IHostingStartup
{
    public AppHost() : base("NT_Report", typeof(ReportService).Assembly)
    {
    }

    public void Configure(IWebHostBuilder builder)
    {
        builder
            .ConfigureServices(services =>
            {

                services.AddScoped<IReportMongoRepository, ReportMongoRepository>();
                services.AddScoped<IBalanceReportService, BalanceReportService>();
                services.AddScoped<ICardStockReportService, CardStockReportService>();
                services.AddScoped<ICompareService, CompareService>();
                services.AddScoped<IDateTimeHelper, DateTimeHelper>();
                services.AddScoped<WebApiConnector>();
                services.AddScoped<IEmailTemplateProvider, EmailTemplateProvider>();
                services.AddScoped<IEmailSender, EmailSender>();
                services.AddScoped<ICacheManager, CacheManager>();
                services.AddScoped<IEpPlusExcelExporterBase, EpPlusExcelExporterBase>();
                services.AddScoped<IExportDataExcel, ExportDataExcel>();
                services.AddScoped<IExportingService, ExportingService>();
                services.AddScoped<IAutoReportService, AutoReportService>();
                services.AddScoped<IElasticReportRepository, ElasticReportRepository>();
                services.AddScoped<IElasticReportService, ElasticReportService>();
                services.AddScoped<IFileUploadRepository, FileUploadRepository>();                
                services.AddTransient<AlarmAppVersion>();
                services.AddTransient<GrpcClientHepper>();
            })
            .ConfigureAppHost(appHost => { })
            .Configure((context, app) =>
            {
                // Configure ASP .NET Core App
                if (!HasInit)
                    app.UseServiceStack(new AppHost());

                //var pathBase = context.Configuration["PATH_BASE"];
                //if (!string.IsNullOrEmpty(pathBase)) app.UsePathBase(pathBase);
                //
                // app.UseRouting();
                if (bool.Parse(context.Configuration["Hangfire:EnableHangfire"]))
                {
                    var time = int.Parse(context.Configuration["Hangfire:TimeRun"]);

                    RecurringJob.AddOrUpdate<IAutoReportService>(x => x.SysJobBalanceReport(),
                        $"0 17 * * *");

                    RecurringJob.AddOrUpdate<IAutoReportService>(x => x.SysJobReport(),
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
        Plugins.Add(new GrpcFeature(App));
        Plugins.Add(new OpenApiFeature());

        JsConfig.Init(new Config
        {
            ExcludeTypeInfo = true
        });
    }
}