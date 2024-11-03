using HLS.Paygate.Gw.Model.Dtos;

namespace HLS.Paygate.Gw.Model.Events;

public interface TopupCommandReceived : IEvent
{
    SaleRequestDto SaleRequest { get; }
}