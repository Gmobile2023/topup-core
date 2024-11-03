namespace HLS.Paygate.Gw.Model.Events;

public interface TopupResultEvent : IEvent
{
    string ResultCode { get; }
    decimal Amount { get; }
}