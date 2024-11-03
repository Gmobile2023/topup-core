using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace GMB.Topup.Common.Model.Dtos;

public class AlarmBalanceConfigDto
{
    public Guid Id { get; set; }
    public int? TenantId { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public string Channel { get; set; }
    public string AccountCode { get; set; }
    public string AccountName { get; set; }
    public decimal MinBalance { get; set; }
    public long TeleChatId { get; set; }
    public string CurrencyCode { get; set; }
    public bool IsRun { get; set; }
}