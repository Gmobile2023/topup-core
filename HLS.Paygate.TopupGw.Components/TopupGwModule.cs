using Autofac;
using HLS.Paygate.Shared;
using HLS.Paygate.TopupGw.Components.Connectors;
using HLS.Paygate.TopupGw.Components.Connectors.Card;
using HLS.Paygate.TopupGw.Components.Connectors.Fake;
using HLS.Paygate.TopupGw.Components.Connectors.Imedia;
using HLS.Paygate.TopupGw.Components.Connectors.IOMedia;
using HLS.Paygate.TopupGw.Components.Connectors.Mobifone;
using HLS.Paygate.TopupGw.Components.Connectors.MTC;
using HLS.Paygate.TopupGw.Components.Connectors.NhatTran;
using HLS.Paygate.TopupGw.Components.Connectors.Octa;
using HLS.Paygate.TopupGw.Components.Connectors.Payoo;
using HLS.Paygate.TopupGw.Components.Connectors.PayTech;
using HLS.Paygate.TopupGw.Components.Connectors.Viettel2;
using HLS.Paygate.TopupGw.Components.Connectors.WPay;
using HLS.Paygate.TopupGw.Components.Connectors.ZoTa;
using HLS.Paygate.TopupGw.Components.Connectors.ESale;
using HLS.Paygate.TopupGw.Components.Connectors.HLS;
using HLS.Paygate.TopupGw.Components.Connectors.VDS;

namespace HLS.Paygate.TopupGw.Components;

public class TopupGwModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        // builder.RegisterModule<DomainModule>();
        // builder.RegisterModule<ReportModule>();
        //
        // builder.RegisterType<GateApp>();
        // builder.RegisterType<TopupService>();
        // builder.RegisterType<ProcessService>().AsSelf();
        // builder.RegisterType<QueryProcess>().AsSelf();
        // builder.RegisterType<SchedulerProcess>().AsSelf();
        // builder.RegisterType<ElasticSearch>().As<IElasticSearch>();
        // builder.RegisterType<ElasticSearchException>().AsSelf();

        // builder.RegisterType<TopupGatewayService>().As<ITopupGatewayService>();
        // builder.RegisterType<ITransRepository>().As<TransRepository>();
        // builder.RegisterType<DateTimeHelper>().As<IDateTimeHelper>();
        // builder.RegisterType<CacheManager>().As<ICacheManager>();
        // builder.RegisterType<CommonService>().As<ICommonService>();
        // builder.RegisterType<CommonRepository>().As<ICommonRepository>();
        // builder.RegisterType<TopupGwProcess.TopupGwProcess>().As<ITopupGwProcess>();

        // builder.RegisterType<ViettelVttConnector>().Keyed<IGatewayConnector>(ProviderConst.VTT);
        // builder.RegisterType<ViettelVttConnector>().Keyed<IGatewayConnector>(ProviderConst.VTT_TEST);
        builder.RegisterType<ViettelVtt2Connector>().Keyed<IGatewayConnector>(ProviderConst.VTT2);
        builder.RegisterType<ViettelVtt2Connector>().Keyed<IGatewayConnector>(ProviderConst.VTT2_TEST);
        builder.RegisterType<ZotaConnector>().Keyed<IGatewayConnector>(ProviderConst.ZOTA);
        builder.RegisterType<ZotaConnector>().Keyed<IGatewayConnector>(ProviderConst.ZOTA_TEST);
        builder.RegisterType<OctaConnector>().Keyed<IGatewayConnector>(ProviderConst.OCTA);
        builder.RegisterType<OctaConnector>().Keyed<IGatewayConnector>(ProviderConst.OCTA_TEST);
        builder.RegisterType<MtcConnector>().Keyed<IGatewayConnector>(ProviderConst.MTC);
        builder.RegisterType<IoMediaConnector>().Keyed<IGatewayConnector>(ProviderConst.IOMEDIA);
        builder.RegisterType<IoMediaConnector>().Keyed<IGatewayConnector>(ProviderConst.IOMEDIA_TEST);
        builder.RegisterType<FakeConnector>().Keyed<IGatewayConnector>(ProviderConst.FAKE);
        builder.RegisterType<ImediaConnector>().Keyed<IGatewayConnector>(ProviderConst.IMEDIA);
        builder.RegisterType<ImediaConnector>().Keyed<IGatewayConnector>(ProviderConst.IMEDIA_TEST);
        builder.RegisterType<CardConnector>().Keyed<IGatewayConnector>(ProviderConst.CARD);
        builder.RegisterType<NhattranConnector>().Keyed<IGatewayConnector>(ProviderConst.NHATTRAN);
        builder.RegisterType<NhatTranStockConnector>().Keyed<IGatewayConnector>(ProviderConst.NHATTRANSTOCK);
        builder.RegisterType<WPayConnector>().Keyed<IGatewayConnector>(ProviderConst.WPAY);
        builder.RegisterType<PayooConnector>().Keyed<IGatewayConnector>(ProviderConst.PAYOO);
        builder.RegisterType<MobifoneConnector>().Keyed<IGatewayConnector>(ProviderConst.MOBIFONE);
        //builder.RegisterType<Imedia2Connector>().Keyed<IGatewayConnector>(ProviderConst.IMEDIA2);
        builder.RegisterType<PayTechConnector>().Keyed<IGatewayConnector>(ProviderConst.PAYTECH);
        builder.RegisterType<ESaleConnector>().Keyed<IGatewayConnector>(ProviderConst.ESALE);
        builder.RegisterType<HLSConnector>().Keyed<IGatewayConnector>(ProviderConst.HLS);
        builder.RegisterType<VDSConnector>().Keyed<IGatewayConnector>(ProviderConst.VDS);
        base.Load(builder);
    }
}