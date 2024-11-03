using System;
using System.Collections.Generic;
using HLS.Paygate.Shared;
using ServiceStack;
using ServiceStack.DataAnnotations;

namespace HLS.Paygate.Stock.Contracts.ApiRequests;

#region Nhap kho tu email

[Route("/api/v1/stock/cards-auto-import", "POST")]
public class CardAutoImportRequest : IPost, IReturn<MessageResponseBase>
{
    [Required] public string Vendor { get; set; }

    [Required] public string Provider { get; set; }

    [Required] public string FileName { get; set; }

    public float Discount { get; set; }

    [Required] public List<CardAutoDto> CardItems { get; set; }
}

public class CardAutoDto
{
    public string Serial { get; set; }
    public string Pin { get; set; }
    public string Profile { get; set; }
    public decimal Value { get; set; }
    public DateTime ExpiredDate { get; set; }
}

#endregion

#region Nhap kho file excel backend

[Route("/api/v1/stock/cards-file-import", "POST")]
public class CardImportFileRequest : IPost, IReturn<MessageResponseBase>
{
    [Required] public string Provider { get; set; }

    public string Description { get; set; }

    [Required] public List<CardImportFileItemModel> Data { get; set; }
}

public class CardImportFileItemModel
{
    public string ServiceCode { get; set; }
    public string CategoryCode { get; set; }
    public decimal CardValue { get; set; }
    public int Quantity { get; set; }
    public float Discount { get; set; }
    public List<CardImportFileLineModel> Cards { get; set; }
}

public class CardImportFileLineModel
{
    public string Serial { get; set; }
    public string CardCode { get; set; }
    public DateTime ExpiredDate { get; set; }
}

#endregion

#region update info card and batch

[Route("/api/v1/stock/update_info", "POST")]
public class StockInfoUpdateRequest : IPost, IReturn<MessageResponseBase>
{
    [Required] public string Command { get; set; }
}

#endregion