using System;
using HLS.Paygate.Gw.Model.Events;

namespace HLS.Paygate.TopupGw.Contacts.Dtos;

public class TopupCallBackMessage : IEvent
{
    public int Status { get; set; }
    public decimal Amount { get; set; }
    public string TransCode { get; set; }
    public string ProviderCode { get; set; }
    public Guid CorrelationId { get; set; }
    public DateTime Timestamp { get; set; }
}