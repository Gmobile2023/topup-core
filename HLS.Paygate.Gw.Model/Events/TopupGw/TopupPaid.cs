namespace HLS.Paygate.Gw.Model.Events.TopupGw;

public interface TopupPaid : IEvent
{
    string PaymentTransCode { get; }
}