using Topup.Gw.Model.Dtos;

namespace Topup.Gw.Model.Events;

public interface TopupCommandReceived : IEvent
{
    SaleRequestDto SaleRequest { get; }
}