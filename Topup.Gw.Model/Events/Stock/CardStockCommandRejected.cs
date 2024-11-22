namespace Topup.Gw.Model.Events.Stock;

public interface CardStockCommandRejected : IEvent
{
    //Guid Id { get; set; }
    string Reason { get; set; }

    //DateTime Timestamp { get; set; }
    string Code { get; set; }
}