namespace GMB.Topup.Gw.Model.Events;

public interface PaymentProcessed : IEvent
{
    string ResultMessage { get; }
    string ResultCode { get; }
    string Payload { get; }
}