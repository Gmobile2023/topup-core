using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Topup.Gw.Model.Dtos;
using Topup.Gw.Model.Events;
using Topup.Report.Model.Dtos;
using Topup.Report.Model.Dtos.RequestDto;
using Topup.Report.Model.Dtos.ResponseDto;
using Topup.Shared;
using Topup.Report.Domain.Entities;

namespace Topup.Report.Domain.Services;

public interface IBalanceReportService
{
    #region I.Service

    Task<MessagePagedResponseBase> ReportDetailGetList(ReportDetailRequest request);

    Task<MessagePagedResponseBase> ReportTransDetailGetList(ReportTransDetailRequest request);

    Task<ReportItemDetail> ReportTransDetailQuery(TransDetailByTransCodeRequest request);
    Task<MessagePagedResponseBase> ReportTotalDayGetList(ReportTotalDayRequest request);
    Task<MessagePagedResponseBase> ReportBalanceTotalGetList(BalanceTotalRequest request);
    Task<MessagePagedResponseBase> ReportBalanceGroupTotalGetList(BalanceGroupTotalRequest request);

    Task<MessagePagedResponseBase> ReportDebtDetailGetList(ReportDebtDetailRequest request);

    Task<MessagePagedResponseBase> ReportTotalDebtGetList(ReportTotalDebtRequest request);

    Task<MessagePagedResponseBase> ReportRefundDetailGetList(ReportRefundDetailRequest request);

    Task<MessagePagedResponseBase> ReportTransferDetailGetList(ReportTransferDetailRequest request);

    Task<MessagePagedResponseBase> ReportDepositDetailGetList(ReportDepositDetailRequest request);

    Task<MessagePagedResponseBase> ReportServiceDetailGetList(ReportServiceDetailRequest request);

    Task<MessagePagedResponseBase> ReportServiceTotalGetList(ReportServiceTotalRequest request);
    Task<MessagePagedResponseBase> ReportAgentBalanceGetList(ReportAgentBalanceRequest request);

    Task<MessagePagedResponseBase> ReportRevenueAgentGetList(ReportRevenueAgentRequest request);

    Task<MessagePagedResponseBase> ReportRevenueCityGetList(ReportRevenueCityRequest request);

    Task<MessagePagedResponseBase> ReportTotalSaleAgentGetList(ReportTotalSaleAgentRequest request);

    Task<MessagePagedResponseBase> ReportRevenueActiveGetList(ReportRevenueActiveRequest request);

    Task<MessagePagedResponseBase> ReportComparePartnerGetList(ReportComparePartnerRequest request);

    Task<MessagePagedResponseBase> ReportCommissionDetailGetList(ReportCommissionDetailRequest request);

    Task<MessagePagedResponseBase> ReportCommissionTotalGetList(ReportCommissionTotalRequest request);

    Task<MessagePagedResponseBase> ReportCommissionAgentDetailGetList(ReportCommissionAgentDetailRequest request);

    Task<MessagePagedResponseBase> ReportCommissionAgentTotalGetList(ReportCommissionAgentTotalRequest request);

    Task<MessagePagedResponseBase> ReportAgentGeneralDayGetDash(ReportAgentGeneralDashRequest request);

    Task<ReportAccountDto> GetAccountBackend(string accountCode);
    Task<ReportProviderDto> GetProviderBackend(string providerCode);
    Task<ReportProductDto> GetProductBackend(string productCode);

    Task<ReportServiceDto> GetServiceBackend(string serviceCode);

    Task SaveCompensationReportItem(ReportBalanceHistories history);

    Task SaveReportItemInfoSale(SaleRequestDto saleRequest);

    Task ExportFileDataAgent(DateTime date, List<ReportItemDetail> saleItems, List<ReportAgentBalanceDto> balanceItems, ReportFile soucePath);

    Task ExportFileBalanceHistory(DateTime date, List<ReportBalanceHistories> list, ReportFile soucePath);

    #endregion

    #region II.Queue

    Task<MessageResponseBase> CreateReportTransDetail(ReportBalanceHistoriesMessage depositRequest);
    Task ReportSaleIntMessage(ReportSaleMessage message);
    Task ReportStatusMessage(ReportTransStatusMessage request);
    Task<ReportItemDetail> ConvertInfoSale(ReportSaleMessage message);
    Task ReportCompensationHistoryMessage(ReportCompensationHistoryMessage message);

    Task<RevenueInDayDto> ReportRevenueInDayQuery(RevenueInDayRequest request);

    Task<MessagePagedResponseBase> ReportSyncAccountFullInfoRequest(ReportSyncAccounMessage message);
    Task<MessagePagedResponseBase> SyncInfoOnjectRequest(SyncInfoObjectRequest request);
    Task<MessagePagedResponseBase> ReportSyncNxtProviderRequest(SyncNXTProviderRequest request);

    Task<MessagePagedResponseBase> SyncDayRequest(SyncTotalDayRequest request);

    Task<List<ReportCardStockDayDto>> CardStockDateAuto(CardStockAutoRequest request);

    Task<List<ReportRevenueTotalAutoDto>> ReportTotal0hDateAuto(ReportTotalAuto0hRequest request);

    Task<List<ReportBalanceSupplierDto>> ReportBalanceSupplierAuto(ReportBalanceSupplierRequest request);

    Task<List<ReportSmsDto>> ReportSmsAuto(ReportSmsRequest request);
    Task<ReportRegisterInfo> GetRegisterInfo(string code, bool isCache = false);
    Task UpdateRegisterInfo(ReportRegisterInfo info);

    Task UpdateBalanceSupplierInfo(ReportBalanceSupplierDay info);

    Task<MessagePagedResponseBase> ReportRevenueDashBoardDayGetList(ReportRevenueDashBoardDayRequest request);

    Task<object> ReportSyncAgentBalanceRequest(SyncAgentBalanceRequest request);

    Task<List<ReportItemDetail>> ReportQueryItemRequest(DateTime date, string accountCode);

    Task<List<ReportBalanceHistories>> ReportQueryHistoryRequest(DateTime date, string accountCode);

    Task SysAccountBalanceDay(string accountCode, DateTime fromDate, DateTime date);

    Task SaveCompensationSouceRefund(ReportItemDetail item, ReportItemDetail itemRef);

    Task SaveBalanceHistorySouce(BalanceHistories request);

    Task<bool> ReportCommistionMessage(ReportCommistionMessage message);
    Task<bool> InsertSmsMessage(SmsMessageRequest request);
    Task ReportRefundInfoMessage(ReportRefundMessage message);

    Task SysDayOneProcess(DateTime date);

    Task<decimal> CheckTopupBalance(string providerCode);

    Task<List<ReportWarning>> GetCheckAgentBalance(SyncAgentBalanceRequest request);

    Task<object> SysAgentBalance(SyncAgentBalanceRequest request);

    Task<List<ReportItemDetail>> QueryDetailGetList(DateTime date);

    Task<bool> CompensationTranProviderOrStatus_Single(string tranCode, ReportItemDetail item);

    Task CompensationTransError(DateTime fDate, DateTime tDate);

    Task ExportFileSaleByPartner(List<ReportServiceDetailDto> list, string sourcesourcePath);

    Task<MessagePagedResponseBase> DeleteFileFpt(DateTime date);

    Task<ReportAccountBalanceDay> GetBalanceAgent(string agentCode, DateTime fromDate, DateTime toDate);

    Task<List<ReportAccountBalanceDay>> ReportQueryAccountBalanceDayRequest(DateTime date, string currencyCode, string accountCode);

    Task<List<ReportStaffDetail>> ReportQueryStaffDetailRequest(DateTime date, string accountCode);

    Task<List<ReportCardStockByDate>> ReportQueryCardStockByDateRequest(DateTime date, string stockCode);

    Task<List<ReportCardStockProviderByDate>> ReportQueryCardStockProviderByDateRequest(DateTime date);

    Task ReportRefundInfoError(ReportItemDetail itemRef);

    Task UpdateBalanceByInput(ReportAccountBalanceDay input);

    Task<List<ReportAccountDto>> ReportQueryAccountRequest(string accountCode);

    Task<List<ReportSystemDay>> GetAccountSystemDay(string dateTxt);

    Task<ReportSystemDay> GetAccountSystemDayByCode(string accountCode, string dateTxt);

    Task<bool> UpdateAccountSystemDay(ReportSystemDay dto);

    Task<double> getCheckBalance(string accountCode);

    Task SysAccountBalanceAfter(string accountCode, string currencyCode, string txtDay, ReportTransactionDetailDto dto);

    #endregion

    Task<MessagePagedResponseBase> ReportTopupRequestLogGetList(ReportTopupRequestLogs request);

    Task<ReportFile> GetForderCreate(string key, string extension = "");
    Task<string> ZipForderCreate(ReportFile sourceFile);
    Task DeleteFileNow(string key, string sourceFile);


}