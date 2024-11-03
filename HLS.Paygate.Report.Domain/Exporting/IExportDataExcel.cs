using System.Collections.Generic;
using HLS.Paygate.Report.Domain.Entities;
using HLS.Paygate.Report.Model.Dtos;
using HLS.Paygate.Shared.Common;

namespace HLS.Paygate.Report.Domain.Exporting;

public interface IExportDataExcel
{
    FileDto ReportCardStockAutoToFile(List<ReportCardStockDayDto> input, string name);

    FileDto ReportTotalRevenueAutoToFile(List<ReportRevenueTotalAutoDto> input, string name);

    FileDto ReportBalanceSupplierAutoToFile(List<ReportBalanceSupplierDto> input, string name);

    FileDto ReportSmsAutoToFile(List<ReportSmsDto> input, string name);

    FileDto ReportCompareToFile(List<CompareReponseDto> input, string name);

    FileDto ReportRefundToFile(List<CompareReponseDto> input, string name);

    FileDto ReportCompareParnerExportToFile(ReportComparePartnerExportInfo input, string name);

    FileDto ReportDepositToFile(List<ReportDepositDetailDto> input, string name);

    FileDto ReportSaleToFile(List<ReportServiceDetailDto> input, string name);

    FileDto ReportTransDetailToFile(List<ReportTransDetailDto> lst);

    FileDto ReportDetailToFile(List<ReportDetailDto> lst);

    FileDto ReportServiceDetailToFile(List<ReportServiceDetailDto> lst);

    FileDto ReportRefundDetailToFile(List<ReportRefundDetailDto> lst);

    bool ReportServiceDetailToFileCsv(string fileName, List<ReportServiceDetailDto> list);

    bool ReportTransDetailToFileCsv(string fileName, List<ReportTransDetailDto> lst);

    bool ReportDetailToFileCsv(string fileName, List<ReportDetailDto> lst);

    FileDto ReportStaffDetailToFile(List<ReportStaffDetail> lst);

    bool ReportStaffDetailToFileCsv(string fileName, List<ReportStaffDetail> list);

    FileDto ReportTopupRequestLogToFile(List<ReportTopupRequestLogDto> lst);
    bool ReportTopupRequestLogToFileCsv(string fileName, List<ReportTopupRequestLogDto> list);
}