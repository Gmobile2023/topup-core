namespace Infrastructure.RedisSentinel;

public class SentinelConfigDto
{
    public string Server { get; set; }
    public string MasterName { get; set; }
    public string RedisServer { get; set; }
}