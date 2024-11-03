using GMB.Topup.Gw.Model.Dtos;

namespace GMB.Topup.Gw.Model.Commands;

public interface PayBillRequestCommand : ICommand
{
    SaleRequestDto SaleRequest { get; }
    bool IsInvoice { get; }
}