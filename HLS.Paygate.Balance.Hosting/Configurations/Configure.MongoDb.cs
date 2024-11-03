using HLS.Paygate.Balance.Hosting.Configurations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDbGenericRepository;
using ServiceStack;

[assembly: HostingStartup(typeof(ConfigureMongoDb))]
namespace HLS.Paygate.Balance.Hosting.Configurations;

[Priority(-1)]
public class ConfigureMongoDb : IHostingStartup
{
    public void Configure(IWebHostBuilder builder)
    {
        builder.ConfigureServices((context, services) =>
        {
            services.AddSingleton<IMongoDbContext>(_ =>
                new MongoDbContext(context.Configuration.GetConnectionString("Mongodb"),
                    context.Configuration["ConnectionStrings:MongoDatabaseName"]));
        });
    }
}