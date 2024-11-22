using Topup.Gw.Model.Dtos;

namespace Topup.Gw.Model.Commands;

public interface PayBillRequestCommand : ICommand
{
    SaleRequestDto SaleRequest { get; }
    bool IsInvoice { get; }
}