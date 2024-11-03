using Infrastructure.ServiceDiscovery;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ServiceStack;

namespace HLS.Paygate.TopupGw.Hosting.Configurations;

public class ConfigureConsul : IConfigureServices
{
    public ConfigureConsul(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    private IConfiguration Configuration { get; }

    public void Configure(IServiceCollection services)
    {
        var serviceConfig = Configuration.GetConsulConfig();
        if (serviceConfig.IsUseConsul) services.RegisterConsulServices(serviceConfig);
    }
}