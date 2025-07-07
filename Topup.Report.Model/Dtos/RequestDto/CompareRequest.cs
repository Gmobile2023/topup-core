using System;
using System.Collections.Generic;
using Topup.Shared;
using ServiceStack;

namespace Topup.Report.Model.Dtos.RequestDto;

/// <summary>
///     Lịch sử đối soát
/// </summary>
[Route("/api/v1/report/ReportCompareList", "GET")]
public class ReportCompareListRequest : PaggingBase, IReturn<MessagePagedResponseBase>
{
    public DateTime? FromTransDate { get; set; }
    public DateTime? ToTransDate { get; set; }
    public DateTime? FromCompareDate { get; set; }
    public DateTime? ToCompareDate { get; set; }
    public string ProviderCode { get; set; }
}

/// <summary>
///     Check ngày giao dịch đối soát
/// </summary>
[Route("/api/v1/report/ReportCheckCompare", "GET")]
public class ReportCheckCompareRequest : PaggingBase, IReturn<MessagePagedResponseBase>
{
    public string TransDate { get; set; }
    public string ProviderCode { get; set; }
}

/// <summary>
///     Chi tiết kết quả đối soát
/// </summary>
[Route("/api/v1/report/ReportCompareDeailReponse", "GET")]
public class ReportCompareDetailReonseRequest : PaggingBase, IReturn<MessagePagedResponseBase>
{
    public string KeyCode { get; set; }
    public string ProviderCode { get; set; }
    public int CompareType { get; set; }
}

/// <summary>
///     Kết quả đối soát
/// </summary>
[Route("/api/v1/report/ReportCompareReponse", "GET")]
public class ReportCompareReonseRequest : PaggingBase, IReturn<MessagePagedResponseBase>
{
    public DateTime TransDate { get; set; }
    public string ProviderCode { get; set; }
}

/// <summary>
///     Chi tiết hoàn tiền
/// </summary>
[Route("/api/v1/report/ReportCompareRefundDetail", "GET")]
public class ReportCompareRefundDetailRequest : PaggingBase, IReturn<MessagePagedResponseBase>
{
    public string KeyCode { get; set; }
    public string ProviderCode { get; set; }
    public int RefundInt { get; set; }
}

/// <summary>
///     Hoàn tiền
/// </summary>
[Route("/api/v1/report/ReportCompareRefund", "GET")]
public class ReportCompareRefundRequest : PaggingBase, IReturn<MessagePagedResponseBase>
{
    public DateTime FromDateTrans { get; set; }
    public DateTime ToDateTrans { get; set; }
    public string ProviderCode { get; set; }
}

/// <summary>
///     Hoàn tiền
/// </summary>
[Route("/api/v1/report/ReportCompareRefundSingle", "GET")]
public class ReportCompareRefundSingleRequest : PaggingBase, IReturn<MessagePagedResponseBase>
{
    public string KeyCode { get; set; }
    public string ProviderCode { get; set; }
}

/// <summary>
///     Đánh dấu hoàn tiền đối soát
/// </summary>
[Route("/api/v1/report/RefundSetCompareRequest", "POST")]
public class RefundSetCompareRequest
{
    public string KeyCode { get; set; }

    public string TransCode { get; set; }

    public string TransCodeRefund { get; set; }
}

[Route("/api/v1/report/CompareRefundCompareRequest", "POST")]
public class CompareRefundCompareRequest
{
    /// <summary>
    ///     Nhà cung cấp
    /// </summary>
    public string ProviderCode { get; set; }

    /// <summary>
    ///     Ngày giao dịch
    /// </summary>
    public string KeyCode { get; set; }

    /// <summary>
    ///     Danh sách giao dịch
    /// </summary>
    public List<string> Items { get; set; }
}

/// <summary>
///     Đối soát giao dịch
/// </summary>
[Route("/api/v1/report/CompareProviderRequest", "POST")]
public class CompareProviderRequest
{
    /// <summary>
    ///     Ngày đối soát
    /// </summary>
    public DateTime CompareDate { get; set; }

    /// <summary>
    ///     Ngày giao dịch
    /// </summary>
    public DateTime TransDate { get; set; }

    /// <summary>
    ///     Nhà cung cấp
    /// </summary>
    public string ProviderCode { get; set; }

    /// <summary>
    ///     Tên file đối soát của Hệ thống
    /// </summary>
    public string SysFileName { get; set; }

    /// <summary>
    ///     Tên file đối soát của nhà cung cấp
    /// </summary>
    public string ProviderFileName { get; set; }

    /// <summary>
    ///     Số lượng giao dịch của hệ thống Hệ thống
    /// </summary>
    public int SysQuantity { get; set; }


    /// <summary>
    ///     Tổng số tiền giao dịch
    /// </summary>
    public decimal SysAmount { get; set; }

    /// <summary>
    ///     Số giao dịch của nhà cung cấp
    /// </summary>
    public int ProviderQuantity { get; set; }

    /// <summary>
    ///     Số tiền của nhà cung cấp
    /// </summary>
    public decimal ProviderAmount { get; set; }

    /// <summary>
    ///     Số giao dịch khớp
    /// </summary>
    public int SameQuantity { get; set; }

    /// <summary>
    ///     Số tiền khớp
    /// </summary>
    public decimal SameAmount { get; set; }

    /// <summary>
    ///     Số giao dịch nhà cung cấp có mà NT không có
    /// </summary>
    public int ProviderOnlyQuantity { get; set; }

    /// <summary>
    ///     Số tiền giao dịch nhà cung cấp có mà NT không có
    /// </summary>
    public decimal ProviderOnlyAmount { get; set; }

    /// <summary>
    ///     Số giao dịch NT có NCC không có
    /// </summary>
    public int SysOnlyQuantity { get; set; }

    /// <summary>
    ///     Số tiền giao dịch NT có mà NCC không có
    /// </summary>
    public decimal SysOnlyAmount { get; set; }

    /// <summary>
    ///     Số giao dịch lệch
    /// </summary>
    public int NotSameQuantity { get; set; }

    /// <summary>
    ///     Số tiền lệch
    /// </summary>
    public decimal NotSameSysAmount { get; set; }

    /// <summary>
    ///     Số tiền lệch Provider
    /// </summary>
    public decimal NotSameProviderAmount { get; set; }

    public bool Isenabled { get; set; }

    /// <summary>
    ///     Người đối soát
    /// </summary>
    public string AccountCompare { get; set; }

    /// <summary>
    ///     Danh sách đối soát
    /// </summary>
    public List<CompareItem> Items { get; set; }
}

[Route("/api/v1/report/TestFptRequest", "POST")]
public class TestFptRequest
{  
    public string ProviderCode { get; set; }
}

public class CompareItem
{
    public string AccountCode { get; set; }

    public string TransCode { get; set; }

    public DateTime TransDate { get; set; }

    public decimal SysValue { get; set; }

    public decimal ProviderValue { get; set; }

    public int Status { get; set; }

    public int Result { get; set; }

    public string ProductCode { get; set; }

    public string ProductName { get; set; }

    public string ReceivedAccount { get; set; }

    public string TransCodePay { get; set; }

    public DateTime CompareDate { get; set; }

    public decimal Amount { get; set; }

    public string ProviderCode { get; set; }

    public bool IsRefund { get; set; }
}

// [Route("/api/v1/balance/cancelPayment", "POST")]
// public class CancelPaymentRequest
// {
//     public string TransactionCode { get; set; }
//     public decimal RevertAmount { get; set; }
//     public string TransRef { get; set; }
//     public string TransNote { get; set; }
//     public string Description { get; set; }
//     public string CurrencyCode { get; set; }
//     public string AccountCode { get; set; }
// }