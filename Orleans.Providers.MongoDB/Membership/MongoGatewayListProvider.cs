using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Orleans.Configuration;
using Orleans.Messaging;
using Orleans.Providers.MongoDB.Configuration;
using Orleans.Providers.MongoDB.Membership.Store;
using Orleans.Providers.MongoDB.Utils;

// ReSharper disable ConvertToLambdaExpression

namespace Orleans.Providers.MongoDB.Membership;

public sealed class MongoGatewayListProvider : IGatewayListProvider
{
    private readonly string _clusterId;
    private readonly ILogger<MongoGatewayListProvider> _logger;
    private readonly IMongoClient _mongoClient;
    private readonly MongoDBGatewayListProviderOptions _options;
    private IMongoMembershipCollection _gatewaysCollection;

    public MongoGatewayListProvider(
        IMongoClientFactory mongoClientFactory,
        ILogger<MongoGatewayListProvider> logger,
        IOptions<ClusterOptions> clusterOptions,
        IOptions<GatewayOptions> gatewayOptions,
        IOptions<MongoDBGatewayListProviderOptions> options)
    {
        _mongoClient = mongoClientFactory.Create(options.Value, "Membership");
        _logger = logger;
        _options = options.Value;
        _clusterId = clusterOptions.Value.ClusterId;
        MaxStaleness = gatewayOptions.Value.GatewayListRefreshPeriod;
    }

    /// <inheritdoc />
    public bool IsUpdatable { get; } = true;

    /// <inheritdoc />
    public TimeSpan MaxStaleness { get; }

    /// <inheritdoc />
    public Task InitializeGatewayListProvider()
    {
        CreateCollection();

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IList<Uri>> GetGateways()
    {
        return DoAndLog(nameof(GetGateways), () => { return _gatewaysCollection.GetGateways(_clusterId); });
    }

    private void CreateCollection()
    {
        _gatewaysCollection = Factory.CreateCollection(_mongoClient, _options, _options.Strategy);
    }

    private async Task<T> DoAndLog<T>(string actionName, Func<Task<T>> action)
    {
        _logger.LogInformation($"{nameof(MongoGatewayListProvider)}.{actionName} called.");

        try
        {
            return await action();
        }
        catch (Exception ex)
        {
            _logger.LogWarning((int) MongoProviderErrorCode.MembershipTable_Operations, ex,
                $"{nameof(MongoGatewayListProvider)}.{actionName} failed. Exception={ex.Message}");

            throw;
        }
    }
}