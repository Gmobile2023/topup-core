using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HLS.Paygate.Shared.Utils;
using ServiceStack.Redis;

namespace HLS.Paygate.Shared.CacheManager;

public class CacheManager : ICacheManager
{
    //private static BasicRedisClientManager _redisClientsManager;

    private readonly IRedisClientsManager _redisClientsManager;
    //private IConfiguration Configuration { get; }

    public CacheManager(IRedisClientsManager redisClientsManager)
    {
        _redisClientsManager = redisClientsManager;
    }


    public async Task<string> GetCache(string key)
    {
        await using var client = await _redisClientsManager.GetClientAsync();

        var val = await client.GetValueAsync(key);

        return val;
    }

    public async Task<bool> SetCache(string key, string value, TimeSpan expire)
    {
        await using var client = await _redisClientsManager.GetClientAsync();
        await client.SetAsync(key, value, expire);
        return true;
    }


    public async Task<bool> ClearCache(string key)
    {
        await using var client = await _redisClientsManager.GetClientAsync();
        await client.RemoveAsync(key);
        return true;
    }

    public async Task<bool> ClearAllCache(IEnumerable<string> keys)
    {
        await using var client = await _redisClientsManager.GetClientAsync();
        await client.RemoveAllAsync(keys);
        return true;
    }

    // public async Task<bool> SetCacheObject<T>(string key, T obj, TimeSpan expire)
    // {
    //     await using var client = await _redisClientsManager.GetClientAsync();
    //     await client.SetAsync(key, obj, expire);
    //     return true;
    // }

    public async Task<string> GetAndSetValue(string key, string value)
    {
        await using var client = await _redisClientsManager.GetClientAsync();
        var val = await client.GetAndSetValueAsync(key, value);
        return val;
    }

    // public async Task<T> GetCacheObject<T>(string key)
    // {
    //     await using var client = await _redisClientsManager.GetClientAsync();
    //     return await client.GetAsync<T>(key);
    // }

    public async Task SetFile(string token, byte[] content)
    {
        await using var client = await _redisClientsManager.GetClientAsync();
        await client.SetAsync($"PayGate_File:{token}", content,
            TimeSpan.FromMinutes(1)); // expire time is 1 min by default
    }

    public async Task<byte[]> GetFile(string token)
    {
        await using var client = await _redisClientsManager.GetClientAsync();
        return await client.GetAsync<byte[]>($"PayGate_File:{token}");
    }


    public async Task<bool> AddEntity<T>(string key, T dto, TimeSpan? expire = null)
    {
        await using var redisClient = await _redisClientsManager.GetClientAsync();
        var redis = redisClient.As<T>();
        if (expire == null)
            await redis.SetValueAsync(key, dto);
        else
            await redis.SetValueAsync(key, dto, (TimeSpan) expire);
        return true;
    }

    public async Task<T> GetEntity<T>(string key)
    {
        await using var redisClient = await _redisClientsManager.GetClientAsync();
        var redis = redisClient.As<T>();
        return await redis.GetValueAsync(key);
    }

    public async Task<bool> UpdateEntity<T>(string key, T dto)
    {
        await using var redisClient = await _redisClientsManager.GetClientAsync();
        var redis = redisClient.As<T>();

        await redis.SetValueIfExistsAsync(key, dto);

        return true;
    }

    public async Task<bool> DeleteEntity(string key)
    {
        await using var redisClient = await _redisClientsManager.GetClientAsync();
        await redisClient.RemoveAsync(key);
        return true;
    }

    public async Task<List<T>> GetAllEntityByPattern<T>(string pattern)
    {
        await using var redisClient = await _redisClientsManager.GetClientAsync();
        var key = await redisClient.ScanAllKeysAsync(pattern).ToListAsync();
        return await redisClient.GetValuesAsync<T>(key);
    }

    public async Task<int> GetTotalKeys(string pattern)
    {
        await using var redisClient = await _redisClientsManager.GetClientAsync();
        var key = await redisClient.ScanAllKeysAsync(pattern).ToListAsync();
        return key.Count;
    }
}