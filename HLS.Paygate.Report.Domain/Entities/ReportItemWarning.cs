using System;
using MongoDbGenericRepository.Models;

namespace HLS.Paygate.Report.Domain.Entities;

public class ReportItemWarning : Document
{
    /// <summary>
    ///     Status: Trạng thái,Nhà cung cấp
    ///     SouceTrans: Thông tin giao dịch gốc
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    ///     Loại giao dịch thực hiện
    /// </summary>
    public string TransType { get; set; }

    /// <summary>
    ///     0: Chưa xử lý
    ///     1: Đã xử lý
    ///     2: Đang xử lý
    /// </summary>
    public int Status { get; set; }

    public string TransCode { get; set; }
    public string TextDay { get; set; }
    public DateTime CreatedDate { get; set; }
    public string Message { get; set; }

    public int Retry { get; set; }
}

public class ReportWarningType
{
    public const string Type_Status = "Status";
    public const string Type_SouceTrans = "SouceTrans";
    public const string Type_ErrorInsert = "ErrorInsert";
    public const string Type_ErrorConvertInfo = "ErrorConvertInfo";
}