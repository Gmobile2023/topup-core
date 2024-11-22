using Topup.Gw.Model.Dtos;

namespace Topup.Gw.Model.Events;

public interface SaleCommandFailed : IEvent
{
    SaleRequestDto SaleRequest { get; set; }
}