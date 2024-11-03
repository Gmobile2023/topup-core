using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using HLS.Paygate.Balance.Domain.Grains;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;
using Orleans.Runtime;

namespace HLS.Paygate.Balance.Hosting;

public class ClusterClientHostedService : IHostedService
{
    private readonly ILogger<ClusterClientHostedService> _logger;

    public ClusterClientHostedService(ILogger<ClusterClientHostedService> logger)
    {
        _logger = logger;

        Initialize().Value.GetAwaiter().GetResult();
        // Client = Initialize().Value.GetAwaiter().GetResult();
    }

    public IClusterClient Client { get; private set; }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        //await Client.Connect(CreateRetryFilter()).ConfigureAwait(false);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Client.Close();
    }

    private Lazy<Task<IClusterClient>> Initialize(string serviceName = "")
    {
        return new Lazy<Task<IClusterClient>>(async () =>
        {
            var attempt = 0;
            while (true)
            {
                var gateways = new[]
                {
                    new IPEndPoint(IPAddress.Parse("192.168.100.27"), 30006),
                    new IPEndPoint(IPAddress.Parse("192.168.100.8"), 30006)
                };

                var client = new ClientBuilder()
                    .ConfigureApplicationParts(parts =>
                    {
                        parts.AddApplicationPart(typeof(TransferGrain).Assembly).WithReferences();
                        parts.AddApplicationPart(typeof(BalanceGrain).Assembly).WithReferences();
                    })
                    // .UseAdoNetClustering(opts =>
                    // {
                    //     opts.Invariant = "System.Data.SqlClient";
                    //     opts.ConnectionString =
                    //         "Data Source=192.168.100.8;Initial Catalog=Paygate;User ID=sa;Password=123456";
                    // })
                    .UseStaticClustering(gateways)
                    // .UseStaticClustering(new IPEndPoint(IPAddress.Parse("192.168.100.27"), 40000))
                    // o =>
                    //     {
                    //         o.Gateways.AddRange(new List<Uri>()
                    //         {
                    //             new Uri("192.168.100.27"),
                    //             //new Uri("192.168.100.27:3006")
                    //         });
                    //     })
                    //.UseMongoDBClient("mongodb://paygate_test:paygate_test_123@192.168.33.10:27100")
                    // .UseMongoDBClustering(options =>
                    // {
                    //     options.DatabaseName = "balanceMembership";
                    //     // options.Strategy = MongoDBMembershipStrategy.Muiltiple;
                    //     // options.CreateShardKeyForCosmos = false;
                    // })
                    // .UseConsulClustering(options =>
                    // {
                    //     options.Address = new Uri("http://192.168.33.10:8500");
                    // })
                    .Configure<ClusterOptions>(opts =>
                    {
                        opts.ClusterId = "abc";
                        opts.ServiceId = "BalanceService";
                    })
                    .AddSimpleMessageStreamProvider("SMS_Balance")
                    .ConfigureLogging(logging => logging.AddConsole())
                    .Build();

                try
                {
                    // await client.Connect(CreateRetryFilter());
                    Client = client;
                }
                catch (SiloUnavailableException)
                {
                    attempt++;
                    if (attempt > 3) throw;
                    await Task.Delay(TimeSpan.FromSeconds(4));
                }
            }
        });
    }

    private static Func<Exception, Task<bool>> CreateRetryFilter(int maxAttempts = 5)
    {
        var attempt = 0;
        return RetryFilter;

        async Task<bool> RetryFilter(Exception exception)
        {
            attempt++;
            Console.WriteLine(
                $"Cluster client attempt {attempt} of {maxAttempts} failed to connect to cluster.  Exception: {exception}");
            if (attempt > maxAttempts) return false;

            await Task.Delay(TimeSpan.FromSeconds(4));
            return true;
        }
    }
}

public static class ClusterServiceBuilderExtensions
{
    public static IServiceCollection AddClusterService(this IServiceCollection services)
    {
        // services.AddOrleansMultiClient(build =>
        // {
        //     build.AddClient(otp =>
        //     {
        //         otp.ClusterId = "abc";
        //         otp.ServiceId = "BalanceService";
        //         otp.SetServiceAssembly(typeof(BalanceGrain).Assembly);
        //         otp.SetServiceAssembly(typeof(TransferGrain).Assembly);
        //         otp.Configure = (b =>
        //         {
        //             b.UseAdoNetClustering(opts =>
        //             {
        //                 opts.Invariant = "System.Data.SqlClient";
        //                 opts.ConnectionString =
        //                     "Data Source=192.168.100.8;Initial Catalog=Paygate;User ID=sa;Password=123456";
        //             });
        //         });
        //     });
        // });
        services.AddSingleton<ClusterClientHostedService>();
        services.AddSingleton<IHostedService>(_ => _.GetService<ClusterClientHostedService>());
        services.AddTransient(_ => _.GetService<ClusterClientHostedService>().Client);
        return services;
    }
}