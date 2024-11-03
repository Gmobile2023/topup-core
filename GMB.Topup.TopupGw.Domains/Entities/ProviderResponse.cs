using MongoDbGenericRepository.Models;

namespace GMB.Topup.TopupGw.Domains.Entities;

public class ProviderResponse : Document
{
    public string Provider { get; set; }
    public string ResponseCode { get; set; }
    public string ResponseName { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
}