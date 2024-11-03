using HLS.Paygate.Shared;
using ServiceStack;

namespace HLS.Paygate.Stock.Contracts.ApiRequests;

[Route("/api/v1/stock/card-stock", "POST")]
public class CardStockCreateRequest : IPost, IReturn<MessageResponseBase>
{
    public string StockCode { get; set; }
    public string Vendor { get; set; }
    public decimal CardValue { get; set; }
    public int InventoryLimit { get; set; }
    public int MinimumInventoryLimit { get; set; }
    public string Description { get; set; }
}

[Route("/api/v1/stock/card-stock", "PUT")]
public class CardStockUpdateRequest : IPut, IReturn<MessageResponseBase>
{
    public string StockCode { get; set; }
    public string ProductCode { get; set; }
    public decimal CardValue { get; set; }
    public int InventoryLimit { get; set; }
    public int MinimumInventoryLimit { get; set; }
    public string Description { get; set; }
}

[Route("/api/v1/stock/card-stock-quantity", "PUT")]
public class UpdateInventoryRequest : IPut, IReturn<MessageResponseBase>
{
    public string StockCode { get; set; }
    public string KeyCode { get; set; }
    public int Inventory { get; set; }
}

/// <summary>
///     chi tiết kho
/// </summary>
[Route("/api/v1/stock/card-stock", "GET")]
public class CardStockGetRequest : IGet, IReturn<MessageResponseBase>
{
    [ApiMember(ExcludeInSchema = false, IsRequired = true, Description = "Mã kho")]
    public string StockCode { get; set; }

    [ApiMember(ExcludeInSchema = false, IsRequired = true, Description = "Nhà mạng - dịch vụ")]
    public string ProductCode { get; set; }
}

/// <summary>
///     danh sách kho
/// </summary>
[Route("/api/v1/stock/card-stock-list", "GET")]
public class CardStockGetListRequest : PaggingBase, IGet, IReturn<MessagePagedResponseBase>
{
    [ApiMember(ExcludeInSchema = false, Description = "Search Text")]
    public string Filter { get; set; }

    [ApiMember(ExcludeInSchema = false, Description = "Mã kho")]
    public string StockCode { get; set; }

    [ApiMember(ExcludeInSchema = false, Description = "Mệnh giá min")]
    public int MinCardValue { get; set; }

    [ApiMember(ExcludeInSchema = false, Description = "Mệnh giá max")]
    public int MaxCardValue { get; set; }

    [ApiMember(ExcludeInSchema = false, Description = "Dịch vụ")]
    public string ServiceCode { get; set; }

    [ApiMember(ExcludeInSchema = false, Description = "Loại sản phẩm")]
    public string CategoryCode { get; set; }

    [ApiMember(ExcludeInSchema = false, Description = "Sản phẩm")]
    public string ProductCode { get; set; }

    [ApiMember(ExcludeInSchema = false, Description = "Trạng thái")]
    public byte Status { get; set; }
}