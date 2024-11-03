using System;
using System.Collections.Generic;
using GMB.Topup.Gw.Model.Dtos;
using GMB.Topup.Shared;
using ServiceStack;

namespace GMB.Topup.Gw.Model.RequestDtos;

[Route("/api/v1/topup/history", "GET")]
public class GetTopupHistoryRequest : PaggingBase, IReturn<MessagePagedResponseBase>
{
    public string TransCode { get; set; }
}

[Route("/api/v1/topup/getTopupItems", "GET")]
public class GetTopupItemsRequest : PaggingBase, IUserInfoRequest, IReturn<MessagePagedResponseBase>
{
    public SaleRequestStatus Status { get; set; }
    public string MobileNumber { get; set; }
    public string TransRef { get; set; }
    public string TransCode { get; set; }
    public string Serial { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string ServiceCode { get; set; }
    public string CategoryCode { get; set; }
    public string ProductCode { get; set; }

    public List<string> ServiceCodes { get; set; }
    public List<string> CategoryCodes { get; set; }
    public List<string> ProductCodes { get; set; }
    public string Vendor { get; set; }
    public string Filter { get; set; }

    public string TopupTransactionType { get; set; }

    public string PartnerCode { get; set; }
    public string StaffAccount { get; set; }
    public SystemAccountType AccountType { get; set; }
    public AgentType AgentType { get; set; }

    public string ParentCode { get; set; }
    // public string WorkerApp { get; set; }
}

[Route("/api/v1/invoice/invoiceRequest", "POST")]
public class InvoiceRequest
{
    public decimal Amount { get; set; }
    public DateTime CreatedTime { get; set; }
    public string Email { get; set; }
    public string FullName { get; set; }
    public string CustomerReference { get; set; }
    public string Address { get; set; }
    public string Period { get; set; }
    public string PhoneNumber { get; set; }
    public string TransCode { get; set; }
    public string TransRef { get; set; }
    public string Description { get; set; }
    public string ExtraInfo { get; set; }
}

[Route("/api/v1/getPayBatchBill", "GET")]
public class GetPayBatchBill : PaggingBase, IReturn<MessagePagedResponseBase>
{
    /// <summary>
    ///     Số hóa đơn tối thiểu
    /// </summary>
    public int BlockMin { get; set; }

    /// <summary>
    ///     Số tiền thưởng trên mỗi Block
    /// </summary>
    public decimal MoneyBlock { get; set; }

    /// <summary>
    ///     Số tiền hóa đơn tối thiểu
    /// </summary>
    public decimal BillAmountMin { get; set; }

    /// <summary>
    ///     Số tiền thưởng tối đa
    /// </summary>
    public decimal BonusMoneyMax { get; set; }

    public DateTime FromDate { get; set; }

    public DateTime ToDate { get; set; }

    public string CategoryCode { get; set; }

    public string ProductCode { get; set; }
}

[Route("/api/v1/Offsettopup/history", "GET")]
public class GetOffsetTopupHistoryRequest : PaggingBase, IReturn<MessagePagedResponseBase>
{
    public string OriginPartnerCode { get; set; }

    public string OriginTransCode { get; set; }

    public string TransCode { get; set; }

    public string PartnerCode { get; set; }

    public string ReceiverInfo { get; set; }

    public int Status { get; set; }

    public DateTime FromDate { get; set; }

    public DateTime ToDate { get; set; }
}

[Route("/api/v1/Offsettopup/history", "POST")]
public class OffsetTestRequest
{
}