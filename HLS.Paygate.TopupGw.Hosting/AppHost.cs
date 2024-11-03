using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using Autofac;
using Funq;
using HLS.Paygate.Gw.Domain.Repositories;
using HLS.Paygate.Gw.Domain.Services;
using HLS.Paygate.Shared;
using HLS.Paygate.Shared.CacheManager;
using HLS.Paygate.Shared.Helpers;
using HLS.Paygate.Shared.UniqueIdGenerator;
using HLS.Paygate.TopupGw.Components.ApiServices;
using HLS.Paygate.TopupGw.Components.Connectors;
using HLS.Paygate.TopupGw.Components.Connectors.Appota;
using HLS.Paygate.TopupGw.Components.Connectors.Card;
using HLS.Paygate.TopupGw.Components.Connectors.CG2022;
using HLS.Paygate.TopupGw.Components.Connectors.Fake;
using HLS.Paygate.TopupGw.Components.Connectors.Imedia;
using HLS.Paygate.TopupGw.Components.Connectors.IOMedia;
using HLS.Paygate.TopupGw.Components.Connectors.Mobifone;
using HLS.Paygate.TopupGw.Components.Connectors.MTC;
using HLS.Paygate.TopupGw.Components.Connectors.NhatTran;
using HLS.Paygate.TopupGw.Components.Connectors.Octa;
using HLS.Paygate.TopupGw.Components.Connectors.Payoo;
using HLS.Paygate.TopupGw.Components.Connectors.PayTech;
using HLS.Paygate.TopupGw.Components.Connectors.SHT;
using HLS.Paygate.TopupGw.Components.Connectors.Viettel;
using HLS.Paygate.TopupGw.Components.Connectors.Viettel2;
using HLS.Paygate.TopupGw.Components.Connectors.Vimo;
using HLS.Paygate.TopupGw.Components.Connectors.WPay;
using HLS.Paygate.TopupGw.Components.Connectors.ZoTa;
using HLS.Paygate.TopupGw.Components.Connectors.ESale;
using HLS.Paygate.TopupGw.Components.TopupGwProcess;
using HLS.Paygate.TopupGw.Domains.BusinessServices;
using HLS.Paygate.TopupGw.Domains.Repositories;
using Infrastructure.AppVersion;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using ServiceStack;
using ServiceStack.Api.OpenApi;
using ServiceStack.Auth;
using ServiceStack.Configuration;
using ServiceStack.Text;
using Config = ServiceStack.Text.Config;
using HostConfig = ServiceStack.HostConfig;
using IContainer = Autofac.IContainer;
using HLS.Paygate.TopupGw.Components.Connectors.PayPoo;

namespace HLS.Paygate.TopupGw.Hosting;

public class AppHost : AppHostBase
{
    private readonly IConfiguration _configuration;
    private readonly ILifetimeScope _lifetimeScope;

    public AppHost(IConfiguration configuration, ILifetimeScope lifetimeScope) : base("TopupGw",
        typeof(MainService).Assembly)
    {
        _configuration = configuration;
        _lifetimeScope = lifetimeScope;
    }

    public override void Configure(IServiceCollection services)
    {
        services.AddScoped<ITopupGatewayService, TopupGatewayService>();
        services.AddScoped<ITransRepository, TransRepository>();
        services.AddScoped<IDateTimeHelper, DateTimeHelper>();
        services.AddScoped<ICacheManager, CacheManager>();
        services.AddScoped<ICommonService, CommonService>();
        services.AddScoped<ICommonRepository, CommonRepository>();
        services.AddTransient<ITopupGwProcess, TopupGwProcess>();
        services.AddTransient<AlarmAppVersion>();
        services.AddTransient<BackgroundServices>();
        
        services.AddScoped<ITransCodeGenerator, TransCodeGenerator>();
        services.AddTransient<GrpcClientHepper>(); 
        // _builder.Populate(services);
    }

    public override void Configure(Container container)
    {
        // container.RegisterAutoWiredAs<FakeConnector, IGatewayConnector>(ProviderConst.FAKE).ReusedWithin(ReuseScope.None);
        container.RegisterAutoWiredAs<ViettelVttConnector, IGatewayConnector>(ProviderConst.VTT)
            .ReusedWithin(ReuseScope.None);
        container.RegisterAutoWiredAs<ViettelVttConnector, IGatewayConnector>(ProviderConst.VTT_TEST)
            .ReusedWithin(ReuseScope.None);
        container.RegisterAutoWiredAs<ViettelVtt2Connector, IGatewayConnector>(ProviderConst.VTT2)
            .ReusedWithin(ReuseScope.None);
        container.RegisterAutoWiredAs<ViettelVtt2Connector, IGatewayConnector>(ProviderConst.VTT2_TEST)
            .ReusedWithin(ReuseScope.None);
        container.RegisterAutoWiredAs<ZotaConnector, IGatewayConnector>(ProviderConst.ZOTA)
            .ReusedWithin(ReuseScope.None);
        container.RegisterAutoWiredAs<ZotaConnector, IGatewayConnector>(ProviderConst.ZOTA_TEST)
            .ReusedWithin(ReuseScope.None);
        container.RegisterAutoWiredAs<OctaConnector, IGatewayConnector>(ProviderConst.OCTA)
            .ReusedWithin(ReuseScope.None);
        container.RegisterAutoWiredAs<OctaConnector, IGatewayConnector>(ProviderConst.OCTA_TEST)
            .ReusedWithin(ReuseScope.None);
        container.RegisterAutoWiredAs<MtcConnector, IGatewayConnector>(ProviderConst.MTC).ReusedWithin(ReuseScope.None);
        container.RegisterAutoWiredAs<IoMediaConnector, IGatewayConnector>(ProviderConst.IOMEDIA)
            .ReusedWithin(ReuseScope.None);
        container.RegisterAutoWiredAs<IoMediaConnector, IGatewayConnector>(ProviderConst.IOMEDIA_TEST)
            .ReusedWithin(ReuseScope.None);
        container.RegisterAutoWiredAs<FakeConnector, IGatewayConnector>(ProviderConst.FAKE)
            .ReusedWithin(ReuseScope.None);
        container.RegisterAutoWiredAs<ImediaConnector, IGatewayConnector>(ProviderConst.IMEDIA)
            .ReusedWithin(ReuseScope.None);
        container.RegisterAutoWiredAs<ImediaConnector, IGatewayConnector>(ProviderConst.IMEDIA_TEST)
            .ReusedWithin(ReuseScope.None);
        container.RegisterAutoWiredAs<CardConnector, IGatewayConnector>(ProviderConst.CARD)
            .ReusedWithin(ReuseScope.None);
        container.RegisterAutoWiredAs<NhatTranStockConnector, IGatewayConnector>(ProviderConst.NHATTRANSTOCK)
            .ReusedWithin(ReuseScope.None);
        container.RegisterAutoWiredAs<NhattranConnector, IGatewayConnector>(ProviderConst.NHATTRAN)
            .ReusedWithin(ReuseScope.None);
        container.RegisterAutoWiredAs<AppotaConnector, IGatewayConnector>(ProviderConst.APPOTA)
            .ReusedWithin(ReuseScope.None);
        container.RegisterAutoWiredAs<VimoConnector, IGatewayConnector>(ProviderConst.VIMO)
            .ReusedWithin(ReuseScope.None);
        container.RegisterAutoWiredAs<CG2022Connector, IGatewayConnector>(ProviderConst.CG2022)
            .ReusedWithin(ReuseScope.None);
        container.RegisterAutoWiredAs<SHTConnector, IGatewayConnector>(ProviderConst.SHT).ReusedWithin(ReuseScope.None);
        container.RegisterAutoWiredAs<WPayConnector, IGatewayConnector>(ProviderConst.WPAY)
            .ReusedWithin(ReuseScope.None);
        container.RegisterAutoWiredAs<PayooConnector, IGatewayConnector>(ProviderConst.PAYOO)
           .ReusedWithin(ReuseScope.None);
        container.RegisterAutoWiredAs<MobifoneConnector, IGatewayConnector>(ProviderConst.MOBIFONE)
           .ReusedWithin(ReuseScope.None);
        // container.RegisterAutoWiredAs<Imedia2Connector, IGatewayConnector>(ProviderConst.IMEDIA2)
        //    .ReusedWithin(ReuseScope.None);
        container.RegisterAutoWiredAs<PayTechConnector, IGatewayConnector>(ProviderConst.PAYTECH)
          .ReusedWithin(ReuseScope.None);
        container.RegisterAutoWiredAs<ESaleConnector, IGatewayConnector>(ProviderConst.ESALE)
          .ReusedWithin(ReuseScope.None);
        container.RegisterAutoWiredAs<PayPooConnector, IGatewayConnector>(ProviderConst.PAYPOO)
        .ReusedWithin(ReuseScope.None);

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

        Plugins.Add(new AuthFeature(() => new AuthUserSession(),
            new IAuthProvider[]
            {
                new NetCoreIdentityAuthProvider(AppSettings),
                // new JwtAuthProvider(AppSettings)
                // {
                //     HashAlgorithm = "HS256",
                //     RequireSecureConnection = false,
                //     // ValidateToken = (js,req) => req.GetJwtToken().LastRightPart('.').FromBase64UrlSafe().Length >= 32,
                //     // AuthKey = AesUtils.CreateKey()
                // },
                // new JwtAuthProvider(AppSettings)
                // {
                //     HashAlgorithm = "RS256",
                //     CallbackUrl = "http://localhost:5000",
                //     Audience = "default-api",
                //     AuthKeyBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("def2edf7-5d42-4edc-a84a-30136c340e13")),
                //     PrivateKey = new RSAParameters()
                //     // RequireSecureConnection = false,
                //     // Issuer = "Topup",
                //     // Audience = "Topup",
                //     // AuthKey = Encoding.ASCII.GetBytes("Topup_8CFB2EC534E14D56")
                // },

                new JwtAuthProviderReader(AppSettings)
                {
                    HashAlgorithm = "HS256",
                    RequireSecureConnection = false,
                    Issuer = "Topup",
                    Audience = "Topup",
                    AuthKey = Encoding.ASCII.GetBytes("Topup_8CFB2EC534E14D56") //AesUtils.CreateKey()
                    // CreatePayloadFilter = (payload, session) =>
                    // {
                    //     var githubAuth = session.ProviderOAuthAccess.Safe()
                    //         .FirstOrDefault(x => x.Provider == "github");
                    //     payload["ats"] = githubAuth != null
                    //         ? githubAuth.AccessTokenSecret : null;
                    // },

                    // PopulateSessionFilter = (session, obj, req) =>
                    // {
                    //     session.ProviderOAuthAccess = new List<IAuthTokens>
                    //     {
                    //         new AuthTokens { Provider = "github", AccessTokenSecret = obj["ats"] }
                    //     };
                    // },

                    //AuthKey = Encoding.ASCII.GetBytes("Topup_8CFB2EC534E14D56"),//AesUtils.CreateKey()
                    // ValidateToken = (js,req) =>
                    // {
                    //     var auth = req.GetAuthorization();
                    //     if (auth.StartsWith("Bearer_HLS"))
                    //     {
                    //
                    //         return true;
                    //         //return req.GetJwtToken().LastRightPart('.').FromBase64UrlSafe().Length >= 32;
                    //     }
                    //     return false;
                    // },
                }
            }));

        Plugins.Add(new OpenApiFeature());
        ConfigurePlugin<PredefinedRoutesFeature>(feature => feature.JsonApiRoute = null);
        JsConfig<string>.DeSerializeFn = str =>
        {
            using (JsConfig.With(new Config {IncludeNullValues = false}))
            {
                return str?.Trim();
            }
        };

        JsConfig<string>.SerializeFn = str =>
        {
            using (JsConfig.With(new Config {IncludeNullValues = false}))
            {
                return str?.Trim();
            }
        };

        JsConfig.Init(new Config
        {
            ExcludeDefaultValues = true
        });
    }

    public class AutofacIocAdapter : IContainerAdapter
    {
        private readonly IContainer _container;

        public AutofacIocAdapter(IContainer container)
        {
            _container = container;
        }

        public T TryResolve<T>()
        {
            if (_container.TryResolve<ILifetimeScope>(out var scope) &&
                scope.TryResolve(typeof(T), out var scopeComponent))
                return (T) scopeComponent;

            if (_container.TryResolve(typeof(T), out var component))
                return (T) component;

            return default;
        }

        public T Resolve<T>()
        {
            var ret = TryResolve<T>();
            return !ret.Equals(default)
                ? ret
                : throw new Exception($"Error trying to resolve '{typeof(T).Name}'");
        }
    }
}