using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using MobileCheck.Configurations;
using ServiceStack;

[assembly: HostingStartup(typeof(ConfigureProfiling))]

namespace MobileCheck.Configurations;

public class ConfigureProfiling : IHostingStartup
{
    public void Configure(IWebHostBuilder builder) => builder
        .ConfigureServices((context, services) => {
            if (context.HostingEnvironment.IsDevelopment())
            {
                services.AddPlugin(new RequestLogsFeature
                {
                    EnableResponseTracking = true,
                });

                services.AddPlugin(new ProfilingFeature
                {
                    IncludeStackTrace = true,
                });
            }
        });
}
