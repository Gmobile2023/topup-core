using System;
using System.Net.Http;
using GMB.Topup.Shared.ConfigDtos;
using Grpc.Net.Client;
using Microsoft.Extensions.Configuration;
using ServiceStack;

namespace GMB.Topup.Shared.Helpers;

public class GrpcClientHepper
{
    private readonly ServiceUrlConfig _serviceUrlConfig;

    public GrpcClientHepper(IConfiguration configuration)
    {
        _serviceUrlConfig = new ServiceUrlConfig();
        configuration.GetSection("ServiceUrlConfig").Bind(_serviceUrlConfig);
    }

    public GrpcServiceClient GetClient(string serviceName)
    {
        var url = string.Empty;
        if (serviceName == GrpcServiceName.Backend)
            url = _serviceUrlConfig.GrpcServices.Backend;
        if (serviceName == GrpcServiceName.Balance)
            url = _serviceUrlConfig.GrpcServices.Balance;
        if (serviceName == GrpcServiceName.Commission)
            url = _serviceUrlConfig.GrpcServices.Commission;
        if (serviceName == GrpcServiceName.Common)
            url = _serviceUrlConfig.GrpcServices.Common;
        if (serviceName == GrpcServiceName.Report)
            url = _serviceUrlConfig.GrpcServices.Report;
        if (serviceName == GrpcServiceName.Stock)
            url = _serviceUrlConfig.GrpcServices.Stock;
        if (serviceName == GrpcServiceName.TopupGateway)
            url = _serviceUrlConfig.GrpcServices.TopupGateway;
        if (serviceName == GrpcServiceName.Worker)
            url = _serviceUrlConfig.GrpcServices.Worker;
        if (serviceName == GrpcServiceName.MobileInfo)
            url = _serviceUrlConfig.GrpcServices.MobileInfo;
        return new GrpcServiceClient(url);
    }

    public GrpcServiceClient GetClientCluster(string serviceName, int timeoutSeconds = 0)
    {
        var url = string.Empty;
        if (serviceName == GrpcServiceName.Backend)
            url = _serviceUrlConfig.GrpcServices.Backend;
        if (serviceName == GrpcServiceName.Balance)
            url = _serviceUrlConfig.GrpcServices.Balance;
        if (serviceName == GrpcServiceName.Commission)
            url = _serviceUrlConfig.GrpcServices.Commission;
        if (serviceName == GrpcServiceName.Common)
            url = _serviceUrlConfig.GrpcServices.Common;
        if (serviceName == GrpcServiceName.Report)
            url = _serviceUrlConfig.GrpcServices.Report;
        if (serviceName == GrpcServiceName.Stock)
            url = _serviceUrlConfig.GrpcServices.Stock;
        if (serviceName == GrpcServiceName.TopupGateway)
            url = _serviceUrlConfig.GrpcServices.TopupGateway;
        if (serviceName == GrpcServiceName.Worker)
            url = _serviceUrlConfig.GrpcServices.Worker;
        if (serviceName == GrpcServiceName.MobileInfo)
            url = _serviceUrlConfig.GrpcServices.MobileInfo;

        return new GrpcServiceClient(new GrpcClientConfig
        {
            BaseUri = url,
            Channel = GrpcChannel.ForAddress(url, new GrpcChannelOptions
            {
                HttpClient = new HttpClient
                {
                    Timeout = timeoutSeconds > 0 ? TimeSpan.FromSeconds(timeoutSeconds) : TimeSpan.FromMinutes(15)
                },
                MaxReceiveMessageSize = 500 * 1024 * 1024, // 500 MB
                MaxSendMessageSize = 500 * 1024 * 1024 // 500 MB
            })
        });


        // var channelCluster = GrpcChannel.ForAddress(url, new GrpcChannelOptions
        // {
        //     Credentials = Grpc.Core.ChannelCredentials.Insecure,
        //     ServiceConfig = new ServiceConfig
        //     {
        //         LoadBalancingConfigs =
        //         {
        //             new RoundRobinConfig()
        //         }
        //     }
        // });
        // return new GrpcServiceClient(channelCluster);
    }
}