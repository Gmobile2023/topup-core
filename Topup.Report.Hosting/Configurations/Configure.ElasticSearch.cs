using Topup.Report.Hosting.Configurations;
using Infrastructure.ElasticSeach;
using Microsoft.AspNetCore.Hosting;

[assembly: HostingStartup(typeof(ConfigureElasticSearch))]
namespace Topup.Report.Hosting.Configurations;

public class ConfigureElasticSearch : IHostingStartup
{
    public void Configure(IWebHostBuilder builder)
    {
        builder.ConfigureServices((context, services) => { services.AddElasticsearch(context.Configuration); });
    }
}