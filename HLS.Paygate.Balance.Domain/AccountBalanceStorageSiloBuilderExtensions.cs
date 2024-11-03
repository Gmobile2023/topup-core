using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Orleans.Configuration;
using Orleans.Hosting;
using Orleans.Runtime.Hosting;
using Orleans.Storage;

namespace HLS.Paygate.Balance.Domain;

public static class AccountBalanceStorageSiloBuilderExtensions
{
    public static ISiloBuilder AddAccountBalanceStorage(this ISiloBuilder builder, string name, Action<OptionsBuilder<MemoryGrainStorageOptions>> configureOptions = null)
    {
        return builder
            .ConfigureServices(services =>
            {
                configureOptions?.Invoke(services.AddOptions<MemoryGrainStorageOptions>(name));
                services.AddTransient<IPostConfigureOptions<MemoryGrainStorageOptions>, DefaultStorageProviderSerializerOptionsConfigurator<MemoryGrainStorageOptions>>();
                services.ConfigureNamedOptionForLogging<MemoryGrainStorageOptions>(name);
                services.AddGrainStorage(name, AccountBalanceStorageFactory.Create);
            });
    }
}