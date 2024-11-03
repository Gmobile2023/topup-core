using System;
using System.Net.Http;
using Funq;
using HLS.Paygate.Worker.Components;
using Microsoft.Extensions.Configuration;

using ServiceStack;
using ServiceStack.Api.OpenApi;
using ServiceStack.Validation;

namespace HLS.Paygate.Worker.Hosting;

public class AppHost : AppHostBase
{
    private readonly IConfiguration _configuration;

    //private readonly Func<IMessageService> _createMqServerFn;
    public AppHost(IConfiguration configuration) : base("WorkerService", typeof(WorkerService).Assembly)
    {
        _configuration = configuration;
        //this._createMqServerFn = createMqServerFn;
    }

    public override void Configure(Container container)
    {
        SetConfig(new HostConfig
        {
            //WebHostUrl = AppSettings.Get<string>("HostConfig:Url"),
            ApiVersion = AppSettings.Get<string>("HostConfig:Version")
        }); 
        Plugins.Add(new GrpcFeature(App));
        Plugins.Add(new OpenApiFeature());
        Plugins.Add(new ValidationFeature());
        ConfigurePlugin<PredefinedRoutesFeature>(feature => feature.JsonApiRoute = null);
    }
}