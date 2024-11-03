namespace GMB.Topup.Gw.Model.Events.Stock;

public interface StockAirtimeImported : IEvent
{
    string StockCode { get; }
    string ProviderCode { get; }
    int Amount { get; }
    string TransRef { get; }
}