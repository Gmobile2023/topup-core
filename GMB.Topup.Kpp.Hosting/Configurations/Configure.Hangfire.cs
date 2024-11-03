using System;
using Hangfire;
using Hangfire.Mongo;
using Hangfire.Mongo.Migration.Strategies;
using Hangfire.Mongo.Migration.Strategies.Backup;
using GMB.Topup.Kpp.Domain.Services;
using GMB.Topup.Kpp.Hosting.Configurations;
using GMB.Topup.Shared.ConfigDtos;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

[assembly: HostingStartup(typeof(ConfigureHangfire))]

namespace GMB.Topup.Kpp.Hosting.Configurations;

public class ConfigureHangfire : IHostingStartup
{
    public void Configure(IWebHostBuilder builder)
    {
        builder.ConfigureServices((context, services) =>
        {
            var configHangFire = new HangFireConfig();
            context.Configuration.GetSection("Hangfire").Bind(configHangFire);
            if (!configHangFire.EnableHangfire) return;
            var migrationOptions = new MongoMigrationOptions
            {
                MigrationStrategy = new MigrateMongoMigrationStrategy(),
                BackupStrategy = new CollectionMongoBackupStrategy()
            };
            services.AddHangfire(config =>
            {
                config.SetDataCompatibilityLevel(CompatibilityLevel.Version_170);
                config.UseSimpleAssemblyNameTypeSerializer();
                config.UseRecommendedSerializerSettings();
                config.UseMongoStorage(context.Configuration.GetConnectionString("Mongodb"),
                    context.Configuration["ConnectionStrings:HangfireDatabaseName"],
                    new MongoStorageOptions { MigrationOptions = migrationOptions });
            });
            services.AddHangfireServer(options =>
            {
                options.ServerName = $"{configHangFire.ServerName}-{Environment.MachineName}";
            });
        });
    }
    // public void Configure(IWebHostBuilder builder)
    // {
    //     builder.ConfigureServices((context, services) =>
    //     {
    //         if (bool.Parse(context.Configuration["Hangfire:EnableHangfire"]))
    //         {
    //             var migrationOptions = new MongoMigrationOptions
    //             {
    //                 MigrationStrategy = new MigrateMongoMigrationStrategy(),
    //                 BackupStrategy = new CollectionMongoBackupStrategy()
    //             };
    //             services.AddHangfire(config =>
    //             {
    //                 config.SetDataCompatibilityLevel(CompatibilityLevel.Version_170);
    //                 config.UseSimpleAssemblyNameTypeSerializer();
    //                 config.UseRecommendedSerializerSettings();
    //                 config.UseMongoStorage(context.Configuration.GetConnectionString("Mongodb"),
    //                     context.Configuration["ConnectionStrings:HangfireDatabaseName"],
    //                     new MongoStorageOptions { MigrationOptions = migrationOptions });
    //             });
    //             services.AddHangfireServer(option =>
    //             {
    //                 option.ServerName =
    //                     $"{context.Configuration["Hangfire:ServcerName"]}-{Environment.MachineName}";
    //             });
    //         }
    //     })
    //         .Configure((context, app) =>
    //         {
    //             if (bool.Parse(context.Configuration["Hangfire:EnableHangfire"]))
    //             {
    //                 var time = int.Parse(context.Configuration["Hangfire:TimeRun"]);
    //                 RecurringJob.AddOrUpdate<IAutoKppService>(x => x.SysAutoFile(),
    //                     $"0 {time + 1} * * *");

    //                 app.UseHangfireDashboard();
    //             }
    //         });
    // }
}