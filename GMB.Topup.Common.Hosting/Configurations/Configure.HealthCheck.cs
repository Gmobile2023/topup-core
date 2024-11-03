using System.Linq;
using HealthChecks.UI.Core;
using GMB.Topup.Common.Hosting.Configurations;
using GMB.Topup.Shared.ConfigDtos;
using GMB.Topup.Shared.HealthCheck;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

[assembly: HostingStartup(typeof(HealthCheck))]

namespace GMB.Topup.Common.Hosting.Configurations;

public class HealthCheck : IHostingStartup
{
    public void Configure(IWebHostBuilder builder)
    {
        builder.ConfigureServices((context, services) =>
        {
            var config = new HealthCheckConfig();
            context.Configuration.GetSection("HealthChecks").Bind(config);
            var healthCheckUiSection = context.Configuration.GetSection("HealthChecks")?.GetSection("HealthChecksUI");
            services.Configure<HealthChecksUIBuilder>(settings =>
            {
                healthCheckUiSection.Bind(settings, c => c.BindNonPublicProperties = true);
            });
            if (config.HealthChecksUI.IsCheckService)
                services.AddPaygateHealthCheck(config);

            if (config.HealthChecksEnabled && config.HealthChecksUI.HealthChecksUIEnabled)
                services.AddHealthChecksUI(setup =>
                {
                    //Set the maximum history entries by endpoint that will be served by the UI api middleware
                    setup.MaximumHistoryEntriesPerEndpoint(50);
                    setup.AddHealthCheckEndpoint("Nhất Trần", $"{config.Url}/health");
                    setup.AddWebhookNotification("health-check-notifi",
                        $"{config.Url}/health-check-notifi",
                        "{ \"message\": \"Cảnh báo service [[LIVENESS]]\n[[FAILURE]]\nThông tin chi tiết:\n[[DESCRIPTIONS]]\"}",
                        "{ \"message\": \"[[LIVENESS]] is back to life\"}",
                        customMessageFunc: (s, report) =>
                        {
                            var failing = report.Entries.Where(e => e.Value.Status == UIHealthStatus.Unhealthy);
                            return $"{failing.Count()} healthchecks are failing";
                        }, customDescriptionFunc: (s, report) =>
                        {
                            var message = string.Empty;
                            var failing = report.Entries.Where(e => e.Value.Status == UIHealthStatus.Unhealthy)
                                .ToList();
                            var index = 0;
                            foreach (var item in failing)
                            {
                                index ++;
                                message += $"{index}. {item.Key}:  {item.Value.Description}\n";
                            }
                            return message;
                        }
                    );
                    setup.SetMinimumSecondsBetweenFailureNotifications(config.HealthChecksUI
                        .MinimumSecondsBetweenFailureNotifications);
                    setup.SetEvaluationTimeInSeconds(config.HealthChecksUI.EvaluationTimeOnSeconds);
                }).AddInMemoryStorage();
        });
    }
}