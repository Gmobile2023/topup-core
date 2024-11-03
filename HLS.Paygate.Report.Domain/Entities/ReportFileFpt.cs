using MongoDbGenericRepository.Models;

namespace HLS.Paygate.Report.Domain.Entities;

public class ReportFileFpt : Document
{
    public string FileName { get; set; }

    public string Type { get; set; }

    public string TextDay { get; set; }
}