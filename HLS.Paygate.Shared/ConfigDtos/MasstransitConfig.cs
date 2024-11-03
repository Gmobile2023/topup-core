using System.Collections.Generic;

namespace HLS.Paygate.Shared.ConfigDtos;

public class MassTransitConfig
{
    public bool IsUseGrpc { get; set; }
    public RabbitMqConfig RabbitMqConfig { get; set; }
    public GrpcConfig GrpcConfig { get; set; }
}

public class GrpcConfig
{
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 19797;
    public bool AddServer { get; set; } = false;
    public List<string> Servers { get; set; } = new List<string> { "http://127.0.0.1:19796" };
}

public class RabbitMqConfig
{
    public string Host { get; set; } = "localhost";
    public string VirtualHost { get; set; } = "/";
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string Clusters { get; set; }
}