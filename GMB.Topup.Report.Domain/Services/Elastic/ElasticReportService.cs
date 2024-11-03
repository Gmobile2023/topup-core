using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GMB.Topup.Report.Domain.Entities;
using GMB.Topup.Report.Domain.Repositories;
using GMB.Topup.Report.Model.Dtos.RequestDto;
using GMB.Topup.Shared;

namespace GMB.Topup.Report.Domain.Services;

public class ElasticReportService : IElasticReportService
{
    private readonly IElasticReportRepository _elasticReportRepository;

    public ElasticReportService(IElasticReportRepository elasticReportRepository)
    {
        _elasticReportRepository = elasticReportRepository;
    }

    public async Task<List<ReportItemDetail>> GetReportItemDetail(ReportDetailRequest request)
    {
        var a = await _elasticReportRepository.GetReportItemDetail(request);
        return null;
    }

    public async Task<MessagePagedResponseBase> ReportServiceDetailGetList(ReportServiceDetailRequest request)
    {
        return await _elasticReportRepository.ReportServiceDetailGetList(request);
    }

    public async Task<MessagePagedResponseBase> ReportRefundDetailGetList(ReportRefundDetailRequest request)
    {
        return await _elasticReportRepository.ReportRefundDetailGetList(request);
    }

    public async Task AddElasticReportItemDetail(string indexName, ReportItemDetail item)
    {
        await _elasticReportRepository.AddReportItemDetail(indexName, item);
    }


    public async Task<bool> CheckPaidTransCode(string paidTransCode)
    {
        return await _elasticReportRepository.CheckPaidTransCode(paidTransCode);
    }

    public async Task<List<string>> GetTransPaidList(DateTime date, int typeTransCode)
    {
        return await _elasticReportRepository.GetTransPaidList(date, typeTransCode);
    }


    public async Task<MessagePagedResponseBase> ReportDetailGetList(ReportDetailRequest request)
    {
        return await _elasticReportRepository.ReportDetailGetList(request);
    }

    public async Task<MessagePagedResponseBase> ReportTransDetailGetList(ReportTransDetailRequest request)
    {
        return await _elasticReportRepository.ReportTransDetailGetList(request);
    }

    public async Task<MessagePagedResponseBase> ReportCommissionDetailGetList(ReportCommissionDetailRequest request)
    {
        return await _elasticReportRepository.ReportCommissionDetailGetList(request);
    }

    public async Task<MessagePagedResponseBase> ReportCommissionTotalGetList(ReportCommissionTotalRequest request)
    {
        return await _elasticReportRepository.ReportCommissionTotalGetList(request);
    }

    public async Task<MessagePagedResponseBase> ReportCommissionAgentDetailGetList(
        ReportCommissionAgentDetailRequest request)
    {
        return await _elasticReportRepository.ReportCommissionAgentDetailGetList(request);
    }
}