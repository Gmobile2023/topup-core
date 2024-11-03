using System;

namespace GMB.Topup.Gw.Model.Events;

public class UpdateAmountTransLimit : IEvent
{
    public decimal Amount { get; set; }
    public Guid CorrelationId { get; set; }
    public DateTime Timestamp { get; set; }
}

public class CheckAmountTransLimit : IEvent
{
    public decimal Amount { get; set; }
    public string AccountCode { get; set; }
    public string ServiceCode { get; set; }
    public string CategoryCode { get; set; }
    public string ProductCode { get; set; }
    public LimitTransType LimitTransType { get; set; }
    public Guid CorrelationId { get; set; }
    public DateTime Timestamp { get; set; }
}

public enum LimitTransType : byte
{
    LimitPerDay = 1,
    LimitPerTrans = 2
}