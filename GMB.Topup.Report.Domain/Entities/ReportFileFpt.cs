using MongoDbGenericRepository.Models;

namespace GMB.Topup.Report.Domain.Entities;

public class ReportFileFpt : Document
{
    public string FileName { get; set; }

    public string Type { get; set; }

    public string TextDay { get; set; }
}