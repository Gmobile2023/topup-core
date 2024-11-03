using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;
using Orleans.Providers.MongoDB.Configuration;
using ServiceStack;
using TW.Paygate.Stock.Domains.Grains;
using TW.Paygate.StockGrains;

namespace TW.Paygate.Stock.Hosting.Configurations
{
    public class ConfigureOrleans //: IConfigureServices//, IAfterInitAppHost
    {
        IConfiguration Configuration { get; }
        public ConfigureOrleans(IConfiguration configuration) => Configuration = configuration;
        
        public void Configure(IServiceCollection services)
        {
            var siloHost = SiloHostInit();
            services.AddSingleton(x => siloHost);
            var client = SiloClientInit();

            services.AddSingleton(x => client);
            
            services.AddHostedService<OrleansHostedService>();
            services.AddHostedService<OrleansClientHostedService>();
            // services.AddSingleton<IClusterClientProvider, HostedClusterClientProvider>();
        }

        ISiloHost SiloHostInit()
        {
            var connectionString = Configuration["ConnectionStrings:SiloData"] + "/Paygate/?retryWrites=false";
            //var createShardKey = true;
            var silo = new SiloHostBuilder()
                .ConfigureApplicationParts(parts =>
                {
                    parts.AddApplicationPart(typeof(StockGrain).Assembly).WithReferences();
                }).UseLocalhostClustering()
                .AddMemoryGrainStorage(Configuration["ConnectionStrings:MongoDatabaseName"])
                .AddMemoryGrainStorageAsDefault()
                // .UseMongoDBClient(connectionString)
                // .UseMongoDBClustering(options =>
                // {
                //     options.DatabaseName = "Paygate";
                //     options.CreateShardKeyForCosmos = createShardKey;
                //     // options.Strategy = MongoDBMembershipStrategy.Muiltiple;
                // })
                // .AddMongoDBGrainStorageAsDefault(options =>
                // {
                //     options.Configure(p => p.DatabaseName = "Paygate");
                // })
                // .AddMongoDBGrainStorage("MongoDBStore", options =>
                // {
                //     options.DatabaseName = "Paygate";
                //     options.CreateShardKeyForCosmos = createShardKey;
                //     options.ConfigureJsonSerializerSettings = settings =>
                //     {
                //         settings.NullValueHandling = NullValueHandling.Include;
                //         settings.ObjectCreationHandling = ObjectCreationHandling.Replace;
                //         settings.DefaultValueHandling = DefaultValueHandling.Populate;
                //     };
                // })
                .UseTransactions()
                        
                .Configure<ClusterOptions>(opts =>
                {
                    opts.ClusterId = "dev_stock";
                    opts.ServiceId = "Api_Stock_Service";
                })
                .ConfigureEndpoints(IPAddress.Loopback, 11112, 30001).Build();

            return silo;
        }

        IClusterClient SiloClientInit()
        {
            var connectionString = Configuration["ConnectionStrings:SiloData"] + "/Paygate/?retryWrites=false";
            var client = new ClientBuilder()
                .ConfigureApplicationParts(options =>
                {
                    options.AddApplicationPart(typeof(IStockGrain).Assembly);
                })
                .UseLocalhostClustering()
                // .UseMongoDBClient(connectionString)
                // .UseMongoDBClustering(options =>
                // {
                //     options.DatabaseName = "Paygate";
                // })
                .Configure<ClusterOptions>(options =>
                {
                    options.ClusterId = "dev";
                    options.ServiceId = "ApiAPIService";
                })
                .ConfigureLogging(logging => logging.AddConsole())
                .Build();

            return client;
        }

        public void AfterInit(IAppHost appHost)
        {
            // var connectionString = Configuration["ConnectionStrings:SiloData"] + "/Paygate/?retryWrites=false";
            // var createShardKey = true;
            // var silo = new SiloHostBuilder()
            //     .ConfigureApplicationParts(parts =>
            //     {
            //         parts.AddApplicationPart(typeof(StockGrain).Assembly).WithReferences();
            //     })
            //     .UseMongoDBClient(connectionString)
            //     .UseMongoDBClustering(options =>
            //     {
            //         options.DatabaseName = "Paygate";
            //         options.CreateShardKeyForCosmos = createShardKey;
            //         options.Strategy = MongoDBMembershipStrategy.Muiltiple;
            //     })
            //     .AddMongoDBGrainStorageAsDefault(options =>
            //     {
            //         options.Configure(p => p.DatabaseName = "Paygate");
            //     })
            //     .AddMongoDBGrainStorage("MongoDBStore", options =>
            //     {
            //         options.DatabaseName = "Paygate";
            //         options.CreateShardKeyForCosmos = createShardKey;
            //         options.ConfigureJsonSerializerSettings = settings =>
            //         {
            //             settings.NullValueHandling = NullValueHandling.Include;
            //             settings.ObjectCreationHandling = ObjectCreationHandling.Replace;
            //             settings.DefaultValueHandling = DefaultValueHandling.Populate;
            //         };
            //     })
            //     .UseTransactions()
            //             
            //     .Configure<ClusterOptions>(opts =>
            //     {
            //         opts.ClusterId = "dev";
            //         opts.ServiceId = "ApiAPIService";
            //     })
            //     .ConfigureEndpoints(IPAddress.Loopback, 11111, 30000).Build();
            //
            // silo.StartAsync().Wait();

            // Task.Run(() =>
            // {
            //     appHost.Resolve<ISiloHost>().StartAsync().Wait();
            // });

            // Task.Run(() => { appHost.Resolve<IClusterClient>().Connect().Wait(); });
        }
    }
}