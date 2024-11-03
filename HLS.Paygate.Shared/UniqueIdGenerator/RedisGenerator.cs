using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ServiceStack.Redis;

namespace HLS.Paygate.Shared.UniqueIdGenerator;

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

    public async Task<string> GeneratorCodeAsync(string key, string prefix)
    {
        try
        {
            var code = await Task.Run(() =>
            {
                var s = string.Empty;
                using (var client = new BasicRedisClientManager().GetClient())
                {
                    s = (!string.IsNullOrEmpty(prefix) ? prefix : "NT") + DateTime.Now.ToString("yy") +
                        client.IncrementValue(key).ToString("00000000000");
                }

                return s;
            });

            return code;
        }
        catch (Exception)
        {
            var code = Guid.NewGuid().ToString().GetHashCode().ToString("x").ToUpper();
            var rand = new Random();
            var date = DateTime.Now.ToString("yy");
            return prefix + date + rand.Next(000000000, 999999999) + code;
        }
    }

    public string GeneratorCode(string key, string prefix)
    {
        try
        {
            var s = string.Empty;
            using (var client = _redisClientsManager.GetClient())
            {
                s = (!string.IsNullOrEmpty(prefix) ? prefix : "NT") + DateTime.Now.ToString("ddMMyy") +
                    client.IncrementValue(key).ToString("00000000000");
            }

            return s;
        }
        catch (Exception ex)
        {
            _logger.LogError($"GeneratorCode error:{ex}");
            Console.WriteLine($"GeneratorCode error:{ex}");
            var code = Guid.NewGuid().ToString().GetHashCode().ToString("x").ToUpper();
            var rand = new Random();
            var date = DateTime.Now.ToString("yy");
            return prefix + date + rand.Next(000000000, 999999999) + code;
        }
    }
}