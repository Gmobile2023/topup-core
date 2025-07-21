using System;
using System.Collections.Generic;
using System.Linq;
using Topup.Report.Model.Dtos;
using Topup.Shared.Common;
using Microsoft.Extensions.Logging;
using NPOI.HSSF.Util;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using Topup.Report.Domain.DataExporting.Excel.EpPlus;
using Topup.Report.Domain.Entities;

namespace Topup.Report.Domain.Exporting;

public partial class ExportDataExcel : IExportDataExcel
{
    private readonly IEpPlusExcelExporterBase _exporter;
    private readonly ILogger<ExportDataExcel> _log;

    public ExportDataExcel(IEpPlusExcelExporterBase exporter,
        ILogger<ExportDataExcel> log)
    {
        _exporter = exporter;
        _log = log;
    }

    public FileDto ReportCardStockAutoToFile(List<ReportCardStockDayDto> input, string name)
    {
        try
        {
            return _exporter.CreateExcelPackageV2(
                name,
                excelPackage =>
                {
                    var sheet = excelPackage.CreateSheet("Sheet1");
                    _exporter.AddHeaderV2(
                        sheet,
                        "Dịch vụ",
                        "Loại sản phẩm",
                        "Sản phẩm",
                        "Mệnh giá",
                        "Kho Temp/Tồn đầu kỳ",
                        "Kho Temp/SL Nhập",
                        "Kho Temp/SL Xuất",
                        "Kho Temp/Tồn cuối kỳ",
                        "Kho Temp/Thành tiền tồn cuối",
                        "Kho Sale/Tồn đầu kỳ",
                        "Kho Sale/SL Nhập",
                        "Kho Sale/SL Xuất",
                        "Kho Sale/Tồn cuối kỳ",
                        "Kho Sale/Thành tiền tồn cuối"
                    );

                    _exporter.AddObjectsV2(
                        sheet, input,
                        _ => _.ServiceName,
                        _ => _.CategoryName,
                        _ => _.ProductName,
                        _ => CellOption.Create(_.CardValue, "Number"),
                        _ => CellOption.Create(_.Before_Temp, "Number"),
                        _ => CellOption.Create(_.Import_Temp, "Number"),
                        _ => CellOption.Create(_.Export_Temp, "Number"),
                        _ => CellOption.Create(_.After_Temp, "Number"),
                        _ => CellOption.Create(_.Monney_Temp, "Number"),
                        _ => CellOption.Create(_.Before_Sale, "Number"),
                        _ => CellOption.Create(_.Import_Sale, "Number"),
                        _ => CellOption.Create(_.Export_Sale, "Number"),
                        _ => CellOption.Create(_.After_Sale, "Number"),
                        _ => CellOption.Create(_.Monney_Sale, "Number")
                    );
                });
        }
        catch (Exception ex)
        {
            _log.LogInformation(
               $"ReportCardStockAutoToFile Exception: {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
            return null;
        }
    }

    public FileDto ReportTotalRevenueAutoToFile(List<ReportRevenueTotalAutoDto> input, string name)
    {
        try
        {
            return _exporter.CreateExcelPackageV2(
                name,
                excelPackage =>
                {
                    var sheet = excelPackage.CreateSheet("Sheet1");
                    _exporter.AddHeaderV2(
                        sheet,
                        "Ngày",
                        "Số lượng ĐL kích hoạt",
                        "SL ĐL hoạt động",
                        "Số dư ĐL Đầu kỳ",
                        "DS nạp trong kỳ",
                        "Phát sinh tăng khác",
                        "DS bán trong kỳ",
                        "Phát sinh giảm khác",
                        "Số dư cuối kỳ"
                    );

                    _exporter.AddObjectsV2(
                        sheet, input,
                        _ => CellOption.Create(_.CreatedDay, "dd/MM/yyyy"),
                        _ => CellOption.Create(_.AccountActive, "Number"),
                        _ => CellOption.Create(_.AccountRevenue, "Number"),
                        _ => CellOption.Create(_.Before, "Number"),
                        _ => CellOption.Create(_.InputDeposit, "Number"),
                        _ => CellOption.Create(_.IncOther, "Number"),
                        _ => CellOption.Create(_.Sale, "Number"),
                        _ => CellOption.Create(_.DecOther, "Number"),
                        _ => CellOption.Create(_.After, "Number")
                    );
                });
        }
        catch (Exception ex)
        {
            _log.LogInformation(
                $"ReportTotalRevenueAutoToFile Exception: {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
            return null;
        }
    }

    public FileDto ReportBalanceSupplierAutoToFile(List<ReportBalanceSupplierDto> input, string name)
    {
        try
        {
            var first = input.OrderByDescending(c => c.CreatedDay).First();
            var headers = new List<string>();
            headers.Add("Ngày");
            foreach (var x in first.Items)
                headers.Add(x.Name);
            headers.Add("Tổng");

            #region

            return _exporter.CreateExcelPackageV2(
                name,
                excelPackage =>
                {
                    var sheet = excelPackage.CreateSheet("Sheet1");
                    _exporter.AddHeaderV2(
                        sheet,
                        headers.ToArray()
                    );                    
                        _exporter.AddObjectsV2(
                            sheet, input,
                            _ => CellOption.Create(_.CreatedDay, "dd/MM/yyyy"),
                            _ => CellOption.Create(_.Items[0].Balance, "Number"),
                            _ => CellOption.Create(_.Items[1].Balance, "Number"),
                            _ => CellOption.Create(_.Items[2].Balance, "Number"),
                            _ => CellOption.Create(_.Items[3].Balance, "Number"),
                            _ => CellOption.Create(_.Items[4].Balance, "Number"),
                            _ => CellOption.Create(_.Items[5].Balance, "Number"),
                            _ => CellOption.Create(_.Items[6].Balance, "Number"),
                            _ => CellOption.Create(_.Items[7].Balance, "Number"),
                            _ => CellOption.Create(_.Items[8].Balance, "Number"),
                            _ => CellOption.Create(_.Items[9].Balance, "Number"),
                            _ => CellOption.Create(_.Items[10].Balance, "Number"),
                            _ => CellOption.Create(_.Items[11].Balance, "Number"),
                            _ => CellOption.Create(_.Items[12].Balance, "Number"),
                            _ => CellOption.Create(_.Items[13].Balance, "Number"),
                            _ => CellOption.Create(_.Items[14].Balance, "Number"),
                            _ => CellOption.Create(_.Items[15].Balance, "Number"),
                            _ => CellOption.Create(_.Items[16].Balance, "Number"),
                            _ => CellOption.Create(_.Items[17].Balance, "Number"),
                            _ => CellOption.Create(_.Items[18].Balance, "Number"),
                            _ => CellOption.Create(_.Items[19].Balance, "Number"),
                            _ => CellOption.Create(_.Items[20].Balance, "Number"),
                            _ => CellOption.Create(_.Items[21].Balance, "Number"),
                            _ => CellOption.Create(_.Items[22].Balance, "Number"),
                            _ => CellOption.Create(_.Items[23].Balance, "Number"),
                            _ => CellOption.Create(_.Items[0].Balance 
                            + _.Items[1].Balance
                            + _.Items[2].Balance
                            + _.Items[3].Balance
                            + _.Items[4].Balance
                            + _.Items[5].Balance
                            + _.Items[6].Balance
                            + _.Items[7].Balance
                            + _.Items[8].Balance
                            + _.Items[9].Balance
                            + _.Items[10].Balance
                            + _.Items[11].Balance
                            + _.Items[12].Balance
                            + _.Items[13].Balance
                            + _.Items[14].Balance
                            + _.Items[15].Balance
                            + _.Items[16].Balance
                            + _.Items[17].Balance
                            + _.Items[18].Balance
                            + _.Items[19].Balance
                            + _.Items[20].Balance
                            + _.Items[21].Balance
                            + _.Items[22].Balance
                            + _.Items[23].Balance
                            , "Number"));
                });

            #endregion
        }
        catch (Exception ex)
        {
            _log.LogInformation(
                $"ReportBalanceSupplierAutoToFile Exception: {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
            return null;
        }
    }

    public FileDto ReportSmsAutoToFile(List<ReportSmsDto> input, string name)
    {
        try
        {
            return _exporter.CreateExcelPackageV2(
                name,
                excelPackage =>
                {
                    var sheet = excelPackage.CreateSheet("Sheet1");
                    _exporter.AddHeaderV2(
                        sheet,
                        "Ngày",
                        "Số điện thoại",
                        "Nội dung",
                        "Mã giao dịch",
                        "Trạng thái",
                        "Kênh",
                        "Kết quả"
                    );

                    _exporter.AddObjectsV2(
                        sheet, input,
                        _ => CellOption.Create(_.CreatedDate, "dd/MM/yyyy HH:mm:ss"),
                        _ => _.Phone,
                        _ => _.Message,
                        _ => _.TransCode,
                        _ => _.Status == 1 ? "Thành công" : _.Status == 0 ? "Thất bại" : "",
                        _ => _.Channel,
                        _ => _.Result
                    );
                });
        }
        catch (Exception ex)
        {
            _log.LogInformation(
                $"ReportSmsAutoToFile Exception: {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
            return null;
        }
    }

    public FileDto ReportCompareToFile(List<CompareReponseDto> input, string name)
    {
        try
        {
            return _exporter.CreateExcelPackageV2(
                name,
                excelPackage =>
                {
                    var sheet = excelPackage.CreateSheet("Sheet1");
                    _exporter.AddHeaderV2(
                        sheet,
                        "Loại kết quá",
                        "SL giao dịch",
                        "Số tiền BF Hệ thống",
                        "Số tiền BF NCC",
                        "Số tiền lệch"
                    );

                    _exporter.AddObjectsV2(
                        sheet, input,
                        _ => _.CompareType,
                        _ => _.Quantity,
                        _ => _.AmountSys,
                        _ => _.AmountProvider,
                        _ => _.Deviation
                    );
                });
        }
        catch (Exception ex)
        {
            _log.LogInformation(
                $"ReportCompareToFile Exception: {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
            return null;
        }
    }

    public FileDto ReportRefundToFile(List<CompareReponseDto> input, string name)
    {
        try
        {
            return _exporter.CreateExcelPackageV2(
                name,
                excelPackage =>
                {
                    var sheet = excelPackage.CreateSheet("Sheet1");
                    _exporter.AddHeaderV2(
                        sheet,
                        "Loại kết quả",
                        "Số lượng",
                        "Mệnh giá",
                        "Số tiền hoàn"
                    );

                    _exporter.AddObjectsV2(
                        sheet, input,
                        _ => _.CompareType,
                        _ => _.Quantity,
                        _ => _.AmountSys,
                        _ => _.Amount
                    );
                });
        }
        catch (Exception ex)
        {
            _log.LogInformation(
                $"ReportRefundToFile Exception: {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
            return null;
        }
    }

    public FileDto ReportCompareParnerExportToFile(ReportComparePartnerExportInfo input, string name)
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


                    var styleSum = sheet.Workbook.CreateCellStyle();
                    styleSum.BorderTop = BorderStyle.Thin;
                    styleSum.BorderLeft = BorderStyle.Thin;
                    styleSum.BorderRight = BorderStyle.Thin;
                    styleSum.BorderBottom = BorderStyle.Thin;
                    styleSum.FillForegroundColor = HSSFColor.LightYellow.Index;
                    styleSum.WrapText = true;
                    styleSum.FillPattern = FillPattern.FineDots;
                    styleSum.VerticalAlignment = VerticalAlignment.Center;
                    styleSum.Alignment = HorizontalAlignment.Center;

                    var font = sheet.Workbook.CreateFont();
                    font.IsBold = true;
                    font.FontHeightInPoints = 12;
                    styleSum.SetFont(font);

                    var styleSum2 = sheet.Workbook.CreateCellStyle();
                    styleSum2.VerticalAlignment = VerticalAlignment.Bottom;
                    styleSum2.Alignment = HorizontalAlignment.Left;
                    styleSum2.SetFont(font);

                    _exporter.AddObjectRowItemColumn(sheet, 0, 0, input.Title);
                    sheet.AddMergedRegion(new CellRangeAddress(0, 0, 0, 6));
                    _exporter.AddObjectRowItemColumn(sheet, 1, 0, input.PeriodCompare);
                    sheet.AddMergedRegion(new CellRangeAddress(1, 1, 0, 6));

                    var columsIndex = 0;
                    _exporter.AddObjectRowItemColumn(sheet, 2, 0, $"ĐỐI TÁC: {input.Provider} - {input.FullName}");
                    sheet.AddMergedRegion(new CellRangeAddress(2, 2, 0, 6));
                    _exporter.AddObjectRowItemColumn(sheet, 3, 0, input.Contract);
                    sheet.AddMergedRegion(new CellRangeAddress(3, 3, 0, 6));
                    _exporter.AddObjectRowItemColumn(sheet, 4, 0, input.PeriodPayment);
                    sheet.AddMergedRegion(new CellRangeAddress(4, 4, 0, 6));

                    var rowsIndex = 6;
                    var rowsType = 1;

                    #region 1.Bảng mã thẻ

                    sheet.SetColumnWidth(0, 7000);
                    sheet.SetColumnWidth(1, 5000);
                    sheet.SetColumnWidth(2, 4000);
                    sheet.SetColumnWidth(3, 4000);
                    sheet.SetColumnWidth(4, 4000);
                    sheet.SetColumnWidth(5, 4000);
                    sheet.SetColumnWidth(6, 6000);

                    #region 1.1.Bảng mã thẻ điện thoại

                    if (input.PinCodeItems.Count > 0)
                    {
                        _exporter.AddObjectRowItemColumn(sheet, 5, 0,
                            $"{ReportComparePartnerExportInfo.GetIndex(rowsType)}. ĐỐI SOÁT DỊCH VỤ MÃ THẺ",
                            isCenter: false);
                        _exporter.AddHeaderStartRowIndex(
                            sheet, rowsIndex, columsIndex,
                            "Dịch vụ",
                            "Loại sản phẩm",
                            "Mệnh giá",
                            "Số lượng",
                            "Thành tiền chưa CK",
                            "Tỷ lệ CK(%)",
                            "Tiền thanh toán"
                        );

                        _exporter.AddObjectStartRowsIndex(
                            sheet, rowsIndex, columsIndex, style, input.PinCodeItems,
                            _ => _.ServiceName,
                            _ => _.CategoryName,
                            _ => CellOption.Create(_.ProductValue, "Number"),
                            _ => CellOption.Create(_.Quantity, "Number"),
                            _ => CellOption.Create(_.Value, "Number"),
                            _ => CellOption.Create(_.DiscountRate, "Number"),
                            _ => CellOption.Create(_.Price, "Number")
                        );

                        rowsIndex = rowsIndex + input.TotalRowsPinCode + 1;
                        _exporter.AddObjectSumTableIndex(
                            sheet, styleSum, rowsIndex, columsIndex, input.SumPinCodes,
                            _ => "Tổng",
                            _ => "",
                            _ => "",
                            _ => CellOption.Create(_.Quantity, "Number"),
                            _ => CellOption.Create(_.Value, "Number"),
                            _ => "",
                            _ => CellOption.Create(_.Price, "Number")
                        );

                        rowsIndex = rowsIndex + 1;
                        _exporter.AddObjectSumIndex(
                            sheet, styleSum2, rowsIndex, columsIndex, input.SumPinCodes,
                            _ => "Tổng doanh số thu được hưởng:",
                            _ => "",
                            _ => CellOption.Create(_.Price, "Number"),
                            _ => "đồng (bao gồm VAT)"
                        );

                        sheet.AddMergedRegion(new CellRangeAddress(rowsIndex, rowsIndex, 0, 1));
                        rowsType = rowsType + 1;
                    }

                    #endregion


                    #region 1.2.Bảng mã thẻ game

                    if (input.PinGameItems.Count > 0)
                    {
                        rowsIndex = rowsIndex + 1;
                        _exporter.AddObjectRowItemColumn(sheet, 5, 0,
                            $"{ReportComparePartnerExportInfo.GetIndex(rowsType)}. ĐỐI SOÁT DỊCH VỤ MÃ THẺ GAME",
                            isCenter: false);
                        _exporter.AddHeaderStartRowIndex(
                            sheet, rowsIndex, columsIndex,
                            "Dịch vụ",
                            "Loại sản phẩm",
                            "Mệnh giá",
                            "Số lượng",
                            "Thành tiền chưa CK",
                            "Tỷ lệ CK(%)",
                            "Tiền thanh toán"
                        );

                        _exporter.AddObjectStartRowsIndex(
                            sheet, rowsIndex, columsIndex, style, input.PinGameItems,
                            _ => _.ServiceName,
                            _ => _.CategoryName,
                            _ => CellOption.Create(_.ProductValue, "Number"),
                            _ => CellOption.Create(_.Quantity, "Number"),
                            _ => CellOption.Create(_.Value, "Number"),
                            _ => CellOption.Create(_.DiscountRate, "Number"),
                            _ => CellOption.Create(_.Price, "Number")
                        );

                        rowsIndex = rowsIndex + input.TotalRowsPinGame + 1;
                        _exporter.AddObjectSumTableIndex(
                            sheet, styleSum, rowsIndex, columsIndex, input.SumPinGames,
                            _ => "Tổng",
                            _ => "",
                            _ => "",
                            _ => CellOption.Create(_.Quantity, "Number"),
                            _ => CellOption.Create(_.Value, "Number"),
                            _ => "",
                            _ => CellOption.Create(_.Price, "Number")
                        );

                        rowsIndex = rowsIndex + 1;
                        _exporter.AddObjectSumIndex(
                            sheet, styleSum2, rowsIndex, columsIndex, input.SumPinGames,
                            _ => "Tổng doanh số thu được hưởng:",
                            _ => "",
                            _ => CellOption.Create(_.Price, "Number"),
                            _ => "đồng (bao gồm VAT)"
                        );

                        sheet.AddMergedRegion(new CellRangeAddress(rowsIndex, rowsIndex, 0, 1));
                        rowsType = rowsType + 1;
                    }

                    #endregion

                    #endregion

                    #region 2.Bảng nạp tiền

                    #region 2.1.Bảng nạp tiền trả trước

                    //if (input.TopupPrepaIdItems.Count > 0)
                    //{
                    //    rowsIndex = rowsIndex + 1;
                    //    _exporter.AddObjectRowItemColumn(sheet, rowsIndex, 0,
                    //        $"{ReportComparePartnerExportInfo.GetIndex(rowsType)}. ĐỐI SOÁT DỊCH VỤ NẠP TIỀN TRẢ TRƯỚC",
                    //        isCenter: false);
                    //    rowsIndex = rowsIndex + 1;

                    //    _exporter.AddHeaderStartRowIndex(
                    //        sheet, rowsIndex, columsIndex,
                    //        "Dịch vụ",
                    //        "Loại sản phẩm",
                    //        "Mệnh giá",
                    //        "Số lượng",
                    //        "Thành tiền chưa CK",
                    //        "Tỷ lệ CK(%)",
                    //        "Tiền thanh toán"                          
                    //    );
                    //    _exporter.AddObjectStartRowsIndex(
                    //        sheet, rowsIndex, columsIndex, style, input.TopupPrepaIdItems,
                    //        _ => _.ServiceName,
                    //        _ => _.CategoryName,
                    //        _ => CellOption.Create(_.ProductValue, "Number"),
                    //        _ => CellOption.Create(_.Quantity, "Number"),
                    //        _ => CellOption.Create(_.Value, "Number"),
                    //        _ => CellOption.Create(_.DiscountRate, "Number"),
                    //        _ => CellOption.Create(_.Price, "Number")                           
                    //    );

                    //    rowsIndex = rowsIndex + input.TotalRowsTopupPrepaId + 1;

                    //    _exporter.AddObjectSumTableIndex(
                    //        sheet, styleSum, rowsIndex, columsIndex, input.SumTopupPrepaId,
                    //        _ => "Tổng",
                    //        _ => "",
                    //        _ => "",
                    //        _ => CellOption.Create(_.Quantity, "Number"),
                    //        _ => CellOption.Create(_.Value, "Number"),
                    //        _ => "",
                    //        _ => CellOption.Create(_.Price, "Number")
                    //    );

                    //    rowsIndex = rowsIndex + 1;
                    //    _exporter.AddObjectSumIndex(
                    //        sheet, styleSum2, rowsIndex, columsIndex, input.SumTopupPrepaId,
                    //        _ => "Tổng doanh số thu được hưởng:",
                    //        _ => "",
                    //        _ => CellOption.Create(_.Price, "Number"),
                    //        _ => "đồng (bao gồm VAT)"
                    //    );
                    //    sheet.AddMergedRegion(new CellRangeAddress(rowsIndex, rowsIndex, 0, 1));
                    //    rowsType = rowsType + 1;
                    //}

                    #endregion

                    #region 2.2.Bảng nạp tiền trả sau

                    //if (input.TopupPostpaIdItems.Count > 0)
                    //{
                    //    rowsIndex = rowsIndex + 1;
                    //    _exporter.AddObjectRowItemColumn(sheet, rowsIndex, 0,
                    //        $"{ReportComparePartnerExportInfo.GetIndex(rowsType)}. ĐỐI SOÁT DỊCH VỤ NẠP TIỀN TRẢ SAU",
                    //        isCenter: false);
                    //    rowsIndex = rowsIndex + 1;

                    //    _exporter.AddHeaderStartRowIndex(
                    //        sheet, rowsIndex, columsIndex,
                    //        "Dịch vụ",
                    //        "Loại sản phẩm",
                    //        "Mệnh giá",
                    //        "Số lượng",
                    //        "Thành tiền chưa CK",
                    //        "Tỷ lệ CK(%)",
                    //        "Tiền thanh toán"                          
                    //    );
                    //    _exporter.AddObjectStartRowsIndex(
                    //        sheet, rowsIndex, columsIndex, style, input.TopupPostpaIdItems,
                    //        _ => _.ServiceName,
                    //        _ => _.CategoryName,
                    //        _ => CellOption.Create(_.ProductValue, "Number"),
                    //        _ => CellOption.Create(_.Quantity, "Number"),
                    //        _ => CellOption.Create(_.Value, "Number"),
                    //        _ => CellOption.Create(_.DiscountRate, "Number"),
                    //        _ => CellOption.Create(_.Price, "Number")                           
                    //    );

                    //    rowsIndex = rowsIndex + input.TotalRowsTopup + 1;

                    //    _exporter.AddObjectSumTableIndex(
                    //        sheet, styleSum, rowsIndex, columsIndex, input.SumTopupPostpaId,
                    //        _ => "Tổng",
                    //        _ => "",
                    //        _ => "",
                    //        _ => CellOption.Create(_.Quantity, "Number"),
                    //        _ => CellOption.Create(_.Value, "Number"),
                    //        _ => "",
                    //        _ => CellOption.Create(_.Price, "Number")
                    //    );

                    //    rowsIndex = rowsIndex + 1;
                    //    _exporter.AddObjectSumIndex(
                    //        sheet, styleSum2, rowsIndex, columsIndex, input.SumTopupPostpaId,
                    //        _ => "Tổng doanh số thu được hưởng:",
                    //        _ => "",
                    //        _ => CellOption.Create(_.Price, "Number"),
                    //        _ => "đồng (bao gồm VAT)"
                    //    );
                    //    sheet.AddMergedRegion(new CellRangeAddress(rowsIndex, rowsIndex, 0, 1));
                    //    rowsType = rowsType + 1;
                    //}

                    #endregion

                    #region 2.3.Bảng nạp tiền

                    if (input.TopupItems.Count > 0)
                    {
                        rowsIndex = rowsIndex + 1;
                        _exporter.AddObjectRowItemColumn(sheet, rowsIndex, 0,
                            $"{ReportComparePartnerExportInfo.GetIndex(rowsType)}. ĐỐI SOÁT DỊCH VỤ NẠP TIỀN",
                            isCenter: false);
                        rowsIndex = rowsIndex + 1;

                        _exporter.AddHeaderStartRowIndex(
                            sheet, rowsIndex, columsIndex,
                            "Dịch vụ",
                            "Loại sản phẩm",
                            "Mệnh giá",
                            "Số lượng",
                            "Thành tiền chưa CK",
                            "Tỷ lệ CK(%)",
                            "Tiền thanh toán"                           
                        );
                        _exporter.AddObjectStartRowsIndex(
                            sheet, rowsIndex, columsIndex, style, input.TopupItems,
                            _ => _.ServiceName,
                            _ => _.CategoryName,
                            _ => CellOption.Create(_.ProductValue, "Number"),
                            _ => CellOption.Create(_.Quantity, "Number"),
                            _ => CellOption.Create(_.Value, "Number"),
                            _ => CellOption.Create(_.DiscountRate, "Number"),
                            _ => CellOption.Create(_.Price, "Number")                          
                        );

                        rowsIndex = rowsIndex + input.TotalRowsTopup + 1;

                        _exporter.AddObjectSumTableIndex(
                            sheet, styleSum, rowsIndex, columsIndex, input.SumTopup,
                            _ => "Tổng",
                            _ => "",
                            _ => "",
                            _ => CellOption.Create(_.Quantity, "Number"),
                            _ => CellOption.Create(_.Value, "Number"),
                            _ => "",
                            _ => CellOption.Create(_.Price, "Number")
                        );

                        rowsIndex = rowsIndex + 1;
                        _exporter.AddObjectSumIndex(
                            sheet, styleSum2, rowsIndex, columsIndex, input.SumTopup,
                            _ => "Tổng doanh số thu được hưởng:",
                            _ => "",
                            _ => CellOption.Create(_.Price, "Number"),
                            _ => "đồng (bao gồm VAT)"
                        );
                        sheet.AddMergedRegion(new CellRangeAddress(rowsIndex, rowsIndex, 0, 1));
                        rowsType = rowsType + 1;
                    }

                    #endregion

                    #endregion

                    #region 3.Bảng nạp tiền                   

                    if (input.DataItems.Count > 0)
                    {
                        rowsIndex = rowsIndex + 1;
                        _exporter.AddObjectRowItemColumn(sheet, rowsIndex, 0,
                            $"{ReportComparePartnerExportInfo.GetIndex(rowsType)}. ĐỐI SOÁT DỊCH VỤ DATA",
                            isCenter: false);
                        rowsIndex = rowsIndex + 1;

                        _exporter.AddHeaderStartRowIndex(
                            sheet, rowsIndex, columsIndex,
                            "Dịch vụ",
                            "Loại sản phẩm",
                            "Mệnh giá",
                            "Số lượng",
                            "Thành tiền chưa CK",
                            "Tỷ lệ CK(%)",
                            "Tiền thanh toán"
                        );
                        _exporter.AddObjectStartRowsIndex(
                            sheet, rowsIndex, columsIndex, style, input.DataItems,
                            _ => _.ServiceName,
                            _ => _.CategoryName,
                            _ => CellOption.Create(_.ProductValue, "Number"),
                            _ => CellOption.Create(_.Quantity, "Number"),
                            _ => CellOption.Create(_.Value, "Number"),
                            _ => CellOption.Create(_.DiscountRate, "Number"),
                            _ => CellOption.Create(_.Price, "Number")
                        );

                        rowsIndex = rowsIndex + input.TotalRowsData + 1;

                        _exporter.AddObjectSumTableIndex(
                            sheet, styleSum, rowsIndex, columsIndex, input.SumData,
                            _ => "Tổng",
                            _ => "",
                            _ => "",
                            _ => CellOption.Create(_.Quantity, "Number"),
                            _ => CellOption.Create(_.Value, "Number"),
                            _ => "",
                            _ => CellOption.Create(_.Price, "Number")
                        );

                        rowsIndex = rowsIndex + 1;
                        _exporter.AddObjectSumIndex(
                            sheet, styleSum2, rowsIndex, columsIndex, input.SumData,
                            _ => "Tổng doanh số thu được hưởng:",
                            _ => "",
                            _ => CellOption.Create(_.Price, "Number"),
                            _ => "đồng (bao gồm VAT)"
                        );
                        sheet.AddMergedRegion(new CellRangeAddress(rowsIndex, rowsIndex, 0, 1));
                        rowsType = rowsType + 1;
                    }                          

                    #endregion

                    #region 4.Thanh toán hóa đơn

                    if (input.PayBillItems.Count > 0)
                    {
                        rowsIndex = rowsIndex + 1;
                        _exporter.AddObjectRowItemColumn(sheet, rowsIndex, 0,
                            $"{ReportComparePartnerExportInfo.GetIndex(rowsType)}. ĐỐI SOÁT DỊCH VỤ THANH TOÁN HOÁ ĐƠN",
                            isCenter: false);
                        rowsIndex = rowsIndex + 1;

                        _exporter.AddHeaderStartRowIndex(
                            sheet, rowsIndex, columsIndex,
                            "Loại sản phẩm",
                            "Sản phẩm",
                            "Số lượng GD",
                            "Giá trị GD (chưa phí)",
                            "Phí GD",
                            "Tổng tiền phí GD",
                            "Tiền phí được hưởng"
                        );

                        _exporter.AddObjectStartRowsIndex(
                            sheet, rowsIndex, columsIndex, style, input.PayBillItems,
                            _ => _.CategoryName,
                            _ => _.ProductName,
                            _ => CellOption.Create(_.Quantity, "Number"),
                            _ => CellOption.Create(_.Value, "Number"),
                            _ => _.FeeText,
                            _ => CellOption.Create(_.Fee, "Number"),
                            _ => CellOption.Create(_.Discount, "Number")
                        );

                        rowsIndex = rowsIndex + input.TotalRowsPayBill + 1;

                        _exporter.AddObjectSumTableIndex(
                            sheet, styleSum, rowsIndex, columsIndex, input.SumPayBill,
                            _ => "Tổng",
                            _ => "",
                            _ => CellOption.Create(_.Quantity, "Number"),
                            _ => CellOption.Create(_.Value, "Number"),
                            _ => "",
                            _ => CellOption.Create(_.Fee, "Number"),
                            _ => CellOption.Create(_.Discount, "Number")
                        );

                        rowsIndex = rowsIndex + 1;
                        _exporter.AddObjectSumIndex(
                            sheet, styleSum2, rowsIndex, columsIndex, input.SumTopup,
                            _ => $"Số tiền phí {input.Provider} được hưởng:",
                            _ => "",
                            _ => CellOption.Create(input.SumPayBill.Discount, "Number"),
                            _ => "đồng (bao gồm VAT)");

                        sheet.AddMergedRegion(new CellRangeAddress(rowsIndex, rowsIndex, 0, 1));
                        rowsIndex = rowsIndex + 1;
                        _exporter.AddObjectRowItemColumn(sheet, rowsIndex, 0, "Số tiền bằng chữ:", isCenter: false);
                        rowsIndex = rowsIndex + 1;
                        _exporter.AddObjectRowItemColumn(sheet, rowsIndex, 0, "(Số tiền đã bao gồm thuế VAT)",
                            isCenter: false);
                        rowsIndex = rowsIndex + 1;
                        _exporter.AddObjectRowItemColumn(sheet, rowsIndex, 0, "Trong đó", isCenter: false);
                        rowsIndex = rowsIndex + 1;

                        var discountVat = Math.Round(input.SumPayBill.Discount / 11, 0);
                        _exporter.AddObjectSumIndex(
                            sheet, styleSum2, rowsIndex, columsIndex + 1, input.SumTopup,
                            _ => "Giá trị trước thuế :",
                            _ => CellOption.Create(input.SumPayBill.Discount - discountVat, "Number"));

                        rowsIndex = rowsIndex + 1;
                        _exporter.AddObjectSumIndex(
                            sheet, styleSum2, rowsIndex, columsIndex + 1, input.SumTopup,
                            _ => "Thuế GTGT 10% :",
                            _ => CellOption.Create(discountVat, "Number"));

                        rowsType = rowsType + 1;
                    }

                    #endregion

                    #region 5.Công nợ

                    rowsIndex = rowsIndex + 1;
                    _exporter.AddObjectRowItemColumn(sheet, rowsIndex, 0,
                        $"{ReportComparePartnerExportInfo.GetIndex(rowsType)}. CÔNG NỢ", isCenter: false);
                    rowsIndex = rowsIndex + 1;
                    _exporter.AddHeaderStartRowIndex(
                        sheet, rowsIndex, columsIndex,
                        "STT",
                        "Nội dung",
                        "Số tiền"
                    );
                    _exporter.AddObjectStartRowsIndex(
                        sheet, rowsIndex, columsIndex, style, input.BalanceItems,
                        _ => _.Index,
                        _ => _.Name,
                        _ => CellOption.Create(_.Value, "Number")
                    );

                    rowsIndex = rowsIndex + input.TotalRowsBalance + 2;
                    _exporter.AddObjectRowItemColumn(sheet, rowsIndex, 4, "Ngày.......tháng.......năm.........");
                    rowsIndex = rowsIndex + 1;
                    _exporter.AddObjectRowItemColumn(sheet, rowsIndex, 1, "CÔNG TY CỔ PHẦN VIẼN THÔNG DI ĐỘNG TOÀN CẦU");
                    _exporter.AddObjectRowItemColumn(sheet, rowsIndex, 4, input.FullName, false);

                    _log.LogInformation($"{input.Provider} ReportCompareParnerExportToFile_True");

                    #endregion
                });
        }
        catch (Exception ex)
        {
            _log.LogInformation(
                $"{input.Provider}_ReportCompareParnerExportToFile_False Exception: {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
            return null;
        }
    }

    public FileDto ReportDepositToFile(List<ReportDepositDetailDto> input, string name)
    {
        try
        {
            return _exporter.CreateExcelPackageV2(
                name,
                excelPackage =>
                {
                    var sheet = excelPackage.CreateSheet("Sheet1");
                    _exporter.AddHeaderV2(
                        sheet,
                        "Trạng thái",
                        "Đại lý",
                        "Số tiền",
                        "Ngày duyệt",
                        "Mã giao dịch"
                    );

                    _exporter.AddObjectsV2(
                        sheet, input,
                        _ => "Đã duyệt",
                        _ => _.AgentInfo,
                        _ => _.Price,
                        _ => _.CreatedTime,
                        _ => _.TransCode
                    );
                });
        }
        catch (Exception ex)
        {
            _log.LogInformation($"ReportDepositToFile Exception: {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
            return null;
        }
    }

    public FileDto ReportSaleToFile(List<ReportServiceDetailDto> input, string name)
    {
        try
        {
            return _exporter.CreateExcelPackageV2(
                name,
                excelPackage =>
                {
                    var sheet = excelPackage.CreateSheet("Sheet1");
                    _exporter.AddHeaderV2(
                        sheet,
                        "Loại đại lý",
                        "Mã đại lý",
                        "Dịch vụ",
                        "Loại sản phẩm",
                        "Tên sản phẩm",
                        "Đơn giá",
                        "Số lượng",
                        "Chiết khấu",
                        "Phí",
                        "Thành tiền",
                        "Số thụ hưởng",
                        "Thời gian",
                        "Trạng thái",
                        "Người thực hiện",
                        "Mã giao dịch",
                        "Kênh"
                    );

                    _exporter.AddObjectsV2(
                        sheet, input,
                        _ => _.AgentTypeName,
                        _ => _.AgentInfo,
                        _ => _.ServiceName,
                        _ => _.CategoryName,
                        _ => _.ProductName,
                        _ => _.Value,
                        _ => _.Quantity,
                        _ => _.Discount,
                        _ => _.Fee,
                        _ => _.Price,
                        _ => _.ReceivedAccount,
                        _ => _.CreatedTime,
                        _ => _.StatusName,
                        _ => _.UserProcess,
                        _ => _.RequestRef,
                        _ => _.Channel
                    );
                });
        }
        catch (Exception ex)
        {
            _log.LogInformation($"ReportSaleToFile Exception: {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
            return null;
        }
    }

    public FileDto ReportTransDetailToFile(List<ReportTransDetailDto> lst)
    {
        return _exporter.CreateExcelPackageV2(
            "ReportTransDetail.xlsx",
            excelPackage =>
            {
                var sheet = excelPackage.CreateSheet("Detail");
                _exporter.AddHeaderV2(
                    sheet,
                    "Trạng thái",
                    "Loại giao dịch",
                    "Nhà cung cấp",
                    "Đơn giá",
                    "Số lượng",
                    "Chiết khấu",
                    "Phí",
                    "Thu",
                    "Chi",
                    "Số dư",
                    "Tài khoản thụ hưởng",
                    "Mã giao dịch",
                    "Người thực hiện",
                    "Thời gian",
                    "Mã tham chiếu"
                );

                _exporter.AddObjectsV2(
                    sheet, lst,
                    _ => _.StatusName,
                    _ => _.TransTypeName,
                    _ => _.Vender,
                    _ => Convert.ToDouble(_.Amount),
                    _ => CellOption.Create(_.Quantity, "Number"),
                    _ => CellOption.Create(_.Discount, "Number"),
                    _ => CellOption.Create(_.Fee, "Number"),
                    _ => CellOption.Create(_.PriceIn, "Number"),
                    _ => CellOption.Create(_.PriceOut, "Number"),
                    _ => CellOption.Create(_.Balance, "Number"),
                    _ => _.AccountRef,
                    _ => _.TransCode,
                    _ => _.UserProcess,
                    _ => CellOption.Create(_.CreatedDate, "dd/MM/yyyy HH:mm:ss"),
                    _ => _.RequestTransSouce
                );
            });
    }

    public FileDto ReportDetailToFile(List<ReportDetailDto> lst)
    {
        return _exporter.CreateExcelPackageV2(
            "file.xlsx",
            excelPackage =>
            {
                var sheet = excelPackage.CreateSheet("BalanceAccount");
                _exporter.AddHeaderV2(
                    sheet,
                    "Mã giao dịch",
                    "Loại giao dịch",
                    "Ngày giao dịch",
                    "Số dư trước giao dịch",
                    "Phát sinh tăng",
                    "Phát sinh giảm",
                    "Số dư sau giao dịch",
                    "Nội dung"
                );


                _exporter.AddObjectsV2(
                    sheet, lst,
                    _ => _.TransCode,
                    _ => _.ServiceName,
                    _ => CellOption.Create(_.CreatedDate, "dd/MM/yyyy HH:mm:ss"),
                    _ => CellOption.Create(_.BalanceBefore, "Number"),
                    _ => CellOption.Create(_.Increment, "Number"),
                    _ => CellOption.Create(_.Decrement, "Number"),
                    _ => CellOption.Create(_.BalanceAfter, "Number"),
                    _ => _.TransNote
                );
            });
    }

    public FileDto ReportServiceDetailToFile(List<ReportServiceDetailDto> lst)
    {
        return _exporter.CreateExcelPackageV2(
            "file.xlsx",
            excelPackage =>
            {
                var sheet = excelPackage.CreateSheet("Sheet1");
                _exporter.AddHeaderV2(
                    sheet,
                    "Loại đại lý",
                    "Mã đại lý",
                    "NVKD",
                    "Nhà cung cấp",
                    "Dịch vụ",
                    "Loại sản phẩm",
                    "Tên sản phẩm",
                    "Đơn giá",
                    "Số lượng",
                    "Số tiền chiết khấu",
                    "Phí",
                    "Thành tiền",
                    "Hoa hồng ĐL tổng",
                    "Đại lý tổng",
                    "Số thụ hưởng",
                    "Thời gian",
                    "Trạng thái",
                    "Người thực hiện",
                    "Mã giao dịch",
                    "Mã đối tác",
                    "Mã nhà cung cấp",
                    "Kênh",
                    "Loại thuê bao",
                    "Mã NCC trả về",
                    "Loại thuê bao NCC trả về",
                    "Nhà cung cấp cha"
                );


                _exporter.AddObjectsV2(
                    sheet, lst,
                    _ => _.AgentTypeName,
                    _ => _.AgentInfo,
                    _ => _.StaffInfo,
                    _ => _.VenderName,
                    _ => _.ServiceName,
                    _ => _.CategoryName,
                    _ => _.ProductName,
                    _ => CellOption.Create(_.Value, "Number"),
                    _ => CellOption.Create(_.Quantity, "Number"),
                    _ => CellOption.Create(_.Discount, "Number"),
                    _ => CellOption.Create(_.Fee, "Number"),
                    _ => CellOption.Create(_.Price, "Number"),
                    _ => CellOption.Create(_.CommistionAmount, "Number"),
                    _ => _.AgentParentInfo,
                    _ => _.ReceivedAccount,
                    _ => CellOption.Create(_.CreatedTime, "dd/MM/yyyy HH:mm:ss"),
                    _ => _.StatusName,
                    _ => _.UserProcess,
                    _ => _.TransCode,
                    _ => _.RequestRef,
                    _ => _.PayTransRef,
                    _ => _.Channel,
                    _ => _.ReceiverType,                    
                    _ => _.ProviderTransCode,
                    _ => _.ProviderReceiverType,
                    _ => _.ParentProvider
                );
            });
    }

    public FileDto ReportRefundDetailToFile(List<ReportRefundDetailDto> lst)
    {
        return _exporter.CreateExcelPackageV2(
            "file.xlsx",
            excelPackage =>
            {
                var sheet = excelPackage.CreateSheet("Sheet1");
                _exporter.AddHeaderV2(
                    sheet,
                    "Mã đại lý",
                    "Tên cửa hàng",
                    "Mã giao dịch",
                    "Dịch vụ",
                    "Loại sản phẩm",
                    "Sản phẩm",
                    "Số tiền",
                    "Mã giao dịch gốc",
                    "Thời gian"
                );


                _exporter.AddObjectsV2(
                    sheet, lst,
                    _ => _.AgentInfo,
                    _ => _.AgentName,
                    _ => _.TransCode,
                    _ => _.ServiceName,
                    _ => _.CategoryName,
                    _ => _.ProductName,
                    _ => CellOption.Create(_.Price, "Number"),
                    _ => _.TransCodeSouce,
                    _ => CellOption.Create(_.CreatedTime, "dd/MM/yyyy HH:mm:ss")
                );
            });
    }

    public FileDto ReportStaffDetailToFile(List<ReportStaffDetail> lst)
    {
        return _exporter.CreateExcelPackageV2(
            "file.xlsx",
            excelPackage =>
            {
                var sheet = excelPackage.CreateSheet("Sheet1");
                _exporter.AddHeaderV2(
                    sheet,
                        "Thời gian",
                        "Mã giao dịch",
                        "Loại công nợ",
                        "Nội dung",
                        "Số tiền phát sinh nợ",
                        "Số tiền thanh toán",
                        "Hạn mức còn lại"
                );
                _exporter.AddObjectsV2(
                    sheet, lst,
                   _ => CellOption.Create(_.CreatedTime, "dd/MM/yyyy HH:mm:ss"),
                        _ => _.TransCode,
                        _ => _.ServiceName,
                        _ => _.Description,
                        _ => CellOption.Create(_.DebitAmount, "Number"),
                        _ => CellOption.Create(_.CreditAmount, "Number"),
                        _ => CellOption.Create(_.Balance, "Number")
                );
            });
    }

    public FileDto ReportTopupRequestLogToFile(List<ReportTopupRequestLogDto> lst)
    {
        return _exporter.CreateExcelPackageV2(
            "file.xlsx",
            excelPackage =>
            {
                var sheet = excelPackage.CreateSheet("Sheet1");
                _exporter.AddHeaderV2(
                    sheet,
                    "Mã giao dịch",
                    "Mã giao dịch đối tác",
                    "Dịch vụ",
                    "Loại sản phẩm",
                    "Mã sản phẩm",
                    "Nhà cung cấp",
                    "Mã đối tác",
                    "Thành tiền",
                    "Số thụ hưởng",
                    "Thời gian bắt đầu",
                    "Thời gian kết thúc",
                    "Trạng thái",
                    "Dữ liệu trả về từ NCC"
                );


                _exporter.AddObjectsV2(
                    sheet, lst,
                    _ => _.TransRef,
                    _ => _.TransCode,
                    _ => _.ServiceCode,
                    _ => _.CategoryCode,
                    _ => _.ProductCode,
                    _ => _.ProviderCode,
                    _ => _.PartnerCode,
                    _ => CellOption.Create(_.TransAmount, "Number"),
                    _ => _.ReceiverInfo,
                    _ => CellOption.Create(_.RequestDate, "dd/MM/yyyy HH:mm:ss"),
                    _ => CellOption.Create(_.ModifiedDate, "dd/MM/yyyy HH:mm:ss"),
                    _ => _.StatusName,
                    _ => _.ResponseInfo
                );
            });
    }
}