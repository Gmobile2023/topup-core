using HLS.Paygate.Gw.Model.Dtos;
using IEvent = HLS.Paygate.Gw.Model.Events.IEvent;

namespace HLS.Paygate.Stock.Contracts.Events
{
    public interface CardComingEvent : IEvent
    {
        CardRequestDto CardRequest { get; }
    }
}