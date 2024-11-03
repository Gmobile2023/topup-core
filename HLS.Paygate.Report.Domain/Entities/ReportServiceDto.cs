using MongoDbGenericRepository.Models;

namespace HLS.Paygate.Report.Domain.Entities;

public class ReportServiceDto : Document
{
    public int ServiceId { get; set; }

    public string ServiceCode { get; set; }

    public string ServiceName { get; set; }
}