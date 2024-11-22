using System;
using System.Collections.Generic;
using Topup.Shared.Common;
using Microsoft.Extensions.Logging;
using NPOI.HSSF.Util;
using NPOI.SS.UserModel;
using Topup.Kpp.Domain.DataExporting.Excel.EpPlus;
using Topup.Kpp.Domain.Entities;

namespace Topup.Kpp.Domain.Exporting;

public class ExportDataExcel : IExportDataExcel
{
    private readonly IEpPlusExcelExporterBase _exporter;
    private readonly ILogger<ExportDataExcel> _log;

    public ExportDataExcel(IEpPlusExcelExporterBase exporter,
        ILogger<ExportDataExcel> log)
    {
        _exporter = exporter;
        _log = log;
    }

    public FileDto KppExportToFile(List<AccountDto> list, string name)
    {
        try
        {
            return _exporter.CreateExcelPackageV2(
                name,
                excelPackage =>
                {
                    var sheet = excelPackage.CreateSheet("TongHop");
                    var style = sheet.Workbook.CreateCellStyle();
                    style.BorderTop = BorderStyle.Thin;
                    style.BorderLeft = BorderStyle.Thin;
                    style.BorderRight = BorderStyle.Thin;
                    style.BorderBottom = BorderStyle.Thin;
                    style.VerticalAlignment = VerticalAlignment.Center;
                    style.Alignment = HorizontalAlignment.Center;
                    var font = sheet.Workbook.CreateFont();
                    font.IsBold = true;
                    font.FontHeightInPoints = 12;

                    var styleRed = sheet.Workbook.CreateCellStyle();
                    styleRed.FillForegroundColor = HSSFColor.Red.Index;
                    styleRed.VerticalAlignment = VerticalAlignment.Center;
                    styleRed.Alignment = HorizontalAlignment.Center;
                    styleRed.WrapText = true;
                    styleRed.FillPattern = FillPattern.FineDots;


                    var columsIndex = 0;
                    var rowsIndex = 0;

                    #region 1.Bảng mã thẻ

                    _exporter.AddObjectRowItemColumn(sheet, 5, 0, "Tài khoản", isCenter: false);
                    _exporter.AddHeaderStartRowIndex(
                        sheet, rowsIndex, columsIndex,
                        "Mã đại lý KPP",
                        "Số dư đầu kỳ",
                        "Số tiền nhận",
                        "Số tiền chuyển",
                        "Số tiền bán hàng",
                        "Số dư cuối kỳ",
                        "Chênh lệch",
                        "Trạng thái"
                    );

                    _exporter.AddObjectStartRowsIndex(
                        sheet, rowsIndex, columsIndex, style, styleRed, list,
                        _ => _.AccountCode,
                        _ => CellOption.Create(_.Before, "Number"),
                        _ => CellOption.Create(_.Input, "Number"),
                        _ => CellOption.Create(_.Transfer, "Number"),
                        _ => CellOption.Create(_.Payment, "Number"),
                        _ => CellOption.Create(_.After, "Number"),
                        _ => CellOption.Create(_.Deviation, "Number"),
                        _ => _.Status
                    );

                    #endregion
                });
        }
        catch (Exception ex)
        {
            _log.LogInformation($"KppExportToFile Exception: {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
            return null;
        }
    }
}