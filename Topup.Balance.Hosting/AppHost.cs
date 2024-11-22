using System;
using System.Collections.Generic;
using System.Net.Http;
using Funq;
using HLS.Paygate.Balance.Components.Services;
using HLS.Paygate.Balance.Domain.Services;
using HLS.Paygate.Shared;
using Microsoft.Extensions.Configuration;
using ServiceStack;
using ServiceStack.Api.OpenApi;

namespace HLS.Paygate.Balance.Hosting;

public class AppHost : AppHostBase
{
    // private Container _container;
    private readonly IConfiguration _configuration;

    public AppHost(IConfiguration configuration) : base("BalanceService", typeof(MainService).Assembly)
    {
        _configuration = configuration;
    }

    public override void Configure(Container container)
    {
        SetConfig(new HostConfig
        {
            WebHostUrl = _configuration["HostConfig:Url"],
            ApiVersion = _configuration["HostConfig:Version"],
            DefaultContentType = MimeTypes.Json,

            GlobalResponseHeaders = new Dictionary<string, string>
            {
                { "Vary", "Accept" },
                { "X-Powered-By", "JustForCode" }
            },
            EnableFeatures = Feature.All.Remove(
                Feature.Csv | Feature.Soap11 | Feature.Soap12) // | Feature.Metadata)
        });
        Plugins.Add(new OpenApiFeature());
        ConfigurePlugin<PredefinedRoutesFeature>(feature => feature.JsonApiRoute = null);
        AfterInitCallbacks.Add(host =>
        {
            var balanceService = host.TryResolve<IBalanceService>();
            balanceService.CurrencyCreateAsync(CurrencyCode.VND.ToString("G"));
            balanceService.CurrencyCreateAsync(CurrencyCode.DEBT.ToString("G"));
        });
    }
}