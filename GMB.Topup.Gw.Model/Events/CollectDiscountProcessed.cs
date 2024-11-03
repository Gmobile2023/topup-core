namespace GMB.Topup.Gw.Model.Events;

public interface CollectDiscountProcessed : IEvent
{
    string ResultMessage { get; }
    string ResultCode { get; }
    string Payload { get; }
}