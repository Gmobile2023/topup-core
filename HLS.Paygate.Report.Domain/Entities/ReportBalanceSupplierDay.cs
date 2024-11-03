using System;
using MongoDbGenericRepository.Models;

namespace HLS.Paygate.Report.Domain.Entities;

public class ReportBalanceSupplierDay : Document
{
    public DateTime CreatedDay { get; set; }
    public string SupplierCode { get; set; }
    public string SupplierName { get; set; }
    public decimal Balance { get; set; }
    public string TextDay { get; set; }
}