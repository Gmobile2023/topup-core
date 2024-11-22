using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Topup.Shared;
using ServiceStack;

namespace Topup.Discovery.Requests.Backends;

[Route("/api/v1/card-import-provider", "POST")]
public class CardImportProviderRequest : IPost, IReturn<NewMessageResponseBase<string>>
{
    [Required] public string PartnerCode { get; set; }

    public string ProviderCode { get; set; }
    public List<BatchItem> CardItems { get; set; }
    public Channel Channel { get; set; }

    public DateTime? ExpiredDate { get; set; }
    public string Description { get; set; }
}

public class BatchItem
{
    [Required] public string ServiceCode { get; set; }
    [Required] public string CategoryCode { get; set; }
    [Required] public int Quantity { get; set; }
    [Required] public int CardValue { get; set; }
    [Required] public string ProductCode { get; set; }
    [Required] public decimal Discount { get; set; }
}