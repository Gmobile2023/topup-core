using System;
using System.Collections.Generic;
using GMB.Topup.Gw.Model.Dtos;
using GMB.Topup.Shared;
using ServiceStack;
using ServiceStack.DataAnnotations;

namespace GMB.Topup.Gw.Model;

[Route("/api/v1/paybatch", "POST")]
public class PayBatchRequest : IPost, IUserInfoRequest, IReturn<MessageResponseBase>
{
    [Required] public List<PayBatchItemDto> Items { get; set; }

    public Channel Channel { get; set; }
    public string TransRef { get; set; }
    public string BatchType { get; set; }

    [Required] public string PartnerCode { get; set; }

    public string StaffAccount { get; set; }
    public SystemAccountType AccountType { get; set; }
    public AgentType AgentType { get; set; }
    public string ParentCode { get; set; }
}

[Route("/api/v1/batchLot_List", "GET")]
public class BatchListGetRequest : PaggingBase, IGet, IReturn<MessagePagedResponseBase>
{
    public string BatchCode { get; set; }
    public string AccountCode { get; set; }
    public bool IsStaff { get; set; }
    public string BatchType { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int Status { get; set; }
}

[Route("/api/v1/batchLot_Detail", "GET")]
public class BatchDetailGetRequest : PaggingBase, IGet, IReturn<MessagePagedResponseBase>
{
    public string AccountCode { get; set; }
    public bool IsStaff { get; set; }
    public string BatchCode { get; set; }
    public int Status { get; set; }
    public int BatchStatus { get; set; }
}

[Route("/api/v1/batchLot_Single", "GET")]
public class BatchSingleGetRequest : PaggingBase, IGet, IReturn<MessagePagedResponseBase>
{
    public string AccountCode { get; set; }
    public bool IsStaff { get; set; }
    public string BatchCode { get; set; }
}

[Route("/api/v1/batchLot_Stop", "POST")]
public class Batch_StopRequest : IPost, IReturn<ResponseMessageApi<object>>
{
    public string AccountCode { get; set; }
    public bool IsStaff { get; set; }
    public string BatchCode { get; set; }
    public string BatchType { get; set; }
}