namespace HLS.Paygate.Gw.Model.Events.TopupGw;

public interface TopupCompleted : IEvent
{
    string Result { get; }
    string Message { get; }
}