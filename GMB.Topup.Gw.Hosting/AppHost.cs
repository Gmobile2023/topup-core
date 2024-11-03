using System.Linq;
using System.Threading.Tasks;
using Funq;
using HLS.Paygate.Gw.Domain.Repositories;
using HLS.Paygate.Gw.Domain.Services;
using HLS.Paygate.Gw.Interface.Services;
using HLS.Paygate.Shared;
using HLS.Paygate.Shared.AbpConnector;
using HLS.Paygate.Shared.CacheManager;
using HLS.Paygate.Shared.Helpers;
using HLS.Paygate.Shared.UniqueIdGenerator;
using HLS.Paygate.Shared.Utils;
using Infrastructure.AppVersion;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using ServiceStack;
using ServiceStack.Api.OpenApi;
using ServiceStack.Auth;
using HostConfig = ServiceStack.HostConfig;

namespace HLS.Paygate.Gw.Hosting;

public class AppHost : AppHostBase
{
    private readonly IConfiguration _configuration;

    public AppHost(IConfiguration configuration /*Func<IMessageService> createMqServerFn*/) : base("GatewayService",
        typeof(TopupService).Assembly)
    {
        _configuration = configuration;
        //_createMqServerFn = createMqServerFn;
    }

    public override void Configure(IServiceCollection services)
    {
        services.AddScoped<IPaygateMongoRepository, PaygateMongoRepository>();
        services.AddScoped<ISaleService, SaleService>();
        services.AddScoped<IDateTimeHelper, DateTimeHelper>();
        services.AddScoped<ICommonService, CommonService>();
        services.AddScoped<ICommonRepository, CommonRepository>();
        services.AddScoped<ITransCodeGenerator, TransCodeGenerator>();
        services.AddScoped<IPayBatchService, PayBatchService>();
        services.AddScoped<ICacheManager, CacheManager>();
        services.AddScoped<ISystemService, SystemService>();
        services.AddScoped<IValidateServiceBase, ValidateServiceBase>();
        services.AddScoped<ExternalServiceConnector>();
        services.AddTransient<AlarmAppVersion>();
        services.AddTransient<GrpcClientHepper>(); 
    }

    public override void Configure(Container container)
    {
        SetConfig(new HostConfig
        {
            //WebHostUrl = AppSettings.Get<string>("HostConfig:Url"),
            ApiVersion = AppSettings.Get<string>("HostConfig:Version"),
            DefaultContentType = MimeTypes.Json
            //DefaultRedirectPath = "/metadata"
        });
        Config.GlobalResponseHeaders.Remove(HttpHeaders.XPoweredBy);
        Config.GlobalResponseHeaders.Add(HttpHeaders.XPoweredBy, "JustForCode");

        Plugins.Add(new AuthFeature(() => new CustomUserSession(),
            new IAuthProvider[]
            {
                //new CredentialsAuthProvider(AppSettings),
                new NetCoreIdentityAuthProvider(AppSettings)
                {
                    RoleClaimType = "role",
                    PopulateSessionFilter = (session, principal, req) =>
                    {
                        ((CustomUserSession)session).AccountCode =
                            ((CustomUserSession)session)?.Meta["account_code"];
                        if (session != null)
                            ((CustomUserSession)session).ClientId =
                                ((CustomUserSession)session)?.Meta["client_id"];
                    }
                }
            }));
        Plugins.Add(new OpenApiFeature());
        ConfigurePlugin<PredefinedRoutesFeature>(feature => feature.JsonApiRoute = null);
        GlobalResponseFiltersAsync.Add((req, res, responseDto) =>
        {
            if (responseDto != null)
            {
                var properties = responseDto.GetType().GetProperties();

                var signature = properties.FirstOrDefault(p => p.Name.Contains("Signature"));
                var responseStatus = properties.FirstOrDefault(p => p.Name.Contains("ResponseStatus"));
                var sign = string.Empty;
                if (responseStatus?.PropertyType == typeof(ResponseStatusApi))
                {
                    var resStatus = (ResponseStatusApi)responseStatus.GetValue(responseDto, null);

                    if (resStatus != null)
                        sign = Cryptography.Sign(string.Join("|", resStatus.ErrorCode, resStatus.TransCode),
                            "NT_PrivateKey.pem");
                }

                if (signature != null) signature.SetValue(responseDto, sign, null);
            }

            return Task.CompletedTask;
        });
    }
}