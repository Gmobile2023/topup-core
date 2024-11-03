using HLS.Paygate.Report.Hosting.Configurations;
using Infrastructure.Logging;
using Microsoft.AspNetCore.Hosting;

[assembly: HostingStartup(typeof(ConfigureLog))]

namespace HLS.Paygate.Report.Hosting.Configurations;

public class ConfigureLog : IHostingStartup
{
    public void Configure(IWebHostBuilder builder)
    {
        builder.ConfigureServices((context, services) => { services.RegisterLogging(context.Configuration); });
    }
}