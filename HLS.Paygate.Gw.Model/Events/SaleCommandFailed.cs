using HLS.Paygate.Gw.Model.Dtos;

namespace HLS.Paygate.Gw.Model.Events;

public interface SaleCommandFailed : IEvent
{
    SaleRequestDto SaleRequest { get; set; }
}