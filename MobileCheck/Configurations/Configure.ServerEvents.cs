using Microsoft.AspNetCore.Hosting;
using MobileCheck.Configurations;
using ServiceStack;

[assembly: HostingStartup(typeof(ConfigureServerEvents))]

namespace MobileCheck.Configurations;

public class ConfigureServerEvents : IHostingStartup
{
    public void Configure(IWebHostBuilder builder) => builder
        .ConfigureServices(services => {
            services.AddPlugin(new ServerEventsFeature());
        });
}
