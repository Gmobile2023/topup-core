using System;
using System.Collections.Generic;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Funq;
using HLS.Paygate.Gw.Domain.Repositories;
using HLS.Paygate.Gw.Domain.Services;
using HLS.Paygate.Shared;
using HLS.Paygate.Shared.CacheManager;
using HLS.Paygate.Shared.Helpers;
using HLS.Paygate.Shared.UniqueIdGenerator;
using HLS.Paygate.TopupGw.Components.ApiServices;
using HLS.Paygate.TopupGw.Components.Connectors;
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
using HLS.Paygate.TopupGw.Components.Connectors.SHT;
using HLS.Paygate.TopupGw.Components.Connectors.Viettel2;
using HLS.Paygate.TopupGw.Components.Connectors.Vimo;
using HLS.Paygate.TopupGw.Components.Connectors.WPay;
using HLS.Paygate.TopupGw.Components.Connectors.ZoTa;
using HLS.Paygate.TopupGw.Components.Connectors.ESale;
using HLS.Paygate.TopupGw.Components.Connectors.HLS;
using HLS.Paygate.TopupGw.Components.Connectors.IRIS;
using HLS.Paygate.TopupGw.Components.Connectors.PayTech;
using HLS.Paygate.TopupGw.Components.Connectors.VDS;
using HLS.Paygate.TopupGw.Components.TopupGwProcess;
using HLS.Paygate.TopupGw.Domains.BusinessServices;
using HLS.Paygate.TopupGw.Domains.Repositories;
using HLS.Paygate.TopupGw.Hosting.Configurations;
using Infrastructure.AppVersion;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ServiceStack;
using ServiceStack.Api.OpenApi;
using ServiceStack.Configuration;
using ServiceStack.Text;
using HostConfig = ServiceStack.HostConfig;
using HLS.Paygate.TopupGw.Components.Connectors.PayPoo;
using HLS.Paygate.TopupGw.Components.Connectors.WhyPay;
using HLS.Paygate.TopupGw.Components.Connectors.Advance;
using HLS.Paygate.TopupGw.Components.Connectors.VTC;
using HLS.Paygate.TopupGw.Components.Connectors.VTC365;
using HLS.Paygate.TopupGw.Components.Connectors.Gate;
using HLS.Paygate.TopupGw.Components.Connectors.ShopeePay;
using HLS.Paygate.TopupGw.Components.Connectors.Vmg;
using HLS.Paygate.TopupGw.Components.Connectors.Vinnet;
using HLS.Paygate.TopupGw.Components.Connectors.Finviet;
using HLS.Paygate.TopupGw.Components.Connectors.VNPTPay;
using HLS.Paygate.TopupGw.Components.Connectors.Vmg2;

[assembly: HostingStartup(typeof(AppHost))]

namespace HLS.Paygate.TopupGw.Hosting.Configurations;

public class AppHost : AppHostBase, IHostingStartup
{
    public AppHost() : base("NT_TopupGw", typeof(MainService).Assembly)
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
        container.RegisterAutoWiredAs<NhatTranStockConnector, IGatewayConnector>(ProviderConst.NHATTRANSTOCK)
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

        SetConfig(new HostConfig
        {
            DefaultContentType = MimeTypes.Json,
            DebugMode = AppSettings.Get(nameof(HostConfig.DebugMode), false),
            UseSameSiteCookies = true,
            GlobalResponseHeaders = new Dictionary<string, string>
            {
                { "Server", "nginx/1.4.7" },
                { "Vary", "Accept" },
                { "X-Powered-By", "NT_TopupGw" }
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