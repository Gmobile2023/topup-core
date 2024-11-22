using System;
using Hangfire;
using Hangfire.Mongo;
using Hangfire.Mongo.Migration.Strategies;
using Hangfire.Mongo.Migration.Strategies.Backup;
using Topup.Backend.Hosting.Configurations;
using Topup.Shared.ConfigDtos;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

[assembly: HostingStartup(typeof(ConfigureHangfire))]
namespace Topup.Backend.Hosting.Configurations;

public class ConfigureHangfire : IHostingStartup
{
    public void Configure(IWebHostBuilder builder)
    {
        builder.ConfigureServices((context, services) =>
        {
            var config = new BackendHangFireConfig();
            context.Configuration.GetSection("Hangfire").Bind(config);
            if (!config.EnableHangfire) return;
            var migrationOptions = new MongoMigrationOptions
            {
                MigrationStrategy = new MigrateMongoMigrationStrategy(),
                BackupStrategy = new CollectionMongoBackupStrategy()
            };
            services.AddHangfire(c =>
            {
                c.SetDataCompatibilityLevel(CompatibilityLevel.Version_170);
                c.UseSimpleAssemblyNameTypeSerializer();
                c.UseRecommendedSerializerSettings();
                c.UseMongoStorage(context.Configuration.GetConnectionString("MongoHangFire"),
                    context.Configuration["ConnectionStrings:HangfireDatabaseName"],
                    new MongoStorageOptions { MigrationOptions = migrationOptions });
            });
            services.AddHangfireServer(options =>
            {
                options.ServerName = $"{config.ServerName}-{Environment.MachineName}";
            });
        });
    }
}