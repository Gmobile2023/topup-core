using HLS.Paygate.TopupGw.Hosting.Configurations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using MongoDbGenericRepository;

[assembly: HostingStartup(typeof(ConfigureMongoDb))]

namespace HLS.Paygate.TopupGw.Hosting.Configurations;

public class ConfigureMongoDb : IHostingStartup
{
    public void Configure(IWebHostBuilder builder)
    {
        builder.ConfigureServices((context, services) =>
        {
            var mongoClient = new MongoClient(context.Configuration.GetConnectionString("Mongodb"));
            var mongoDatabase = mongoClient.GetDatabase(context.Configuration["ConnectionStrings:MongoDatabaseName"]);
            services.AddSingleton(mongoDatabase);
            services.AddSingleton<IMongoDbContext>(p => new MongoDbContext(mongoDatabase));
        });
    }
}