using MongoDbGenericRepository.Models;

namespace Topup.Report.Domain.Entities;

public class ReportServiceDto : Document
{
    public int ServiceId { get; set; }

    public string ServiceCode { get; set; }

    public string ServiceName { get; set; }
}