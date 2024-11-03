using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Hosting;

namespace HLS.Paygate.TopupGw.Hosting
{
    public class OrleansHostedService : IHostedService
    {
        private readonly ISiloHost _siloHost;
        private readonly ILogger<OrleansHostedService> _logger;
        // private readonly IEnumerable<IConnectableStore> _stores;

        public OrleansHostedService(ILoggerFactory loggerFactory, ISiloHost siloHost, IServiceProvider serviceProvider)
        {
            this._logger = loggerFactory.CreateLogger<OrleansHostedService>();
            this._siloHost = siloHost;
            // this._stores = serviceProvider.GetServices<IConnectableStore>();
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            this._logger.LogInformation($"--------- Starting Orleans Hosted Silo ---------");
            // if (this._stores?.Count() > 0)
            // {
            //     await Task.WhenAll(this._stores.Select(s => s.Connect()));
            // }
            await this._siloHost.StartAsync(cancellationToken);
            this._logger.LogInformation($"--------- Started Orleans Hosted Silo! ---------");
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            this._logger.LogInformation($"--------- Stopping Orleans Hosted Silo ---------");
            await this._siloHost.StopAsync(cancellationToken);
            // if (this._stores?.Count() > 0)
            // {
            //     await Task.WhenAll(this._stores.Select(s => s.Disconnect()));
            // }
            this._logger.LogInformation($"--------- Stopped Orleans Hosted Silo ---------");
        }
    }

    // internal class HostedClusterClientProvider : IClusterClientProvider
    // {
    //     private IClusterClient _clusterClient;
    //
    //     public HostedClusterClientProvider(IClusterClient client)
    //     {
    //         this._clusterClient = client;
    //     }
    //     public IClusterClient GetClient() => this._clusterClient;
    // }

    internal class OrleansClientHostedService : IHostedService
    {
        private readonly IClusterClient _clusterClient;
        private readonly ILogger<OrleansClientHostedService> _logger;

        public OrleansClientHostedService(ILoggerFactory loggerFactory, IClusterClient clusterClient)
        {
            this._logger = loggerFactory.CreateLogger<OrleansClientHostedService>();
            this._clusterClient = clusterClient;
        }

        //public OrleansClientHostedService(ILoggerFactory loggerFactory, IServiceProvider serviceProvider)
        //{
        //    this._logger = loggerFactory.CreateLogger<OrleansClientHostedService>();

        //    serviceProvider.GetService<SerializationManager>().RegisterSerializers(serviceProvider.GetService<IApplicationPartManager>());

        //    // Construct and return the cluster client.
        //    var asm = typeof(IClusterClient).Assembly;
        //    var runtimeClientType = asm.DefinedTypes.Where(a => a.Name.Contains("OutsideRuntimeClient")).First().AsType();
        //    var consumeServicesMethod = runtimeClientType.GetMethod("ConsumeServices", BindingFlags.Instance | BindingFlags.NonPublic);
        //    var runtimeClient = serviceProvider.GetRequiredService(runtimeClientType);
        //    consumeServicesMethod.Invoke(runtimeClient, new[] { serviceProvider });

        //    this._clusterClient = serviceProvider.GetRequiredService<IClusterClient>();
        //}

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            this._logger.LogInformation($"--------- Starting Orleans Hosted Client ---------");
            await this._clusterClient.Connect();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            this._logger.LogInformation($"--------- Stopping Orleans Hosted Client ---------");
            return this._clusterClient.Close();
        }
    }
}