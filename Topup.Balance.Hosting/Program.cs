using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Topup.Balance.Domain;
using Topup.Balance.Models.Grains;
using Topup.Shared;
using Infrastructure.AppVersion;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;
using Orleans.Sagas;
using Serilog;

ServiceStackHelper.SetLicense();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseOrleans(siloBuilder =>
{
    siloBuilder.UseSagas().Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromMinutes(1))
        .UseTransactions().AddMemoryGrainStorageAsDefault().AddMemoryGrainStorage("SagasStorage")
        .UseInMemoryReminderService();

    siloBuilder.Configure<SiloMessagingOptions>(opt =>
    {
        opt.ResponseTimeout = TimeSpan.FromMinutes(3);
        opt.SystemResponseTimeout = TimeSpan.FromMinutes(3);
        //opt.MaxMessageHeaderSize = 1000000000;
    }).AddAccountBalanceStorage("balance-grains-storage");
    //     , opt =>
    // {
    //     opt.NumStorageGrains = 50;
    // });
    // .AddRedisGrainStorage("balance-grains-storage", options =>
    // {
    //     options.ConfigurationOptions = new StackExchange.Redis.ConfigurationOptions()
    //     {
    //         DefaultDatabase = int.Parse(builder.Configuration["Silo:StorageDatabase"]),
    //         Password = builder.Configuration["Silo:RedisClusterPassword"],
    //         AbortOnConnectFail = false,
    //         ConnectRetry = 5,
    //         ConnectTimeout = 10000,
    //         SyncTimeout = 10000,
    //         EndPoints = { builder.Configuration["Silo:RedisStorage"] },
    //     };
    // });


    if (bool.Parse(builder.Configuration["Silo:UseCluster"] ?? string.Empty))
    {
        var configuration = new StackExchange.Redis.ConfigurationOptions()
        {
            DefaultDatabase = int.Parse(builder.Configuration["Silo:RedisClusterDatabase"]),
            AbortOnConnectFail = false,
            ConnectRetry = 5,
            ConnectTimeout = 10000,
            SyncTimeout = 10000,
            EndPoints = { builder.Configuration["Silo:RedisCluster"] }
        };
        if (bool.Parse(builder.Configuration["Silo:RedisAuth"]))
        {
            configuration.Password = builder.Configuration["Silo:RedisClusterPassword"];
        }
        
        
        var name = Dns.GetHostName(); // get container id
        var ip = Dns.GetHostEntry(name).AddressList
            .FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork);

        Console.WriteLine($"IP in cluster {ip}");

        siloBuilder.ConfigureEndpoints(ip,
                int.Parse(builder.Configuration["Silo:SiloPort"] ?? string.Empty),
                int.Parse(builder.Configuration["Silo:GatewayPort"] ?? string.Empty))
            .Configure<ClusterOptions>(opts =>
            {
                opts.ClusterId = builder.Configuration["Silo:ClusterId"];
                opts.ServiceId = builder.Configuration["Silo:ServiceId"];
            });

        siloBuilder.UseRedisClustering(otp => { otp.ConfigurationOptions = configuration; });
    }
    else
    {
        siloBuilder.UseLocalhostClustering();
    }
});

builder.Logging.AddSerilog();

var app = builder.Build();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
    app.UseHttpsRedirection();
    app.Services.GetRequiredService<AlarmAppVersion>().AlarmVersion();
}

await Task.Factory.StartNew(async () =>
{
    await Task.Delay(TimeSpan.FromSeconds(30));
    var client = app.Services.GetRequiredService<IGrainFactory>();
    var grain = client.GetGrain<IAutoTransferGrain>("AutoTransferGrainKey");
    await grain.Start();
});


await app.RunAsync();