using HLS.Paygate.Gw.Model.Dtos;

namespace HLS.Paygate.Gw.Model.Events;

public interface TopupCommandDone : IEvent
{
    SaleRequestDto SaleRequest { get; }
}