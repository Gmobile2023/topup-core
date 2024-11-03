using HLS.Paygate.Gw.Model.Dtos;

namespace HLS.Paygate.Gw.Model.Commands;

public interface TopupRequestProcessCommand : ICommand
{
    SaleRequestDto SaleRequest { get; }
}
// public interface TopupFulfillRequestCommand : ICommand
// {
//     SaleRequestDto SaleRequest { get; }
// }