using System.IdentityModel.Tokens.Jwt;
using Infrastructure.AppVersion;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using ServiceStack;
using ServiceStack.Auth;

namespace HLS.Paygate.Gw.Hosting;

public class Startup : ModularStartup
{
    public new void ConfigureServices(IServiceCollection services)
    {
        services.AddMvcCore()
            .AddAuthorization();

        // services.AddCustomHealthCheck(Configuration);
        // services.AddLogging(loggingBuilder =>
        // {
        //     // configure Logging with NLog
        //     loggingBuilder.ClearProviders();
        //     loggingBuilder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
        //     loggingBuilder.AddNLog();
        // });
        JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(options =>
        {
            options.Authority = Configuration["OAuth:IdentityServer:AuthorizeUrl"]; // "https://topup365.com/";
            options.RequireHttpsMetadata = false;
            options.Audience = Configuration["OAuth:IdentityServer:Audience"];
        });
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory,
        AlarmAppVersion version)
    {
        app.UseAuthentication();
        //loggerFactory.AddNLog();
        // env.ConfigureNLog("nlog.config");
        if (env.IsDevelopment())
            app.UseDeveloperExceptionPage();
        else
            version.AlarmVersion();
        loggerFactory.AddSerilog();
        //loggerFactory.AddNLog();
        var appHost = new AppHost(Configuration)
        {
            AppSettings = new NetCoreAppSettings(Configuration)
        };
        app.UseServiceStack(appHost);
        var pathBase = Configuration["PATH_BASE"];
        if (!string.IsNullOrEmpty(pathBase)) app.UsePathBase(pathBase);

        app.ApplicationServices.GetService<IAuthRepository>().InitSchema();
    }
}

public static class CustomExtensionMethods
{
    public static IServiceCollection AddCustomHealthCheck(this IServiceCollection services,
        IConfiguration configuration)
    {
        var hcBuilder = services.AddHealthChecks();

        hcBuilder.AddCheck("self", () => HealthCheckResult.Healthy());

        //if (configuration.GetValue<bool>("AzureServiceBusEnabled"))
        //{
        //    hcBuilder
        //        .AddAzureServiceBusTopic(
        //            configuration["EventBusConnection"],
        //            topicName: "eshop_event_bus",
        //            name: "payment-servicebus-check",
        //            tags: new string[] { "servicebus" });
        //}
        //else
        //{
        hcBuilder
            .AddRabbitMQ(
                $"amqp://{configuration["EventBusConnection"]}",
                name: "payment-rabbitmqbus-check",
                tags: new[] {"rabbitmqbus"});
        //}

        return services;
    }
}