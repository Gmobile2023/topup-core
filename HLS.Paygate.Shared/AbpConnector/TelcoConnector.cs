using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ServiceStack;

namespace HLS.Paygate.Shared.AbpConnector;

public class TelcoConnector
{
    private readonly string _apiUrl;
    private readonly ILogger<TelcoConnector> _logger;

    public TelcoConnector(IConfiguration configuration, ILogger<TelcoConnector> logger)
    {
        _logger = logger;
        _apiUrl = configuration["ServiceUrlConfig:GatewayNgate"];
    }

    public async Task<NewMessageReponseBase<string>> CheckPhoneProvider(string phoneNumber)
    {
        try
        {
            _logger.LogInformation($"CheckPhoneProvider request:{phoneNumber}");
            using var client = new JsonServiceClient(_apiUrl);
            client.Timeout = TimeSpan.FromSeconds(5);
            var result = await client.GetAsync(new CheckPhoneProviderRequest
            {
                MobileNumber = phoneNumber
            });
            _logger.LogInformation($"CheckPhoneProvider response:{phoneNumber}-{result.ToJson()}");
            return result;
        }
        catch (Exception e)
        {
            _logger.LogError($"CheckPhoneProvider error:{e}");
            return new NewMessageReponseBase<string>
            {
                ResponseStatus = new ResponseStatusApi("9999", e.Message)
            };
        }
    }
}

[Route("/api/v1/ngate/check_phone_provider_combine_all_channel")]
public class CheckPhoneProviderRequest : IGet, IReturn<NewMessageReponseBase<string>>
{
    public string MobileNumber { get; set; }
}