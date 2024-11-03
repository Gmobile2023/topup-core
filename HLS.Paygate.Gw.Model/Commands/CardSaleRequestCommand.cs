using HLS.Paygate.Gw.Model.Dtos;

namespace HLS.Paygate.Gw.Model.Commands
{
    public interface CardSaleRequestCommand : ICommand
    {
        SaleRequestDto SaleRequest { get; }
        int CardValue { get; }
    }
}