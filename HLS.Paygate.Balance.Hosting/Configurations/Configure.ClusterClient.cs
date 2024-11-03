using System;
using System.Net;
using System.Threading.Tasks;
using HLS.Paygate.Balance.Domain.Grains;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;
using Orleans.Runtime;
using ServiceStack;

namespace HLS.Paygate.Balance.Hosting.Configurations
{
    [Priority(-1)]
    public class ConfigureClusterClient : IConfigureServices
    {
        public void Configure(IServiceCollection services)
        {
            // var client = Initialize(string.Empty).Value.Result;
            // services.AddSingleton(_ => client);
            // services.AddSingleton<IGrainFactory>(_ => client);
            // services.AddHostedService<ClusterClientHostedService>();
            // services.AddHostedService<IHostedService>(_ => _.GetService<ClusterClientHostedService>());
            services.AddSingleton<ClusterClientHostedService>();
            services.AddSingleton<IHostedService>(_ => _.GetService<ClusterClientHostedService>());
            services.AddSingleton<IClusterClient>(_ => _.GetService<ClusterClientHostedService>().Client);
            // services.AddSingleton<IGrainFactory>(_ => _.GetService<ClusterClientHostedService>().Client);
        }
    }
}