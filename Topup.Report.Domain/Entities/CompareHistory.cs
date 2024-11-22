using System;
using MongoDbGenericRepository.Models;

namespace Topup.Report.Domain.Entities;

/// <summary>
///     Lịch sử đối soát
/// </summary>
public class CompareHistory : Document
{
    /// <summary>
    ///     Ngày đối soát
    /// </summary>
    public DateTime CompareDate { get; set; }


    public string CompareDateSoft { get; set; }

    /// <summary>
    ///     Ngày giao dịch
    /// </summary>
    public DateTime TransDate { get; set; }

    public string TransDateSoft { get; set; }

    /// <summary>
    ///     Nhà cung cấp
    /// </summary>
    public string ProviderCode { get; set; }

    /// <summary>
    ///     Tên file đối soát của Nhất Trần
    /// </summary>
    public string SysFileName { get; set; }

    /// <summary>
    ///     Tên file đối soát của nhà cung cấp
    /// </summary>
    public string ProviderFileName { get; set; }

    /// <summary>
    ///     Số lượng giao dịch của hệ thống Nhất Trần
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

    public decimal SysOnlyPrice { get; set; }

    /// <summary>
    ///     Số giao dịch lệch
    /// </summary>
    public int NotSameQuantity { get; set; }

    /// <summary>
    ///     Số tiền lệch NT
    /// </summary>
    public decimal NotSameSysAmount { get; set; }

    /// <summary>
    ///     Số tiền lệch Provider
    /// </summary>
    public decimal NotSameProviderAmount { get; set; }

    /// <summary>
    ///     Người đối soát
    /// </summary>
    public string AccountCompare { get; set; }

    /// <summary>
    ///     Số lượng chờ hoàn
    /// </summary>
    public int? RefundWaitQuantity { get; set; }

    /// <summary>
    ///     Số tiền chờ hoàn
    /// </summary>
    public decimal? RefundWaitAmount { get; set; }

    public decimal? RefundWaitPrice { get; set; }

    /// <summary>
    ///     Số lượng đã hoàn
    /// </summary>
    public int? RefundQuantity { get; set; }

    /// <summary>
    ///     Số tiền chờ hoàn
    /// </summary>
    public decimal? RefundAmount { get; set; }

    public decimal? RefundPrice { get; set; }


    public bool Isenabled { get; set; }


    public string KeyCode { get; set; }
}