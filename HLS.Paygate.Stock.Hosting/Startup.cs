using HLS.Paygate.Shared.CacheManager;
using HLS.Paygate.Shared.Helpers;
using HLS.Paygate.Stock.Components.StockProcess;
using HLS.Paygate.Stock.Domains.BusinessServices;
using HLS.Paygate.Stock.Domains.Repositories;
using Infrastructure.AppVersion;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using ServiceStack;

namespace HLS.Paygate.Stock.Hosting;

public class Startup : ModularStartup
{
    public Startup(IConfiguration configuration) : base(configuration)
    {
        Configuration = configuration;
    }

    public new void ConfigureServices(IServiceCollection services)
    {
        //services.AddLogging(loggingBuilder =>
        //{
        // configure Logging with NLog
        //loggingBuilder.ClearProviders();
        //loggingBuilder.SetMinimumLevel(LogLevel.Debug);
        //loggingBuilder.AddNLog();

        services.AddScoped<ICardService, CardService>();
        services.AddScoped<IStockAirtimeService, StockAirtimeService>();
        services.AddScoped<ICardStockService, CardStockService>();
        services.AddScoped<ICardMongoRepository, CardMongoRepository>();
        services.AddScoped<IDateTimeHelper, DateTimeHelper>();
        services.AddScoped<ICacheManager, CacheManager>();
        services.AddScoped<IStockProcess, StockProcess>();
        services.AddTransient<AlarmAppVersion>();
        services.AddTransient<GrpcClientHepper>(); 
        //});
        services.AddCors(options =>
        {
            options.AddPolicy("CorsPolicy",
                builder =>
                {
                    builder.WithOrigins(Configuration["CorsOrigins"]
                            .Split(",")
                            .ToArray())
                        .SetIsOriginAllowedToAllowWildcardSubdomains()
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                });
        });
        // services.AddScoped<IViettelPayConnector, ViettelPayConnector>();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory,
        AlarmAppVersion version)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler("/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
            version.AlarmVersion();
        }

        loggerFactory.AddSerilog();
        //loggerFactory.AddNLog();
        //loggerFactory.AddProvider(new NLog.Extensions.Logging.NLogLoggerProvider());
        var apphost = new AppHost(Configuration)
        {
            AppSettings = new NetCoreAppSettings(Configuration)
        };
        app.UseServiceStack(apphost);
        app.UseCors("CorsPolicy");
    }
}