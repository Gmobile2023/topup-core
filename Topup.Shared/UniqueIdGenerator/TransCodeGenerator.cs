using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ServiceStack.Redis;

namespace Topup.Shared.UniqueIdGenerator;

public class TransCodeGenerator : ITransCodeGenerator
{
    private readonly ILogger<TransCodeGenerator> _logger;
    private readonly IRedisClientsManager _redisClientsManager;

    public TransCodeGenerator(ILogger<TransCodeGenerator> logger, IRedisClientsManager redisClientsManager)
    {
        _logger = logger;
        _redisClientsManager = redisClientsManager;
    }

    // public async Task<string> GenerateCode(string prefix)
    // {
    //     try
    //     {
    //         _logger.LogInformation($"GenerateCode request:{prefix}");
    //         var client = new JsonServiceClient(_configuration["ServiceConfig:GatewayPrivate"]);
    //
    //         var rs= await client.GetAsync(new TranscodeGenerateRequest
    //         {
    //             Key = prefix,
    //             Prefix = prefix
    //         });
    //         _logger.LogInformation($"GenerateCode return:{rs}");
    //         return rs;
    //     }
    //     catch (Exception e)
    //     {
    //         _logger.LogError($"GenerateCode error:{e}");
    //         var code = Guid.NewGuid().ToString().GetHashCode().ToString("x").ToUpper();
    //         var rand = new Random();
    //         var date = DateTime.Now.ToString("yy");
    //         return prefix + date + rand.Next(000000000, 999999999) + code;
    //     }
    // }
    public async Task<string> TransCodeGeneratorAsync(string prefix = "T")
    {
        try
        {
            var dateParam = DateTime.Now.ToString("yyMMdd");
            await using var client = await _redisClientsManager.GetClientAsync();
            var key = $"PayGate_TransCode:Items:{prefix + dateParam}";
            var id = await client.IncrementValueAsync(key);
            if (id == 1) //Bắt đầu ngày mới
            {
                var oldkey = $"PayGate_TransCode:Items:{prefix + DateTime.Now.AddDays(-1).ToString("yyMMdd")}";
                await client.RemoveAsync(oldkey); //xóa key cũ
            }

            return await Task.FromResult(prefix + dateParam + id.ToString().PadLeft(8, '0'));
        }
        catch (Exception ex)
        {
            _logger.LogError("TransCodeGeneratorAsync error: {Error}", ex.Message);
        }

        var rand = new Random();
        var date = DateTime.Now.ToString("yy");
        return await Task.FromResult(prefix + date + rand.Next(000000000, 999999999));
    }

    public async Task<long> IncrementValueAsync(string key)
    {
        await using var client = await _redisClientsManager.GetClientAsync();
        return await client.IncrementValueAsync(key);
    }

    public async Task<long> AutoCloseIndex(string provider, bool success)
    {
        try
        {
            await using var client = await _redisClientsManager.GetClientAsync();
            var key = $"PayGate_AutoCloseIndex:Items:{provider}";
            if (!success)
            {
                var count = await client.IncrementValueAsync(key);
                return count;
            }

            var check = await client.GetValueAsync(key);
            if (check != null && !string.IsNullOrEmpty(check) && int.Parse(check) > 0) await client.RemoveAsync(key);

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError("AutoCloseIndex error: {Error}", ex.Message);
            return 0;
        }
    }

    public async Task ResetAutoCloseIndex(string provider)
    {
        try
        {
            await using var client = await _redisClientsManager.GetClientAsync();
            var key = $"PayGate_AutoCloseIndex:Items:{provider}";
            await client.RemoveAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError("AutoCloseIndex error: {Error}", ex.Message);
        }
    }

    public async Task<int> GetAutoCloseIndex(string provider)
    {
        try
        {
            await using var client = await _redisClientsManager.GetClientAsync();
            var key = $"PayGate_AutoCloseIndex:Items:{provider}";
            var val = await client.GetValueAsync(key);
            return !string.IsNullOrEmpty(val) ? int.Parse(val) : 0;
        }
        catch (Exception ex)
        {
            _logger.LogError("GetAutoCloseIndex error: {Error}", ex.Message);
            return 0;
        }
    }


    public async Task<long> IndexRoundRobinTrans(string providerCode)
    {
        try
        {
            await using var client = await _redisClientsManager.GetClientAsync();
            var key = $"PayGate_RoundRobinIndexTrans:{providerCode}";
            var index = await client.IncrementValueAsync(key);
            if (index <= 5000) return index;
            var oldkey = $"PayGate_RoundRobinIndexTrans:{providerCode}";
            await client.RemoveAsync(oldkey);
            return index;
        }
        catch (Exception ex)
        {
            _logger.LogError("IndexRoundRobinTrans error: {Error}", ex.Message);
            var rdm = new Random();
            return rdm.Next(0, 9999);
        }
    }
}

// [Route("/api/v1/common/transcodegenerate", "GET")]
// public class TranscodeGenerateRequest : IReturn<string>
// {
//     public string Key { get; set; }
//     public string Prefix { get; set; }
// }
//
// [Route("/api/v1/common/trans_code_generate", "GET")]
// public class TranscodeGenerateRequest1 : IReturn<string>
// {
//     public int CodeLength { get; set; }
//     public string Prefix { get; set; }
// }