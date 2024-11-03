using GMB.Topup.Shared;

namespace GMB.Topup.Contracts.Commands.Commissions;

public interface CommissionTransactionCommand : ICommand
{
    string ReceiverInfo { get; set; }
    SaleType SaleType { get; set; }
    Channel Channel { get; set; }
    string PartnerCode { get; set; }
    string ParentCode { get; set; }
    string TransRef { get; set; }
    string ServiceCode { get; set; }
    decimal PaymentAmount { get; set; }
    decimal DiscountAmount { get; set; }
    decimal Amount { get; set; }
    int Quantity { get; set; }
    string ProductCode { get; set; }
    AgentType AgentType { get; set; }
    string CategoryCode { get; set; }
}