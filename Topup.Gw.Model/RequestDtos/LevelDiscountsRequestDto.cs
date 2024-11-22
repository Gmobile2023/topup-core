using System;
using Topup.Shared;
using ServiceStack;
using ServiceStack.DataAnnotations;

namespace Topup.Gw.Model.RequestDtos;

[Route("/api/v1/discount/levelDiscounts", "GET")]
public class GetLevelDiscountsRequest : PaggingBase, IReturn<MessagePagedResponseBase>
{
    public string TransRef { get; set; }
    public string TransCode { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public LevelDiscountStatus Status { get; set; }
    public string TransAccount { get; set; }
    public string RefAccount { get; set; }
    public string Search { get; set; }
    public string AccountCode { get; set; }
}

[Route("/api/v1/discount/discountAvailable", "GET")]
public class GetDiscountAvailableRequest : IReturn<MessageResponseBase>
{
    [Required] public string AccountCode { get; set; }

    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

[Route("/api/v1/discount/collectDiscount", "POST")]
public class CollectDiscountRequest : IReturn<MessageResponseBase>
{
    public string TransRef { get; set; }
    public string TransCode { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string TransAccount { get; set; }
    public string RefAccount { get; set; }
    public string Search { get; set; }
    public string AccountCode { get; set; }
}