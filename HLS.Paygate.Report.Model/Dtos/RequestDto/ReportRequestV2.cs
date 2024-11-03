using System;
using System.Collections.Generic;
using HLS.Paygate.Shared;
using ServiceStack;

namespace HLS.Paygate.Report.Model.Dtos.RequestDto;

[Route("/api/v1/report/ReportCommissionDetail", "GET")]
public class ReportCommissionDetailRequest : PaggingBase, IReturn<MessagePagedResponseBase>
{
    /// <summary>
    ///     Tìm kiếm chung
    /// </summary>
    public string Filter { get; set; }

    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }

    public string TransCode { get; set; }

    /// <summary>
    ///     Đại lý tổng
    /// </summary>
    public string AgentCodeSum { get; set; }

    /// <summary>
    ///     Đại lý
    /// </summary>
    public string AgentCode { get; set; }

    /// <summary>
    ///     Dịch vụ
    /// </summary>
    public List<string> ServiceCode { get; set; }

    /// <summary>
    ///     Loại sản phẩm
    /// </summary>
    public List<string> CategoryCode { get; set; }

    /// <summary>
    ///     Sản phẩm
    /// </summary>
    public List<string> ProductCode { get; set; }

    /// <summary>
    ///     Trạng thái
    /// </summary>
    public int Status { get; set; }

    public string LoginCode { get; set; }
}

[Route("/api/v1/report/ReportCommissionTotal", "GET")]
public class ReportCommissionTotalRequest : PaggingBase, IReturn<MessagePagedResponseBase>
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }

    public string Filter { get; set; }

    /// <summary>
    ///     Đại lý tổng
    /// </summary>
    public string AgentCode { get; set; }

    public string LoginCode { get; set; }
}

[Route("/api/v1/report/ReportCommissionAgentDetail", "GET")]
public class ReportCommissionAgentDetailRequest : PaggingBase, IReturn<MessagePagedResponseBase>
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }

    public string TransCode { get; set; }

    /// <summary>
    ///     Đại lý
    /// </summary>
    public string AgentCode { get; set; }

    /// <summary>
    ///     Dịch vụ
    /// </summary>
    public string ServiceCode { get; set; }

    /// <summary>
    ///     Loại sản phẩm
    /// </summary>
    public string CategoryCode { get; set; }

    /// <summary>
    ///     Sản phẩm
    /// </summary>
    public string ProductCode { get; set; }

    /// <summary>
    ///     Trạng thái
    /// </summary>
    public int Status { get; set; }

    /// <summary>
    ///     Trạng thái
    /// </summary>
    public int StatusPayment { get; set; }

    public string LoginCode { get; set; }
}

[Route("/api/v1/report/ReportCommissionAgentTotal", "GET")]
public class ReportCommissionAgentTotalRequest : PaggingBase, IReturn<MessagePagedResponseBase>
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string AgentCode { get; set; }
    public string LoginCode { get; set; }
}