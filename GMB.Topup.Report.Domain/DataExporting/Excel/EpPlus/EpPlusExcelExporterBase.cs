using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GMB.Topup.Shared.CacheManager;
using GMB.Topup.Shared.Common;
using NPOI.HSSF.Util;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using OfficeOpenXml;
using ServiceStack;

namespace GMB.Topup.Report.Domain.DataExporting.Excel.EpPlus;

public class EpPlusExcelExporterBase : IEpPlusExcelExporterBase
{
    private readonly ICacheManager _tempFileCacheManager;

    public EpPlusExcelExporterBase(ICacheManager tempFileCacheManager)
    {
        _tempFileCacheManager = tempFileCacheManager;
    }

    public FileDto CreateExcelPackage(string fileName, Action<ExcelPackage> creator)
    {
        try
        {
            var file = new FileDto(fileName, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

            using var excelPackage = new ExcelPackage();
            creator(excelPackage);
            Save(excelPackage, file);
            return file;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw new Exception("Save file fail");
        }
    }

    public FileDto CreateExcelPackageV2(string fileName, Action<XSSFWorkbook> creator)
    {
        var file = new FileDto(fileName, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        var workbook = new XSSFWorkbook();

        creator(workbook);

        SaveV2(workbook, file);

        return file;
    }

    public void AddHeader(ExcelWorksheet sheet, params string[] headerTexts)
    {
        if (headerTexts == null || !headerTexts.Any()) return;

        for (var i = 0; i < headerTexts.Length; i++) AddHeader(sheet, i + 1, headerTexts[i]);
    }

    public void AddHeader(ExcelWorksheet sheet, int columnIndex, string headerText)
    {
        sheet.Cells[1, columnIndex].Value = headerText;
        sheet.Cells[1, columnIndex].Style.Font.Bold = true;
    }

    public void AddObjects<T>(ExcelWorksheet sheet, int startRowIndex, IList<T> items,
        params Func<T, object>[] propertySelectors)
    {
        if (items == null || propertySelectors == null) return;

        for (var i = 0; i < items.Count; i++)
        for (var j = 0; j < propertySelectors.Length; j++)
            sheet.Cells[i + startRowIndex, j + 1].Value = propertySelectors[j](items[i]);
    }

    public void AddHeaderV2(ISheet sheet, params string[] headerTexts)
    {
        //if (headerTexts.IsNullOrEmpty())
        //{
        //    return;
        //}

        sheet.CreateRow(0);

        for (var i = 0; i < headerTexts.Length; i++) AddHeaderV2(sheet, i, headerTexts[i]);
    }

    public void AddObjectsV2<T>(ISheet sheet, IList<T> items,
        params Func<T, object>[] propertySelectors)
    {
        //if (items.IsNullOrEmpty() || propertySelectors.IsNullOrEmpty())
        //{
        //    return;
        //}

        for (var i = 1; i <= items.Count; i++)
        {
            var row = sheet.CreateRow(i);
            for (var j = 0; j < propertySelectors.Length; j++)
            {
                var cell = row.CreateCell(j);
                var cellObj = propertySelectors[j](items[i - 1]);
                CellOption cellData;
                if (cellObj == null)
                    cellData = CellOption.Create("");
                else if (cellObj.GetType() == typeof(CellOption))
                    cellData = cellObj.ConvertTo<CellOption>();
                else
                    cellData = CellOption.Create(cellObj);

                if (cellData != null && cellData.Value != null)
                {
                    if (cellData.Format.ToLower().StartsWith("dd") ||
                        cellData.Format.ToLower().StartsWith("mm") ||
                        cellData.Format.ToLower().StartsWith("yyyy"))
                    {
                        var dateStyle = cell.Sheet.Workbook.CreateCellStyle();
                        dateStyle.DataFormat =
                            cell.Sheet.Workbook.CreateDataFormat().GetFormat(cellData.Format);
                        cell.CellStyle = dateStyle;
                        if (DateTime.TryParse(cellData.Value.ToString(), out var datetime)) cell.SetCellValue(datetime);
                    }
                    else if (cellData.Format.ToLower() == "number")
                    {
                        cell.SetCellType(CellType.Numeric);
                        if (double.TryParse(cellData.Value.ToString(), out var data)) cell.SetCellValue(data);
                    }
                    else
                    {
                        cell.SetCellValue(cellData.Value.ToString());
                    }
                }
                else
                {
                    cell.SetCellValue("");
                }
            }
        }
    }

    public void AddHeaderStartRowIndex(ISheet sheet, int rowsIndex, int index, params string[] headerTexts)
    {
        sheet.CreateRow(rowsIndex);
        for (var i = 0; i < headerTexts.Length; i++) AddHeaderRowIndexItem(sheet, rowsIndex, index + i, headerTexts[i]);
    }

    public void AddHeaderRowIndexItem(ISheet sheet,
        int rowIndex, int columnIndex, string headerText)
    {
        var cell = sheet.GetRow(rowIndex).CreateCell(columnIndex);
        cell.SetCellValue(headerText);
        var cellStyle = sheet.Workbook.CreateCellStyle();
        cellStyle.BorderTop = BorderStyle.Thin;
        cellStyle.BorderLeft = BorderStyle.Thin;
        cellStyle.BorderRight = BorderStyle.Thin;
        cellStyle.BorderBottom = BorderStyle.Thin;
        cellStyle.FillForegroundColor = HSSFColor.Green.Index;
        cellStyle.WrapText = true;
        cellStyle.FillPattern = FillPattern.FineDots;
        cellStyle.VerticalAlignment = VerticalAlignment.Center;
        cellStyle.Alignment = HorizontalAlignment.Center;
        var font = sheet.Workbook.CreateFont();
        font.IsBold = true;
        font.FontHeightInPoints = 12;
        cellStyle.SetFont(font);
        cell.CellStyle = cellStyle;
    }

    public void AddObjectRowItemColumn(ISheet sheet,
        int rowIndex, int columnIndex, string value, bool isNewRow = true, bool isCenter = true)
    {
        if (isNewRow)
            sheet.CreateRow(rowIndex);
        var cell = sheet.GetRow(rowIndex).CreateCell(columnIndex);

        cell.SetCellValue(value);
        var cellStyle = sheet.Workbook.CreateCellStyle();
        if (isCenter)
        {
            cellStyle.VerticalAlignment = VerticalAlignment.Center;
            cellStyle.Alignment = HorizontalAlignment.Center;
        }
        else
        {
            cellStyle.VerticalAlignment = VerticalAlignment.Center;
            cellStyle.Alignment = HorizontalAlignment.Left;
        }

        var font = sheet.Workbook.CreateFont();
        font.IsBold = true;
        font.FontHeightInPoints = 12;
        cellStyle.SetFont(font);
        cell.CellStyle = cellStyle;
    }

    public void AddObjectStartRowsIndex<T>(ISheet sheet,
        int startRowIndex, int columnIndex,
        ICellStyle style, IList<T> items,
        params Func<T, object>[] propertySelectors)
    {
        for (var i = 1; i <= items.Count; i++)
        {
            var row = sheet.CreateRow(startRowIndex + i);
            for (var j = 0; j < propertySelectors.Length; j++)
            {
                var cell = row.CreateCell(j + columnIndex);
                cell.CellStyle = style;
                var cellObj = propertySelectors[j + columnIndex](items[i - 1]);
                CellOption cellData;
                if (cellObj == null)
                    cellData = CellOption.Create("");
                else if (cellObj.GetType() == typeof(CellOption))
                    cellData = cellObj.ConvertTo<CellOption>();
                else
                    cellData = CellOption.Create(cellObj);

                if (cellData != null && cellData.Value != null)
                {
                    if (cellData.Format.ToLower().StartsWith("dd") ||
                        cellData.Format.ToLower().StartsWith("mm") ||
                        cellData.Format.ToLower().StartsWith("yyyy"))
                    {
                        var dateStyle = cell.Sheet.Workbook.CreateCellStyle();
                        dateStyle.DataFormat =
                            cell.Sheet.Workbook.CreateDataFormat().GetFormat(cellData.Format);
                        cell.CellStyle = dateStyle;
                        if (DateTime.TryParse(cellData.Value.ToString(), out var datetime)) cell.SetCellValue(datetime);
                    }
                    else if (cellData.Format.ToLower() == "number")
                    {
                        cell.SetCellType(CellType.Numeric);
                        if (double.TryParse(cellData.Value.ToString(), out var data)) cell.SetCellValue(data);
                    }
                    else
                    {
                        cell.SetCellValue(cellData.Value.ToString());
                    }
                }
                else
                {
                    cell.SetCellValue("");
                }
            }
        }
    }

    public void AddObjectSumIndex<T>(ISheet sheet, ICellStyle style,
        int startRowIndex, int columnIndex, T items,
        params Func<T, object>[] propertySelectors)
    {
        var row = sheet.CreateRow(startRowIndex);
        for (var j = 0; j < propertySelectors.Length; j++)
        {
            var cell = row.CreateCell(j + columnIndex);
            cell.CellStyle = style;
            if (j == 0)
                cell.CellStyle.Alignment = HorizontalAlignment.Left;

            var cellObj = propertySelectors[j](items);
            CellOption cellData;
            if (cellObj == null)
                cellData = CellOption.Create("");
            else if (cellObj.GetType() == typeof(CellOption))
                cellData = cellObj.ConvertTo<CellOption>();
            else
                cellData = CellOption.Create(cellObj);

            if (cellData != null && cellData.Value != null)
            {
                if (cellData.Format.ToLower().StartsWith("dd") ||
                    cellData.Format.ToLower().StartsWith("mm") ||
                    cellData.Format.ToLower().StartsWith("yyyy"))
                {
                    var dateStyle = cell.Sheet.Workbook.CreateCellStyle();
                    dateStyle.DataFormat =
                        cell.Sheet.Workbook.CreateDataFormat().GetFormat(cellData.Format);
                    cell.CellStyle = dateStyle;
                    if (DateTime.TryParse(cellData.Value.ToString(), out var datetime)) cell.SetCellValue(datetime);
                }
                else if (cellData.Format.ToLower() == "number")
                {
                    cell.CellStyle.Alignment = HorizontalAlignment.Center;
                    cell.SetCellType(CellType.Numeric);
                    if (double.TryParse(cellData.Value.ToString(), out var data)) cell.SetCellValue(data);
                }
                else
                {
                    cell.CellStyle.Alignment = HorizontalAlignment.Left;
                    cell.SetCellValue(cellData.Value.ToString());
                }
            }
            else
            {
                cell.CellStyle.Alignment = HorizontalAlignment.Left;
                cell.SetCellValue("");
            }
        }
    }

    public void AddObjectSumTableIndex<T>(ISheet sheet, ICellStyle style,
        int startRowIndex, int columnIndex,
        T items,
        params Func<T, object>[] propertySelectors)
    {
        var row = sheet.CreateRow(startRowIndex);
        for (var j = 0; j < propertySelectors.Length; j++)
        {
            var cell = row.CreateCell(j + columnIndex);
            cell.CellStyle = style;
            var cellObj = propertySelectors[j](items);
            CellOption cellData;
            if (cellObj == null)
                cellData = CellOption.Create("");
            else if (cellObj.GetType() == typeof(CellOption))
                cellData = cellObj.ConvertTo<CellOption>();
            else
                cellData = CellOption.Create(cellObj);

            if (cellData != null && cellData.Value != null)
            {
                if (cellData.Format.ToLower().StartsWith("dd") ||
                    cellData.Format.ToLower().StartsWith("mm") ||
                    cellData.Format.ToLower().StartsWith("yyyy"))
                {
                    var dateStyle = cell.Sheet.Workbook.CreateCellStyle();
                    dateStyle.DataFormat =
                        cell.Sheet.Workbook.CreateDataFormat().GetFormat(cellData.Format);
                    cell.CellStyle = dateStyle;
                    if (DateTime.TryParse(cellData.Value.ToString(), out var datetime)) cell.SetCellValue(datetime);
                }
                else if (cellData.Format.ToLower() == "number")
                {
                    cell.SetCellType(CellType.Numeric);
                    if (double.TryParse(cellData.Value.ToString(), out var data)) cell.SetCellValue(data);
                }
                else
                {
                    cell.SetCellValue(cellData.Value.ToString());
                }
            }
            else
            {
                cell.SetCellValue("");
            }
        }
    }

    public void Save(ExcelPackage excelPackage, FileDto file)
    {
        _tempFileCacheManager.SetFile(file.FileToken, excelPackage.GetAsByteArray());
    }

    public void SaveV2(XSSFWorkbook excelPackage, FileDto file)
    {
        using (var stream = new MemoryStream())
        {
            excelPackage.Write(stream);
            _tempFileCacheManager.SetFile(file.FileToken, stream.ToArray());
        }
    }

    private void AddHeaderV2(ISheet sheet, int columnIndex, string headerText)
    {
        var cell = sheet.GetRow(0).CreateCell(columnIndex);
        cell.SetCellValue(headerText);
        var cellStyle = sheet.Workbook.CreateCellStyle();
        var font = sheet.Workbook.CreateFont();
        font.IsBold = true;
        font.FontHeightInPoints = 12;
        cellStyle.SetFont(font);
        cell.CellStyle = cellStyle;
    }
}

public class CellOption
{
    public object Value { get; set; }
    public string Format { get; set; }

    public static CellOption Create(object value, string format)
    {
        return new CellOption
        {
            Value = value,
            Format = format
        };
    }

    public static CellOption Create(object value)
    {
        return new CellOption
        {
            Value = value,
            Format = ""
        };
    }
}