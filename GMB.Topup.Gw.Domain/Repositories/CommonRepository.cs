using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ServiceStack.Redis;

namespace GMB.Topup.Gw.Domain.Repositories;

public class CommonRepository : ICommonRepository
{
    //private readonly IPaygateConnectionFactory _paygateConnectionFactory;

    //private readonly Logger _logger = LogManager.GetLogger("CommonRepository");
    private readonly ILogger<CommonRepository> _logger;
    private readonly IRedisClientsManager _redisClientsManager;


    public CommonRepository( //IPaygateConnectionFactory paygateConnectionFactory,
        IRedisClientsManager redisClientsManager, ILogger<CommonRepository> logger)
    {
        // _paygateConnectionFactory = paygateConnectionFactory;
        _redisClientsManager = redisClientsManager;
        _logger = logger;
    }

    // public async Task<string> TransCodeGeneratorAsync(string prefix = "T")
    // {
    //     try
    //     {
    //         using var db = await _paygateConnectionFactory.OpenAsync();
    //         var id = await db.InsertAsync(new AbpGeneratorIds()
    //         {
    //             Type = prefix,
    //             Order = 1
    //         }, true);
    //         var code = prefix + DateTime.Now.ToString("yyMMdd") + id.ToString("0000000");
    //         return code;
    //     }
    //     catch (Exception ex)
    //     {
    //         _logger.LogError("TransCodeGeneratorAsync error: {Error}", ex.Message);
    //         var code = Guid.NewGuid().ToString().GetHashCode().ToString("x").ToUpper();
    //         var rand = new Random();
    //         var date = DateTime.Now.ToString("yy");
    //         return prefix + date + rand.Next(000000000, 999999999) + code;
    //     }
    // }

    public async Task<string> TransCodeGeneratorAsync(string provider, int codeLength, string prefix = "T")
    {
        try
        {
            if (codeLength <= 0)
                codeLength = 8;
            //provider = "DB";
            var dateParam = DateTime.Now.ToString("yyMMdd");
            // if (provider == "DB")
            // {
            //     using var db = await _paygateConnectionFactory.OpenAsync();
            //     var id = await db.InsertAsync(new AbpGeneratorIds()
            //     {
            //         Type = prefix,
            //         Order = 1
            //     }, true);
            //     return prefix + dateParam + id.ToString().PadLeft(codeLength, '0');
            // }

            if (string.IsNullOrEmpty(provider))
                provider = "REDIS";

            if (provider == "REDIS")
            {
                await using var client = await _redisClientsManager.GetClientAsync();
                var id = await client.IncrementAsync(prefix + dateParam, 1);
                if (id == 1) //Bắt đầu ngày mới
                    await client.RemoveEntryAsync(prefix + DateTime.Now.AddDays(-1).ToString("yyMMdd")); //xóa key cũ
                return prefix + dateParam + id.ToString().PadLeft(codeLength, '0');
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("TransCodeGeneratorAsync error: {Error}", ex.Message);
        }

        var code = Guid.NewGuid().ToString().GetHashCode().ToString("x").ToUpper();
        var rand = new Random();
        var date = DateTime.Now.ToString("yy");
        return prefix + date + rand.Next(000000000, 999999999) + code;
    }
}