using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HLS.Paygate.Report.Domain.Entities;
using HLS.Paygate.Report.Model.Dtos;
using HLS.Paygate.Report.Model.Dtos.RequestDto;
using HLS.Paygate.Report.Model.Dtos.ResponseDto;
using HLS.Paygate.Shared;

namespace HLS.Paygate.Report.Domain.Repositories;

public interface IElasticReportRepository
{
    #region A.Báo cáo

    Task<MessagePagedResponseBase> ReportServiceDetailGetList(ReportServiceDetailRequest request);

    Task<MessagePagedResponseBase> ReportRefundDetailGetList(ReportRefundDetailRequest request);

    Task<MessagePagedResponseBase> ReportDetailGetList(ReportDetailRequest request);

    Task<MessagePagedResponseBase> ReportTransDetailGetList(ReportTransDetailRequest request);

    Task<MessagePagedResponseBase> ReportTransferDetailGetList(ReportTransferDetailRequest request);

    Task<MessagePagedResponseBase> ReportServiceTotalGetList(ReportServiceTotalRequest request);

    Task<MessagePagedResponseBase> ReportServiceProviderGetList(ReportServiceProviderRequest request);

    Task<MessagePagedResponseBase> ReportRevenueCityGetList(ReportRevenueCityRequest request);

    Task<MessagePagedResponseBase> ReportAgentBalanceGetList(ReportAgentBalanceRequest request);

    Task<MessagePagedResponseBase> ReportRevenueAgentGetList(ReportRevenueAgentRequest request);

    Task<MessagePagedResponseBase> ReportRevenueActiveGetList(ReportRevenueActiveRequest request);

    Task<MessagePagedResponseBase> ReportTotalSaleAgentGetList(ReportTotalSaleAgentRequest request);

    Task<MessagePagedResponseBase> ReportCommissionDetailGetList(ReportCommissionDetailRequest request);

    Task<MessagePagedResponseBase> ReportCommissionTotalGetList(ReportCommissionTotalRequest request);

    Task<MessagePagedResponseBase> ReportCommissionAgentDetailGetList(ReportCommissionAgentDetailRequest request);

    Task<MessagePagedResponseBase> ReportRevenueDashBoardDayGetList(ReportRevenueDashBoardDayRequest request);

    Task<MessagePagedResponseBase> ReportAgentGeneralDayGetDash(ReportAgentGeneralDashRequest request);

    Task<MessagePagedResponseBase> ReportComparePartnerGetList(ReportComparePartnerRequest request);

    #endregion

    #region B.Hàm

    Task<MessagePagedResponseBase> GetReportItemDetail(ReportDetailRequest request);

    Task AddReportItemDetail(string indexName, ReportItemDetail item);

    Task AddReportItemHistory(string indexName, ReportBalanceHistories item);

    Task<bool> CheckPaidTransCode(string paidTransCode);

    Task<List<string>> GetTransPaidList(DateTime date, int typeTransCode);

    Task<List<string>> GetHistoryTempList(DateTime date);

    Task<List<ReportAccountBalanceDay>> GetAccountBalanceDayList(DateTime date, string currencyCode, string accountCode);

    Task<List<string>> GetStaffDetailList(DateTime date,string transCode);

    Task<List<ReportCardStockByDate>> GetCardStockByDateList(DateTime date);

    Task<List<ReportCardStockProviderByDate>> GetCardStockProviderByDateList(DateTime date);

    Task AddReportAccountBalanceDay(string indexName, ReportAccountBalanceDay item);

    Task AddReportStaffDetail(string indexName, ReportStaffDetail item);

    Task AddReportCardStockByDate(string indexName, ReportCardStockByDate item);

    Task AddReportCardStockProviderByDate(string indexName, ReportCardStockProviderByDate item);

    Task<MessagePagedResponseBase> ReportDebtDetailGetList(ReportDebtDetailRequest request);

    Task<ReportItemDetail> ReportTransDetailQuery(TransDetailByTransCodeRequest request);

    Task<MessagePagedResponseBase> ReportBalanceGroupTotalGetList(BalanceGroupTotalRequest request);

    Task<MessagePagedResponseBase> ReportBalanceTotalGetList(BalanceTotalRequest request);

    Task<MessagePagedResponseBase> CardStockImExPort(CardStockImExPortRequest request);

    Task<MessagePagedResponseBase> CardStockImExPortProvider(CardStockImExPortProviderRequest request);

    Task<MessagePagedResponseBase> ReportCommissionAgentTotalGetList(ReportCommissionAgentTotalRequest request);

    Task<MessagePagedResponseBase> ReportDepositDetailGetList(ReportDepositDetailRequest request);

    Task<List<ReportRevenueTotalAutoDto>> ReportTotal0hDateAuto(ReportTotalAuto0hRequest request);

    Task<MessagePagedResponseBase> ReportTotalDayGetList(ReportTotalDayRequest request);

    Task<MessagePagedResponseBase> ReportTotalDebtGetList(ReportTotalDebtRequest request);

    Task<RevenueInDayDto> ReportRevenueInDayQuery(RevenueInDayRequest request);

    Task<MessagePagedResponseBase> CardStockInventory(CardStockInventoryRequest request);

    Task<ReportAccountBalanceDay> GetBalanceAgent(string agentCode, DateTime fromDate, DateTime toDate);

    Task<ReportCheckBalance> CheckReportBalanceAndHistory(DateTime date);

    Task<ReportAccountBalanceDay> getSaleByAccount(string accountCode, DateTime date);

    Task<List<ReportItemDetail>> getSaleReportItem(DateTime date);

    Task<List<ReportBalanceHistories>> getHistoryReportItem(DateTime date);

    Task<List<ReportAgentBalanceDto>> getAccountBalanceItem(DateTime date);

    Task<List<string>> GetTransPaidListEmtry(DateTime date, int typeTransCode);

    Task<List<string>> GetAccountIndexList(string accountCode);

    Task AddReportItemAccount(string indexName, ReportAccountDto item);

    Task<List<ReportAccountBalanceDayInfo>> GetReportBalanceHistory(DateTime fDate, DateTime tDate, string accountCode);

    Task<MessagePagedResponseBase> GetCardStockHistories(CardStockHistoriesRequest request);

    Task<List<ReportCardStockDayDto>> CardStockDateAuto(CardStockAutoRequest request);

    Task<List<ReportCardStockProviderByDate>> CardStockProviderDateAuto(CardStockAutoRequest request);

    Task<MessagePagedResponseBase> ReportTopupRequestLogGetList(ReportTopupRequestLogs request);

    Task<NewMessageReponseBase<ItemMobileCheckDto>> QueryMobileTransData(ReportRegisterInfo config, CheckMobileTransRequest request);

    Task<object> RemoveKeyData(string key);

    #endregion
}