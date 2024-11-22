using HLS.Paygate.Report.Domain.Connectors;
using HLS.Paygate.Report.Domain.DataExporting.Excel.EpPlus;
using HLS.Paygate.Report.Domain.Exporting;
using HLS.Paygate.Report.Domain.Repositories;
using HLS.Paygate.Report.Domain.Services;
using HLS.Paygate.Shared.CacheManager;
using HLS.Paygate.Shared.Emailing;
using HLS.Paygate.Shared.Helpers;
using Infrastructure.AppVersion;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using ServiceStack;

namespace HLS.Paygate.Report.Hosting;

public class Startup : ModularStartup
{
    public Startup(IConfiguration configuration) : base(configuration)
    {
        Configuration = configuration;
    }

    public new void ConfigureServices(IServiceCollection services)
    {
        //services.AddLogging(loggingBuilder =>
        //{
        // configure Logging with NLog
        //loggingBuilder.ClearProviders();
        //loggingBuilder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Debug);
        //loggingBuilder.AddNLog();

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
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory,
        AlarmAppVersion version)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler("/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
            version.AlarmVersion();
        }

        loggerFactory.AddSerilog();
        //loggerFactory.AddProvider(new NLog.Extensions.Logging.NLogLoggerProvider());
        var apphost = new AppHost(Configuration)
        {
            AppSettings = new NetCoreAppSettings(Configuration)
        };
        app.UseServiceStack(apphost);
    }
}