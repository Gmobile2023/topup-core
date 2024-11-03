using System;
using Hangfire;
using Hangfire.Mongo;
using Hangfire.Mongo.Migration.Strategies;
using Hangfire.Mongo.Migration.Strategies.Backup;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ServiceStack;

namespace HLS.Paygate.Commission.Hosting.Configurations
{
    public class ConfigureHangfire : IConfigureServices, IConfigureApp
    {
        public ConfigureHangfire(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        private IConfiguration Configuration { get; }

        public void Configure(IApplicationBuilder app)
        {
            if (bool.Parse(Configuration["Hangfire:EnableHangfire"]))
            {
                if (bool.Parse(Configuration["Hangfire:IsRun"]))
                {
                    var options = new BackgroundJobServerOptions
                    {
                        ServerName = $"{Configuration["Hangfire:ServcerName"]}-{Environment.MachineName}"
                    };
                    var server = new BackgroundJobServer(options);
                    app.UseHangfireServer(options);
                }

                app.UseHangfireDashboard();
            }
        }


        public void Configure(IServiceCollection services)
        {
            //Add hangfire
            if (bool.Parse(Configuration["Hangfire:EnableHangfire"]))
            {
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
                    config.UseMongoStorage(Configuration.GetConnectionString("Mongodb"), Configuration["ConnectionStrings:HangfireDatabaseName"],
                        new MongoStorageOptions { MigrationOptions = migrationOptions });
                });
                services.AddHangfireServer();
            }
        }
    }
}
