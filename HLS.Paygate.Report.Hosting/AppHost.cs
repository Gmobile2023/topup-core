using System;
using System.Collections.Generic;
using System.Net.Http;
using Funq;
using HLS.Paygate.Report.Domain.Repositories;
using HLS.Paygate.Report.Interface.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using ServiceStack;
using ServiceStack.Api.OpenApi;

namespace HLS.Paygate.Report.Hosting;

public class AppHost : AppHostBase
{
    private readonly IConfiguration _configuration;

    public AppHost(IConfiguration configuration) : base("ReportService", typeof(ReportService).Assembly)
    {
        _configuration = configuration;
    }

    public override void Configure(IServiceCollection services)
    {
        services.AddScoped<IReportMongoRepository, ReportMongoRepository>();
        //services.AddScoped<IPostgreConnectionFactory, PostgreConnectionFactory>();
        //services.AddScoped<IReportPosgresqlRepository, ReportPosgresqlRepository>();
    }

    public override void Configure(Container container)
    {
        SetConfig(new HostConfig
        {
            WebHostUrl = AppSettings.GetString("HostConfig:Url"),
            ApiVersion = AppSettings.GetString("HostConfig:Version"),
            DefaultContentType = MimeTypes.Json,
            GlobalResponseHeaders = new Dictionary<string, string>
            {
                {"Vary", "Accept"},
                {"X-Powered-By", "JustForCode"}
            },
            EnableFeatures = Feature.All.Remove(
                Feature.Csv | Feature.Soap11 | Feature.Soap12) // | Feature.Metadata),
        });
        Plugins.Add(new OpenApiFeature());
        ConfigurePlugin<PredefinedRoutesFeature>(feature => feature.JsonApiRoute = null);
    }
}