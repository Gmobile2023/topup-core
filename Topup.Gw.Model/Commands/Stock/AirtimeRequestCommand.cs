namespace Topup.Gw.Model.Commands.Stock;

public interface AirtimeRequestCommand : ICommand
{
}

public interface StockAirtimeInventoryCommand : ICommand
{
    string ProviderCode { get; }
    string StockCode { get; }
}

public interface StockAirtimeImportCommand : ICommand
{
    string ProviderCode { get; }
    string StockCode { get; }
}

public interface StockAirtimeExportCommand : ICommand
{
    string ProviderCode { get; }
    string StockCode { get; }
}