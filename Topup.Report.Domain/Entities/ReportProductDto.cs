using MongoDbGenericRepository.Models;

namespace Topup.Report.Domain.Entities;

public class ReportProductDto : Document
{
    public int ProductId { get; set; }
    public string ProductCode { get; set; }
    public string ProductName { get; set; }
    public int CategoryId { get; set; }
    public string CategoryCode { get; set; }
    public string CategoryName { get; set; }
    public string ServiceCode { get; set; }
    public string ServiceName { get; set; }

    public decimal ProductValue { get; set; }
    public int ServiceId { get; set; }
}