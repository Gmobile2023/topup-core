using ServiceStack.DataAnnotations;

namespace HLS.Paygate.Stock.Contracts.Enums;

[EnumAsInt]
public enum StockTransStatus : byte
{
    Init = 0,
    Active = 1,
    Delete = 3,
    Undefined = 99
}