using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HLS.Paygate.Report.Domain.Entities;
using HLS.Paygate.Report.Model.Dtos.RequestDto;
using HLS.Paygate.Shared;

namespace HLS.Paygate.Report.Domain.Services;

public interface IElasticReportService
{
    Task<List<ReportItemDetail>> GetReportItemDetail(ReportDetailRequest request);

    Task<MessagePagedResponseBase> ReportServiceDetailGetList(ReportServiceDetailRequest request);

    Task<MessagePagedResponseBase> ReportRefundDetailGetList(ReportRefundDetailRequest request);

    Task<MessagePagedResponseBase> ReportDetailGetList(ReportDetailRequest request);

    Task<MessagePagedResponseBase> ReportTransDetailGetList(ReportTransDetailRequest request);

    Task<MessagePagedResponseBase> ReportCommissionDetailGetList(ReportCommissionDetailRequest request);
    Task<MessagePagedResponseBase> ReportCommissionTotalGetList(ReportCommissionTotalRequest request);

    Task<MessagePagedResponseBase> ReportCommissionAgentDetailGetList(ReportCommissionAgentDetailRequest request);

    Task AddElasticReportItemDetail(string indexName, ReportItemDetail item);

    Task<bool> CheckPaidTransCode(string paidTransCode);

    Task<List<string>> GetTransPaidList(DateTime date, int typeTransCode);
}