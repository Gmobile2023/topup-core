using System;
using MongoDbGenericRepository.Models;

namespace GMB.Topup.Report.Domain.Entities;

public class CompareTime : Document
{
    public string AccountCode { get; set; }

    public string TransCode { get; set; }

    public DateTime TransDate { get; set; }

    public string TransDateSoft { get; set; }

    public string CompareDateSoft { get; set; }

    public decimal SysValue { get; set; }

    public decimal ProviderValue { get; set; }

    public int Status { get; set; }

    public int Result { get; set; }

    public bool? IsRefund { get; set; }

    public string ProductCode { get; set; }

    public string ProductName { get; set; }

    public string ReceivedAccount { get; set; }

    public string TransCodePay { get; set; }

    public DateTime CompareDate { get; set; }

    public decimal Amount { get; set; }

    public string ProviderCode { get; set; }

    public DateTime? RefunDate { get; set; }

    public string TranCodeRefund { get; set; }

    public string KeyCode { get; set; }
}