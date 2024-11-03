using System;
using System.Collections.Generic;
using HLS.Paygate.Shared.Common;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using OfficeOpenXml;

namespace HLS.Paygate.Report.Domain.DataExporting.Excel.EpPlus;

public interface IEpPlusExcelExporterBase
{
    FileDto CreateExcelPackage(string fileName, Action<ExcelPackage> creator);

    FileDto CreateExcelPackageV2(string fileName, Action<XSSFWorkbook> creator);

    void AddHeader(ExcelWorksheet sheet, params string[] headerTexts);

    void AddHeader(ExcelWorksheet sheet, int columnIndex, string headerText);

    void AddObjects<T>(ExcelWorksheet sheet, int startRowIndex, IList<T> items,
        params Func<T, object>[] propertySelectors);

    void AddHeaderV2(ISheet sheet, params string[] headerTexts);

    void AddObjectsV2<T>(ISheet sheet, IList<T> items,
        params Func<T, object>[] propertySelectors);

    void AddHeaderRowIndexItem(ISheet sheet,
        int rowIndex, int columnIndex, string headerText);

    void AddHeaderStartRowIndex(ISheet sheet,
        int rowsIndex, int index, params string[] headerTexts);

    void AddObjectRowItemColumn(ISheet sheet,
        int rowIndex, int columnIndex, string value, bool isNewRow = true, bool isCenter = true);

    void AddObjectStartRowsIndex<T>(ISheet sheet,
        int startRowIndex, int columnIndex,
        ICellStyle style, IList<T> items,
        params Func<T, object>[] propertySelectors);

    void AddObjectSumTableIndex<T>(ISheet sheet, ICellStyle style,
        int startRowIndex, int columnIndex, T items,
        params Func<T, object>[] propertySelectors);

    void AddObjectSumIndex<T>(ISheet sheet, ICellStyle style,
        int startRowIndex, int columnIndex, T items,
        params Func<T, object>[] propertySelectors);

    void Save(ExcelPackage excelPackage, FileDto file);

    void SaveV2(XSSFWorkbook excelPackage, FileDto file);
}