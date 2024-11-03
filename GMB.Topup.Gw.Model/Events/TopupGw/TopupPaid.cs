namespace GMB.Topup.Gw.Model.Events.TopupGw;

public interface TopupPaid : IEvent
{
    string PaymentTransCode { get; }
}