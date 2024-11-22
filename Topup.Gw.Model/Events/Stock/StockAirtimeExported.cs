namespace Topup.Gw.Model.Events.Stock;

public interface StockAirtimeExported : IEvent
{
    string StockCode { get; }
    string ProviderCode { get; }
    int Amount { get; }
    string TransRef { get; }
}