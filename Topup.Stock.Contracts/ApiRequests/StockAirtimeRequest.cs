using System;
using Topup.Shared;
using ServiceStack;

namespace Topup.Stock.Contracts.ApiRequests;

// longld
// search list stock airtime
[Route("/api/v1/stock/airtime-list", "GET")]
public class GetAllStockAirtimeRequest : PaggingBase, IGet, IReturn<MessagePagedResponseBase>
{
    public string Filter { get; set; }
    public string ProviderCode { get; set; }
    public byte Status { get; set; }
}

// get stock airtime by provider 
[Route("/api/v1/stock/airtime", "GET")]
public class GetStockAirtimeRequest : IGet, IReturn<MessageResponseBase>
{
    public string ProviderCode { get; set; }
}

// add stock airtime by provider 
[Route("/api/v1/stock/airtime", "POST")]
public class CreateStockAirtimeRequest : IPost, IReturn<MessageResponseBase>
{
    public string ProviderCode { get; set; }
    public byte Status { get; set; }
    public string Description { get; set; }
    public decimal MaxLimitAirtime { get; set; }
    public decimal MinLimitAirtime { get; set; }
}

// update stock airtime by provider 
[Route("/api/v1/stock/airtime", "PUT")]
public class UpdateStockAirtimeRequest : IPut, IReturn<MessageResponseBase>
{
    public string ProviderCode { get; set; }
    public byte Status { get; set; }
    public string Description { get; set; }
    public decimal MaxLimitAirtime { get; set; }
    public decimal MinLimitAirtime { get; set; }
}

// Get list airtime batch
[Route("/api/v1/stock/airtime-import-list", "Get")]
public class GetAllBatchAirtimeRequest : PaggingBase, IGet, IReturn<MessagePagedResponseBase>
{
    public string Filter { get; set; }
    public string BatchCode { get; set; }
    public string ProviderCode { get; set; }
    public byte Status { get; set; }
    public DateTime? FormDate { get; set; }
    public DateTime? ToDate { get; set; }
}

// Get airtime batch
[Route("/api/v1/stock/airtime-import", "Get")]
public class GetBatchAirtimeRequest : IGet, IReturn<MessageResponseBase>
{
    public string BatchCode { get; set; }
}

// create airtime batch
[Route("/api/v1/stock/airtime-import", "POST")]
public class CreateBatchAirtimeRequest : IPost, IReturn<MessageResponseBase>
{
    public string ProviderCode { get; set; }

    /// <summary>
    ///     giá vốn
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    ///     Airtime
    /// </summary>
    public decimal Airtime { get; set; }

    /// <summary>
    ///     Discount
    /// </summary>
    public float Discount { get; set; }

    public byte Status { get; set; }
    public string Description { get; set; }
    public string CreatedAccount { get; set; }
}

// update stock airtime by provider 
[Route("/api/v1/stock/airtime-import", "PUT")]
public class UpdateBatchAirtimeRequest : IPut, IReturn<MessageResponseBase>
{
    public string BatchCode { get; set; }
    public string ProviderCode { get; set; }

    /// <summary>
    ///     giá vốn
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    ///     Airtime
    /// </summary>
    public decimal Airtime { get; set; }

    /// <summary>
    ///     Discount
    /// </summary>
    public float Discount { get; set; }

    public byte Status { get; set; }
    public string Description { get; set; }

    public string ModifiedAccount { get; set; }
    // public string TransRef { get; set; }
}

[Route("/api/v1/stock/airtime-import", "DELETE")]
public class DeleteBatchAirtimeRequest : IDelete, IReturn<MessageResponseBase>
{
    public string BatchCode { get; set; }
}