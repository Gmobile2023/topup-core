using ServiceStack.DataAnnotations;

namespace HLS.Paygate.Stock.Contracts.Enums;

[EnumAsInt]
public enum StockBatchStatus : byte
{
    Init = 0,
    Active = 1,
    Lock = 2,
    Delete = 3,
    Undefined = 99
}

// [EnumAsInt]
// public enum CardBatchType : byte
// {
//     Default = 99,
//     CardSale = 1,//Loại thẻ chỉ bán
//     CardMapping = 2,//Loại thẻ chỉ ghép
//     MappingCanSale = 3//Loại thẻ ghép nhưng có thể bán
// }