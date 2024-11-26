using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ServiceStack.Redis;

namespace Topup.Shared.UniqueIdGenerator;

public class RedisGenerator
{
    private static BasicRedisClientManager _redisClientsManager;

    //private readonly ILog _logger = LogManager.GetLogger("RedisGenerator");
    private readonly ILogger<RedisGenerator> _logger;

    public RedisGenerator(ILogger<RedisGenerator> logger)
    {
        _logger = logger;
        if (_redisClientsManager == null)
            _redisClientsManager = new BasicRedisClientManager();
        //_redisClientsManager = redisClientsManager;
    }
}