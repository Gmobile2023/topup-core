using GMB.Topup.Gw.Model.Dtos;

namespace GMB.Topup.Gw.Model.Events;

public interface TopupCommandReceived : IEvent
{
    SaleRequestDto SaleRequest { get; }
}