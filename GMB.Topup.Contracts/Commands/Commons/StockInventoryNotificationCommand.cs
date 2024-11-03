namespace GMB.Topup.Contracts.Commands.Commons;

public interface StockInventoryNotificationCommand : ICommand
{
    int Inventory { get; set; }
    string StockCode { get; set; }
    string Vendor { get; set; }
    int CardValue { get; set; }
    string ProductCode { get; set; }
    string NotifiType { get; set; }
}