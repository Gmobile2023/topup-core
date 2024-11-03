using GMB.Topup.Gw.Model.Dtos;

namespace GMB.Topup.Gw.Model.Events;

public interface SaleCommandFailed : IEvent
{
    SaleRequestDto SaleRequest { get; set; }
}