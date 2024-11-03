using System;
using Elasticsearch.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nest;
using Nest.JsonNetSerializer;

namespace Infrastructure.ElasticSeach;

public static class ElasticsearchExtensions
{
    public static void AddElasticsearch(this IServiceCollection services, IConfiguration configuration)
    {
        var config = new ElasticSearchConfig();
        configuration.GetSection("ElasticSearch").Bind(config);

        var pool = new SingleNodeConnectionPool(new Uri(config.Url));
        var connectionSettings =
            new ConnectionSettings(pool, JsonNetSerializer.Default).BasicAuthentication(config.UserName,
                config.Password);
        connectionSettings.ThrowExceptions();
        connectionSettings.PrettyJson();
        connectionSettings.DisableDirectStreaming();
        connectionSettings.DefaultFieldNameInferrer(p => p);
        var client = new ElasticClient(connectionSettings);
        services.AddSingleton(client);
    }
}