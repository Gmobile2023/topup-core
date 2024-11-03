using MongoDbGenericRepository.Models;

namespace HLS.Paygate.TopupGw.Domains.Entities;

public class ProviderReponse : Document
{
    public string Provider { get; set; }
    public string ReponseCode { get; set; }
    public string ReponseName { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
}