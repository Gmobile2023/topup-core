using ServiceStack.DataAnnotations;

namespace Topup.Stock.Contracts.Enums;

[EnumAsInt]
public enum CardStockStatus : byte
{
    Init = 0,
    Active = 1,
    Lock = 2,
    Delete = 3,
    Undefined = 99
}