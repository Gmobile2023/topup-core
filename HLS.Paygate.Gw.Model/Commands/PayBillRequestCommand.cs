using HLS.Paygate.Gw.Model.Dtos;

namespace HLS.Paygate.Gw.Model.Commands;

public interface PayBillRequestCommand : ICommand
{
    SaleRequestDto SaleRequest { get; }
    bool IsInvoice { get; }
}