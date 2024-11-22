using MongoDbGenericRepository.Models;

namespace Topup.Report.Domain.Entities;

public class ReportProviderDto : Document
{
    public int ProviderId { get; set; }
    public string ProviderCode { get; set; }
    public string ProviderName { get; set; }
}

public class ReportVenderDto : Document
{
    public int VenderId { get; set; }
    public string VenderCode { get; set; }
    public string VenderName { get; set; }    
}
