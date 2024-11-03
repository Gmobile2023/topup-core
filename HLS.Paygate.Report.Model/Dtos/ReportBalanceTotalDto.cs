using System;

namespace HLS.Paygate.Report.Model.Dtos;

public class ReportBalanceTotalDto
{
    public string AccountCode { get; set; }
    public string AccountType { get; set; }
    public double Credited { get; set; }
    public double Credite { get; set; }
    public double Debit { get; set; }
    public double BalanceBefore { get; set; }
    public double BalanceAfter { get; set; }
    public DateTime CreatedDay { get; set; }
}