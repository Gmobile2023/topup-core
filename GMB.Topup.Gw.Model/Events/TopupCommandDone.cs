using GMB.Topup.Gw.Model.Dtos;

namespace GMB.Topup.Gw.Model.Events;

public interface TopupCommandDone : IEvent
{
    SaleRequestDto SaleRequest { get; }
}