using System;
using Hangfire;
using Hangfire.Mongo;
using Hangfire.Mongo.Migration.Strategies;
using Hangfire.Mongo.Migration.Strategies.Backup;
using Topup.Common.Hosting.Configurations;
using Topup.Shared.ConfigDtos;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

[assembly: HostingStartup(typeof(ConfigureHangfire))]
namespace Topup.Common.Hosting.Configurations;

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
}