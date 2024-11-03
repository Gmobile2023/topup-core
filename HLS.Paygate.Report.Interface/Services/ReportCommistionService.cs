using System.Threading.Tasks;
using HLS.Paygate.Report.Model.Dtos.RequestDto;
using Microsoft.Extensions.Logging;
using ServiceStack;
using HLS.Paygate.Shared;
using HLS.Paygate.Shared.Dtos;
using Paygate.Discovery.Requests.Reports;

namespace HLS.Paygate.Report.Interface.Services;

public partial class ReportService
{
    public async Task<object> Get(ReportCommissionDetailRequest request)
    {
        _logger.LogInformation($"ReportCommissionDetailRequest request: {request.ToJson()}");
        var rs = _searchElastich
            ? await _elasticReportService.ReportCommissionDetailGetList(request)
            : await _balanceReportService.ReportCommissionDetailGetList(request);
        _logger.LogInformation($"ReportRoseDetailRequest return: {rs.ResponseCode} - {rs.ResponseMessage}");
        return rs;
    }

    public async Task<object> Get(ReportCommissionTotalRequest request)
    {
        _logger.LogInformation($"ReportCommissionTotalRequest request: {request.ToJson()}");
        var rs = _searchElastich
            ? await _elasticReportService.ReportCommissionTotalGetList(request)
            : await _balanceReportService.ReportCommissionTotalGetList(request);
        _logger.LogInformation($"ReportCommissionTotalRequest return: {rs.ResponseCode} - {rs.ResponseMessage}");
        return rs;
    }

    public async Task<object> Get(ReportCommissionAgentDetailRequest request)
    {
        _logger.LogInformation($"ReportCommissionAgentDetailRequest request: {request.ToJson()}");
        var rs = _searchElastich
            ? await _elasticReportService.ReportCommissionAgentDetailGetList(request)
            : await _balanceReportService.ReportCommissionAgentDetailGetList(request);
        _logger.LogInformation($"ReportCommissionAgentDetailRequest return: {rs.ResponseCode} - {rs.ResponseMessage}");
        return rs;
    }

    public async Task<object> Get(ReportCommissionAgentTotalRequest request)
    {
        _logger.LogInformation($"ReportCommissionAgentTotalRequest request: {request.ToJson()}");
        var rs = _searchElastich
            ? await _elasticReportService.ReportCommissionAgentTotalGetList(request)
            : await _balanceReportService.ReportCommissionAgentTotalGetList(request);
        _logger.LogInformation($"ReportCommissionAgentTotalRequest return: {rs.ResponseCode} - {rs.ResponseMessage}");
        return rs;
    }

    public async Task<object> Any(ReportGetAccountInfoRequest request)
    {
        _logger.LogInformation($"ReportGetAccountInfoRequest request: {request.AccountCode}");
        var item = await _balanceReportService.GetAccountBackend(request.AccountCode);
        if (item == null)
            return new NewMessageReponseBase<AccountInfoDto>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error, "Not found")
            };
        return new NewMessageReponseBase<AccountInfoDto>
        {
            ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Success, "Success"),
            Results = item.ConvertTo<AccountInfoDto>()
        };
    }
}