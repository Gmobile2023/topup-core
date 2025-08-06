using System;
using System.Collections.Generic;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Funq;
using Topup.Gw.Domain.Repositories;
using Topup.Gw.Domain.Services;
using Topup.Shared;
using Topup.Shared.CacheManager;
using Topup.Shared.Helpers;
using Topup.Shared.UniqueIdGenerator;
using Topup.TopupGw.Components.ApiServices;
using Topup.TopupGw.Components.Connectors;
using Topup.TopupGw.Components.Connectors.Card;
using Topup.TopupGw.Components.Connectors.CG2022;
using Topup.TopupGw.Components.Connectors.Fake;
using Topup.TopupGw.Components.Connectors.Imedia;
using Topup.TopupGw.Components.Connectors.IOMedia;
using Topup.TopupGw.Components.Connectors.Mobifone;
using Topup.TopupGw.Components.Connectors.MTC;
using Topup.TopupGw.Components.Connectors.NhatTran;
using Topup.TopupGw.Components.Connectors.Octa;
using Topup.TopupGw.Components.Connectors.Payoo;
using Topup.TopupGw.Components.Connectors.SHT;
using Topup.TopupGw.Components.Connectors.Viettel2;
using Topup.TopupGw.Components.Connectors.Vimo;
using Topup.TopupGw.Components.Connectors.WPay;
using Topup.TopupGw.Components.Connectors.ZoTa;
using Topup.TopupGw.Components.Connectors.ESale;
using Topup.TopupGw.Components.Connectors.HLS;
using Topup.TopupGw.Components.Connectors.IRIS;
using Topup.TopupGw.Components.Connectors.PayTech;
using Topup.TopupGw.Components.Connectors.VDS;
using Topup.TopupGw.Components.TopupGwProcess;
using Topup.TopupGw.Domains.BusinessServices;
using Topup.TopupGw.Domains.Repositories;
using Topup.TopupGw.Hosting.Configurations;
using Infrastructure.AppVersion;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ServiceStack;
using ServiceStack.Api.OpenApi;
using ServiceStack.Configuration;
using ServiceStack.Text;
using HostConfig = ServiceStack.HostConfig;
using Topup.TopupGw.Components.Connectors.PayPoo;
using Topup.TopupGw.Components.Connectors.WhyPay;
using Topup.TopupGw.Components.Connectors.Advance;
using Topup.TopupGw.Components.Connectors.VTC;
using Topup.TopupGw.Components.Connectors.Gate;
using Topup.TopupGw.Components.Connectors.ShopeePay;
using Topup.TopupGw.Components.Connectors.Vmg;
using Topup.TopupGw.Components.Connectors.Vinnet;
using Topup.TopupGw.Components.Connectors.Finviet;
using Topup.TopupGw.Components.Connectors.VNPTPay;
using Topup.TopupGw.Components.Connectors.Vmg2;
using Topup.TopupGw.Components.Connectors.Vinatti;

[assembly: HostingStartup(typeof(AppHost))]

namespace Topup.TopupGw.Hosting.Configurations;

public class AppHost : AppHostBase, IHostingStartup
{
    public AppHost() : base("TopupGw", typeof(MainService).Assembly)
    {
    }

    // public ILifetimeScope AutofacContainer { get; private set; }

    public void Configure(IWebHostBuilder builder)
    {
        builder
            .ConfigureServices(services =>
            {
                services.AddAutofac();
                services.AddOptions();
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
            })
            //.ConfigureAppHost(appHost =>
            //{
            //    var backgroundService = appHost.Resolve<BackgroundServices>();
            //    backgroundService.StartJob();
            //})
            .Configure((context, app) =>
            {
                // Configure ASP .NET Core App
                if (!HasInit)
                    app.UseServiceStack(new AppHost());

                var pathBase = context.Configuration["PATH_BASE"];
                if (!string.IsNullOrEmpty(pathBase)) app.UsePathBase(pathBase);

                app.UseRouting();
            });
    }

    public override void Configure(Container container)
    {
        // container.RegisterAutoWiredAs<ViettelVttConnector, IGatewayConnector>(ProviderConst.VTT)
        //            .ReusedWithin(ReuseScope.None);
        // container.RegisterAutoWiredAs<ViettelVttConnector, IGatewayConnector>(ProviderConst.VTT_TEST)
        //     .ReusedWithin(ReuseScope.None);
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
        container.RegisterAutoWiredAs<SystemStockConnector, IGatewayConnector>(ProviderConst.SYSTEMSTOCK)
            .ReusedWithin(ReuseScope.None);
        container.RegisterAutoWiredAs<NhattranConnector, IGatewayConnector>(ProviderConst.NHATTRAN)
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
        container.RegisterAutoWiredAs<ESaleConnector, IGatewayConnector>(ProviderConst.ESALE)
          .ReusedWithin(ReuseScope.None);
        container.RegisterAutoWiredAs<PayTechConnector, IGatewayConnector>(ProviderConst.PAYTECH)
          .ReusedWithin(ReuseScope.None);
        container.RegisterAutoWiredAs<HLSConnector, IGatewayConnector>(ProviderConst.HLS)
            .ReusedWithin(ReuseScope.None);
        container.RegisterAutoWiredAs<VDSConnector, IGatewayConnector>(ProviderConst.VDS)
           .ReusedWithin(ReuseScope.None);
        container.RegisterAutoWiredAs<PayPooConnector, IGatewayConnector>(ProviderConst.PAYPOO)
         .ReusedWithin(ReuseScope.None);
        container.RegisterAutoWiredAs<WhyPayConnector, IGatewayConnector>(ProviderConst.WHYPAY)
            .ReusedWithin(ReuseScope.None);
        container.RegisterAutoWiredAs<IrisConnector, IGatewayConnector>(ProviderConst.IRIS)
            .ReusedWithin(ReuseScope.None);
        // container.RegisterAutoWiredAs<IrisPinCodeConnector, IGatewayConnector>(ProviderConst.IRIS_PINCODE)
        //     .ReusedWithin(ReuseScope.None);
        container.RegisterAutoWiredAs<AdvanceConnector, IGatewayConnector>(ProviderConst.ADVANCE)
            .ReusedWithin(ReuseScope.None);
        container.RegisterAutoWiredAs<Vtc365Connector, IGatewayConnector>(ProviderConst.VTC365)
            .ReusedWithin(ReuseScope.None);
        container.RegisterAutoWiredAs<GateConnector, IGatewayConnector>(ProviderConst.GATE)
           .ReusedWithin(ReuseScope.None);
        container.RegisterAutoWiredAs<ShopeePayConnector, IGatewayConnector>(ProviderConst.SHOPEEPAY)
          .ReusedWithin(ReuseScope.None);
        container.RegisterAutoWiredAs<VmgConnector, IGatewayConnector>(ProviderConst.VMG)
          .ReusedWithin(ReuseScope.None);
        container.RegisterAutoWiredAs<Vmg2Connector, IGatewayConnector>(ProviderConst.VMG2)
          .ReusedWithin(ReuseScope.None);
        container.RegisterAutoWiredAs<VinnetConnector, IGatewayConnector>(ProviderConst.VINNET)
         .ReusedWithin(ReuseScope.None);
        container.RegisterAutoWiredAs<FinVietConnector, IGatewayConnector>(ProviderConst.FINVIET);
        container.RegisterAutoWiredAs<VnptPayConnector, IGatewayConnector>(ProviderConst.VNPTPAY)
           .ReusedWithin(ReuseScope.None);
        container.RegisterAutoWiredAs<VinattiConnector, IGatewayConnector>(ProviderConst.VINATTI)
            .ReusedWithin(ReuseScope.None);

        SetConfig(new HostConfig
        {
            DefaultContentType = MimeTypes.Json,
            DebugMode = AppSettings.Get(nameof(HostConfig.DebugMode), false),
            UseSameSiteCookies = true,
            GlobalResponseHeaders = new Dictionary<string, string>
            {
                { "Server", "nginx/1.4.7" },
                { "Vary", "Accept" },
                { "X-Powered-By", "GMB_TopupGw" }
            },
            EnableFeatures = Feature.All.Remove(
                Feature.Csv | Feature.Soap11 | Feature.Soap12) // | Feature.Metadata),
        });

        ConfigurePlugin<PredefinedRoutesFeature>(feature => feature.JsonApiRoute = null);
        Plugins.Add(new GrpcFeature(App));
        Plugins.Add(new OpenApiFeature());

        JsConfig.Init(new ServiceStack.Text.Config
        {
            ExcludeTypeInfo = true
        });
    }

    public class AutofacIocAdapter : IContainerAdapter
    {
        private readonly Autofac.IContainer _container;

        public AutofacIocAdapter(Autofac.IContainer container)
        {
            _container = container;
        }

        public T TryResolve<T>()
        {
            if (_container.TryResolve<ILifetimeScope>(out var scope) &&
                scope.TryResolve(typeof(T), out var scopeComponent))
                return (T)scopeComponent;

            if (_container.TryResolve(typeof(T), out var component))
                return (T)component;

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