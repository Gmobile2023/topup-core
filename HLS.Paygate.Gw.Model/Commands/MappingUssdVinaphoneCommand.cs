using HLS.Paygate.Gw.Model.Dtos;

namespace HLS.Paygate.Gw.Model.Commands
{
    public interface MappingUssdVinaphoneCommand : ICommand
    {
        SaleRequestDto SaleRequest { get; }
    }
}