namespace HLS.Paygate.Gw.Model.Events;

public interface TopupCommandProcessed : IEvent
{
    string ResultCode { get; }
    int Amount { get; }
}