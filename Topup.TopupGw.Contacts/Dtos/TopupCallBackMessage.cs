using System;
using Topup.Gw.Model.Events;

namespace Topup.TopupGw.Contacts.Dtos;

public class TopupCallBackMessage : IEvent
{
    public int Status { get; set; }
    public decimal Amount { get; set; }
    public string TransCode { get; set; }
    public string ProviderCode { get; set; }
    public Guid CorrelationId { get; set; }
    public DateTime Timestamp { get; set; }
}