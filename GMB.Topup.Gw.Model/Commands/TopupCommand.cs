using GMB.Topup.Gw.Model.Dtos;

namespace GMB.Topup.Gw.Model.Commands;

public interface TopupCommand : ICommand
{
    SaleItemDto SaleItem { get; }
}