using System;
using Topup.Shared;
using ServiceStack;
using Topup.Stock.Contracts.Enums;

namespace Topup.Stock.Contracts.ApiRequests;

[Route("/api/v1/stock/cards", "GET")]
public class CardGetList : PaggingBase, IGet, IReturn<MessagePagedResponseBase>
{
    public string Filter { get; set; }
    public string BatchCode { get; set; }
    public string StockCode { get; set; }

    public string Serial { get; set; }
    public string CardCode { get; set; }

    public string ProviderCode { get; set; }
    public string ServiceCode { get; set; }
    public string CategoryCode { get; set; }
    public string ProductCode { get; set; }

    public DateTime? FromExpiredDate { get; set; }
    public DateTime? ToExpiredDate { get; set; }

    public DateTime? FromImportDate { get; set; }
    public DateTime? ToImportDate { get; set; }

    public DateTime? FromExportedDate { get; set; }
    public DateTime? ToExportedDate { get; set; }

    public int? FormCardValue { get; set; }
    public int? ToCardValue { get; set; }


    public CardStatus Status { get; set; }
}