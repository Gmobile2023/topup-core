using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Filters;
using Serilog.Sinks.Elasticsearch;
using Serilog.Sinks.SystemConsole.Themes;

namespace Infrastructure.Logging;

public static class LoggingExtensions
{
    public static void RegisterLogging(this IServiceCollection services, IConfiguration configuration)
    {
        var serviceConfig = new LoggingConfigDto();
        configuration.GetSection("LoggingConfig").Bind(serviceConfig);
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            if (serviceConfig.IsDisableElk)
                builder.AddSerilog(new LoggerConfiguration()
                    .MinimumLevel.Information()
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                    .Enrich.WithProperty("Application", serviceConfig.Application + "_" + Environment.MachineName)
                    .Enrich.FromLogContext()
                    .WriteTo.File(serviceConfig.LogFileUrl, outputTemplate: serviceConfig.OutputTemplate,
                        rollingInterval: RollingInterval.Day, retainedFileCountLimit: null)
                    .WriteTo.Console(theme: AnsiConsoleTheme.Literate)
                    .Filter.ByExcluding(Matching.WithProperty<string>("Path",
                        s => s != null && (s.Equals("/json/reply/Heartbeat") ||
                                           s.Equals("/json/reply/HealthCheck") ||
                                           s.Equals("/ping") || s.Equals("/hangfire/stats") || s.Equals("Statistics"))))
                    .Filter.ByExcluding(e =>
                        e.MessageTemplate.Text.Contains("Statistics") ||
                        e.MessageTemplate.Text.Contains("has completed with status") ||
                        e.MessageTemplate.Text.Contains("App.Requests.Latency.Average"))
                    .CreateLogger(), true);
            else
                builder.AddSerilog(new LoggerConfiguration()
                    .MinimumLevel.Information()
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                    .Enrich.WithProperty("Application", serviceConfig.Application + "_" + Environment.MachineName)
                    .Enrich.FromLogContext()
                    .WriteTo.File(serviceConfig.LogFileUrl, outputTemplate: serviceConfig.OutputTemplate,
                        rollingInterval: RollingInterval.Day, retainedFileCountLimit: null)
                    .WriteTo.Console(theme: AnsiConsoleTheme.Literate)
                    .Filter.ByExcluding(Matching.WithProperty<string>("Path",
                        s => s != null && (s.Equals("/json/reply/Heartbeat") ||
                                           s.Equals("/json/reply/HealthCheck") ||
                                           s.Equals("/ping") || s.Equals("/hangfire/stats") || s.Equals("Statistics"))))
                    .Filter.ByExcluding(e =>
                        e.MessageTemplate.Text.Contains("Statistics") ||
                        e.MessageTemplate.Text.Contains("has completed with status") ||
                        e.MessageTemplate.Text.Contains("App.Requests.Latency.Average"))
                    .WriteTo.Elasticsearch(
                        new ElasticsearchSinkOptions(new Uri(serviceConfig.LogServer))
                        {
                            AutoRegisterTemplate = serviceConfig.AutoRegisterTemplate,
                            AutoRegisterTemplateVersion = AutoRegisterTemplateVersion.ESv6,
                            IndexFormat = serviceConfig.IndexFormat,
                            ModifyConnectionSettings = x =>
                                x.BasicAuthentication(serviceConfig.UserName, serviceConfig.Password)
                        })
                    .CreateLogger(), true);
        });
    }
}