namespace Topup.Gw.Model.Events.TopupGw;

public interface TopupSubmitted<T> : IEvent
{
    T SaleRequest { get; }
}