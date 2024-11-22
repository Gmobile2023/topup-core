using MongoDbGenericRepository.Models;
using System;

namespace Topup.Report.Domain.Entities;

public class TopupRequestLog : Document
{
    public string TransCode { get; set; }
    public string TransRef { get; set; }
    public decimal TransAmount { get; set; }
    public string ReceiverInfo { get; set; }
    public int Status { get; set; }
    public DateTime RequestDate { get; set; }
    public DateTime ModifiedDate { get; set; }
    public string Vendor { get; set; }
    public string CategoryCode { get; set; }
    public string ProductCode { get; set; }
    public string ProviderCode { get; set; }
    public string ResponseInfo { get; set; }
    public string ServiceCode { get; set; }
    public string PartnerCode { get; set; }
    public string ReferenceCode { get; set; }
    public string TransIndex { get; set; }
    public int? ProviderSetTransactionTimeout { get; set; }
    public int? ProviderMaxWaitingTimeout { get; set; }
    public bool? IsEnableResponseWhenJustReceived { get; set; }
    public string StatusResponseWhenJustReceived { get; set; }
    public int? WaitingTimeResponseWhenJustReceived { get; set; }
}