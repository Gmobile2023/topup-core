using System;
using System.Collections.Generic;
using Topup.Shared;

namespace Topup.Gw.Model.Dtos;

public class BatchRequestDto : IUserInfoRequest
{
    public BatchLotRequestStatus Status { get; set; }
    public SaleRequestType SaleRequestType { get; set; }
    public DateTime CreatedTime { get; set; }
    public Channel Channel { get; set; }
    public string Email { get; set; }
    public string ExtraInfo { get; set; }
    public string BatchName { get; set; }
    public string BatchType { get; set; }

    public string BatchCode { get; set; }
    public List<PayBatchItemDto> Items { get; set; }
    public string PartnerCode { get; set; }
    public string StaffAccount { get; set; }
    public SystemAccountType AccountType { get; set; }
    public AgentType AgentType { get; set; }
    public string ParentCode { get; set; }
}

public class BatchItemDto
{
    public BatchLotRequestStatus Status { get; set; }
    public SaleRequestType SaleRequestType { get; set; }
    public DateTime CreatedTime { get; set; }
    public Channel Channel { get; set; }
    public DateTime EndProcessTime { get; set; }
    public string BatchCode { get; set; }
    public string Email { get; set; }
    public string PartnerCode { get; set; }
    public string StaffAccount { get; set; }
    public string ExtraInfo { get; set; }
    public string BatchName { get; set; }
    public string BatchType { get; set; }
    public decimal PaymentAmount { get; set; }
    public int Quantity { get; set; }
}

public class BatchDetailDto
{
    public BatchLotRequestStatus BatchStatus { get; set; }
    public string ReceiverInfo { get; set; }
    public SaleRequestStatus Status { get; set; }
    public SaleRequestType SaleRequestType { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime? UpdateTime { get; set; }
    public string PartnerCode { get; set; }
    public string TransRef { get; set; }
    public string BatchCode { get; set; }
    public string Vendor { get; set; }
    public string Provider { get; set; }
    public decimal Amount { get; set; }
    public int Quantity { get; set; }
    public decimal PaymentAmount { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal? Fee { get; set; }
    public string ProductCode { get; set; }
    public string ProductName { get; set; }
    public string CategoryCode { get; set; }
    public string CategoryName { get; set; }
    public string ServiceCode { get; set; }
    public string ServiceName { get; set; }
    public string Email { get; set; }
    public string StaffAccount { get; set; }
    public string ExtraInfo { get; set; }
}