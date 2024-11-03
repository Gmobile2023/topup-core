using System;
using System.Collections.Generic;
using System.Net.Http;
using Funq;
using HLS.Paygate.Stock.Components.ApiServices;
using Microsoft.Extensions.Configuration;

using ServiceStack;
using ServiceStack.Api.OpenApi;

namespace HLS.Paygate.Stock.Hosting;

public class AppHost : AppHostBase
{
    private readonly IConfiguration _configuration;

    public AppHost(IConfiguration configuration) : base("StockService", typeof(MainService).Assembly)
    {
        _configuration = configuration;
    }

    // public override void Configure(IServiceCollection services)
    // {
    //     services.AddScoped<ICardService, CardService>();
    //     services.AddScoped<ICardStockService, CardStockService>();
    //     services.AddScoped<ICardMongoRepository, CardMongoRepository>();
    // }

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