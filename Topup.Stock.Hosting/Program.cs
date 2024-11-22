using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Topup.Shared;
using Infrastructure.AppVersion;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Orleans.Configuration;
using Orleans.Hosting;
using Serilog;
using ServiceStack;

ServiceStackHelper.SetLicense();
ServicePointManager.ServerCertificateValidationCallback +=
    (_, _, _, _) => true;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseOrleans((context, siloBuilder) =>
{
    siloBuilder.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromMinutes(1));
    siloBuilder.UseTransactions();
#if DEBUG
    siloBuilder.UseLocalhostClustering();
#else
    siloBuilder.UseRedisClustering(otp =>
    {
        otp.ConnectionString = builder.Configuration["Silo:RedisCluster"];
        otp.Database = int.Parse(builder.Configuration["Silo:RedisClusterDatabase"]);
    });
#endif

    // siloBuilder.UseConsulClustering(options =>
    // {
    //     options.Address = new Uri(context.Configuration["Silo:ClusterConsul"]);
    // });
    siloBuilder.Configure<SiloMessagingOptions>(opt =>
    {
        opt.ResponseTimeout = TimeSpan.FromMinutes(5);
        opt.SystemResponseTimeout = TimeSpan.FromMinutes(5);
        opt.ClientDropTimeout = TimeSpan.FromMinutes(3);
        //opt.MaxMessageHeaderSize = 1000000000;
    }).AddMemoryGrainStorage("stock-grains-storage");

    siloBuilder.Configure<ClusterOptions>(opts =>
    {
        opts.ClusterId = context.Configuration["Silo:ClusterId"];
        opts.ServiceId = context.Configuration["Silo:ServiceId"];
    });
    // siloBuilder.Configure<ClusterMembershipOptions>(opt => { opt.ValidateInitialConnectivity = false; });
    var name = Dns.GetHostName(); // get container id
    var ip = Dns.GetHostEntry(name).AddressList
        .FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork);

    siloBuilder.ConfigureEndpoints(ip, //(IPAddress.Parse(configuration["Silo:AdvertisedIP"]),
        int.Parse(context.Configuration["Silo:SiloPort"] ?? string.Empty),
        int.Parse(context.Configuration["Silo:GatewayPort"] ?? string.Empty));
});

builder.Logging.AddSerilog();

var app = builder.Build();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
    app.UseHttpsRedirection();
    //app.Services.Resolve<AlarmAppVersion>().AlarmVersion();
}

app.Run();