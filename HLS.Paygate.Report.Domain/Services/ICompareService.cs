using System.Threading.Tasks;
using HLS.Paygate.Report.Model.Dtos.RequestDto;
using HLS.Paygate.Shared;

namespace HLS.Paygate.Report.Domain.Services;

public interface ICompareService
{
    Task<MessagePagedResponseBase> ReportCompareGetList(ReportCompareListRequest request);


    Task<MessagePagedResponseBase> ReportCheckCompareGet(ReportCheckCompareRequest request);

    Task<MessagePagedResponseBase> ReportCompareRefundDetail(ReportCompareRefundDetailRequest request);

    Task<MessagePagedResponseBase> ReportCompareRefundList(ReportCompareRefundRequest request);

    Task<MessageResponseBase> ReportCompareRefundSingle(ReportCompareRefundSingleRequest request);

    Task<MessagePagedResponseBase> ReportCompareReonseList(ReportCompareReonseRequest request);
    Task<MessagePagedResponseBase> ReportCompareDetailReonseList(ReportCompareDetailReonseRequest request);

    Task<MessagePagedResponseBase> CompareProviderData(CompareProviderRequest request);

    Task<MessagePagedResponseBase> RefundCompareData(CompareRefundCompareRequest request);
}