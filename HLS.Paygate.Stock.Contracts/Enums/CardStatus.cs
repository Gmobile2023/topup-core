using ServiceStack.DataAnnotations;

namespace HLS.Paygate.Stock.Contracts.Enums;

[EnumAsInt]
public enum CardStatus : byte
{
    Init = 0,
    Active = 1,
    Exported = 2,
    Delete = 3,
    Cancelled = 4,
    OnExchangeMode = 11,
    Undefined = 99
}