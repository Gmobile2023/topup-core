namespace GMB.Topup.Gw.Model.Events.Stock;

public interface CardStockCommandSubmitted<T> : IEvent
{
    // Guid Id { get; }
    // DateTime Timestamp { get; }
    T Payload { get; set; }
}