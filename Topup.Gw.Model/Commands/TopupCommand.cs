using Topup.Gw.Model.Dtos;

namespace Topup.Gw.Model.Commands;

public interface TopupCommand : ICommand
{
    SaleItemDto SaleItem { get; }
}