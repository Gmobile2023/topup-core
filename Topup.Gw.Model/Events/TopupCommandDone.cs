using Topup.Gw.Model.Dtos;

namespace Topup.Gw.Model.Events;

public interface TopupCommandDone : IEvent
{
    SaleRequestDto SaleRequest { get; }
}