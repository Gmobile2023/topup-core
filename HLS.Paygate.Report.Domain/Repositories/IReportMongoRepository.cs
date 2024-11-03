using System;
using System.Linq;
using System.Threading.Tasks;
using HLS.Paygate.Report.Domain.Entities;
using HLS.Paygate.Shared;
using MongoDbGenericRepository;

namespace HLS.Paygate.Report.Domain.Repositories;

public interface IReportMongoRepository : IBaseMongoRepository
{
    IQueryable<TDocument> GetQueryable<TDocument>();

    Task<ReportAccountBalanceDay> GetReportAccountBalanceDayAsync(string accountCode, string currencyCode,
        DateTime date);

    Task<ReportBalanceHistories> GetReportBalanceHistoriesByTransCode(string transCode);

    Task<ReportAccountDto> GetReportAccountByUserId(int userId);

    Task<ReportAccountBalanceDay> GetReportAccountBalanceDayBy(string accountCode, DateTime date);

    Task<ReportCardStockByDate> GetReportCardStockByDate(string stockType, string stockcode, string productcode,
        DateTime date);

    Task<ReportCardStockProviderByDate> GetReportCardStockProviderByDate(string providerCode,
        string stockType, string stockcode, string productcode);

    Task<ReportItemDetail> GetReportItemByPaidTransCode(string transCode);
    Task<ReportItemDetail> GetReportItemByTransCode(string transCode);

    Task<ReportItemDetail> GetReportItemByTransSouce(string transCode);
    Task<ReportAccountDto> GetReportAccountByAccountCode(string accountCode);

    Task<ReportProductDto> GetReportProductByProductCode(string productCode);

    Task<ReportServiceDto> GetReportServiceByServiceCode(string serviceCode);

    Task<ReportVenderDto> GetReportVenderByVenderCode(string venderCode);

    Task<ReportServiceDto> GetReportServiceByServiceId(int serviceId);

    Task<ReportProviderDto> GetReportProviderByProviderCode(string providerCode);    

    Task UpdateProduct(ReportProductDto product);

    Task UpdateAccount(ReportAccountDto account);

    Task UpdateService(ReportServiceDto service);

    Task UpdateVender(ReportVenderDto venderDto);

    Task UpdateProvider(ReportProviderDto provider);

    Task UpdateReportStatus(string requestRef, ReportStatus status);

    Task<ReportAccountBalanceDay> GetReportAccountBalanceDayOpenAsync(string accountCode,
        string currencyCode, string date);

    Task InsertWaringInfo(ReportItemWarning info);

    Task<bool> UpdateWaringInfo(ReportItemWarning info);

    Task InsertFileFptInfo(ReportFileFpt info);
}