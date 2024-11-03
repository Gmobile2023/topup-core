using HLS.Paygate.Gw.Model.Dtos;

namespace HLS.Paygate.Gw.Model.Commands;

public interface TopupCommand : ICommand
{
    SaleItemDto SaleItem { get; }
}