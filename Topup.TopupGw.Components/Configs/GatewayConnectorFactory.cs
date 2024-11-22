using System;
using HLS.Paygate.Shared;
using HLS.Paygate.TopupGw.Components.Connectors;
using HLS.Paygate.TopupGw.Components.Connectors.Appota;
using HLS.Paygate.TopupGw.Components.Connectors.Card;
using HLS.Paygate.TopupGw.Components.Connectors.Fake;
using HLS.Paygate.TopupGw.Components.Connectors.Imedia;
using HLS.Paygate.TopupGw.Components.Connectors.IOMedia;
using HLS.Paygate.TopupGw.Components.Connectors.MTC;
using HLS.Paygate.TopupGw.Components.Connectors.NhatTran;
using HLS.Paygate.TopupGw.Components.Connectors.Octa;
using HLS.Paygate.TopupGw.Components.Connectors.Viettel;
using HLS.Paygate.TopupGw.Components.Connectors.ZoTa;
using Microsoft.Extensions.DependencyInjection;

namespace HLS.Paygate.TopupGw.Components.Configs
{
    public class GatewayConnectorFactory
    {
        private readonly ProcessingServiceMapper _mapperLookup;

        public GatewayConnectorFactory(IServiceProvider serviceProvider)
        {
            _mapperLookup = serviceProvider.GetService<ProcessingServiceMapper>();
        }

        public IGatewayConnector GetServiceByKey(string key)
        {
            return _mapperLookup(key);
        }
    }

    public delegate IGatewayConnector ProcessingServiceMapper(string key);

    public static class ServiceBuilder
    {
        public static void AddProcessingService(this IServiceCollection services)
        {
            services.AddTransient<ViettelVttConnector>();
            services.AddTransient<ZotaConnector>();
            services.AddTransient<MtcConnector>();
            services.AddTransient<OctaConnector>();
            services.AddTransient<IoMediaConnector>();
            services.AddTransient<ImediaConnector>();
            services.AddTransient<FakeConnector>();
            services.AddTransient<NhattranConnector>();
            services.AddTransient<AppotaConnector>();
            services.AddTransient<CardConnector>();
            services.AddTransient<ProcessingServiceMapper>(provider => key =>
            {
                switch ((string.IsNullOrEmpty(key) ? " " : key).ToUpper())
                {
                    case ProviderConst.VTT:
                        return provider.GetRequiredService<ViettelVttConnector>();
                    case ProviderConst.ZOTA:
                        return provider.GetRequiredService<ZotaConnector>();
                    case ProviderConst.VTT_TEST:
                        return provider.GetRequiredService<ViettelVttConnector>();
                    case ProviderConst.NT_TEST:
                        return provider.GetRequiredService<ZotaConnector>();
                    case ProviderConst.OCTA:
                        return provider.GetRequiredService<OctaConnector>();
                    case ProviderConst.OCTA_TEST:
                        return provider.GetRequiredService<OctaConnector>();
                    case ProviderConst.MTC:
                        return provider.GetRequiredService<MtcConnector>();
                    case ProviderConst.IOMEDIA:
                        return provider.GetRequiredService<IoMediaConnector>();
                    case ProviderConst.IOMEDIA_TEST:
                        return provider.GetRequiredService<IoMediaConnector>();
                    case ProviderConst.FAKE:
                        return provider.GetRequiredService<FakeConnector>();
                    case ProviderConst.IMEDIA:
                        return provider.GetRequiredService<ImediaConnector>();
                    case ProviderConst.IMEDIA_TEST:
                        return provider.GetRequiredService<ImediaConnector>();
                    case ProviderConst.CARD:
                        return provider.GetRequiredService<CardConnector>();
                    case ProviderConst.NHATTRAN:
                        return provider.GetRequiredService<NhattranConnector>();
                    case ProviderConst.APPOTA:
                        return provider.GetRequiredService<AppotaConnector>();
                    default:
                        return null;
                }
            });

            services.AddTransient<GatewayConnectorFactory>();
        }
    }
}
