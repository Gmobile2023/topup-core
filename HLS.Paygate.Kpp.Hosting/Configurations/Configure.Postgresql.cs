﻿using HLS.Paygate.Kpp.Domain.Repositories;
using HLS.Paygate.Kpp.Hosting.Configurations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ServiceStack;
using ServiceStack.OrmLite;
using ServiceStack.OrmLite.PostgreSQL;

[assembly: HostingStartup(typeof(ConfigurePostgresql))]

namespace HLS.Paygate.Kpp.Hosting.Configurations;

public class ConfigurePostgresql : IHostingStartup
{
    public void Configure(IWebHostBuilder builder)
    {
        builder.ConfigureServices((context, services) =>
        {
            // services.AddSingleton<IKppConnectionFactory>(new KppConnectionFactory(
            //     Configuration.GetConnectionString("Kpp"),
            //     SqliteOrmLiteDialectProvider.Instance));

            services.AddSingleton<IPostgreConnectionFactory>(new PostgreConnectionFactory(
                context.Configuration.GetConnectionString("PostgreDb"),
                PostgreSqlDialectProvider.Instance));
        });
    }
}
