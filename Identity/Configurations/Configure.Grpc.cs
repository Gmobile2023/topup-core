using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Identity.Configurations;
using ServiceStack;

[assembly: HostingStartup(typeof(ConfigureGrpc))]

namespace Identity.Configurations;

public class ConfigureGrpc : IHostingStartup
{
    public void Configure(IWebHostBuilder builder)
    {
        builder.ConfigureServices((context, services) => { services.AddServiceStackGrpc(); })
            .ConfigureAppHost(appHost => { appHost.GetApp().UseRouting(); });
    }
}