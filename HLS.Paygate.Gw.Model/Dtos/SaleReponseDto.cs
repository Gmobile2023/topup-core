using HLS.Paygate.Shared;

namespace HLS.Paygate.Gw.Model.Dtos;

public class SaleReponseDto
{
    public SaleRequestDto Sale { get; set; }
    public SaleRequestStatus Status { get; set; }
    public decimal Balance { get; set; }
    public int NextStep { get; set; }
    public string FeeDto { get; set; }
}