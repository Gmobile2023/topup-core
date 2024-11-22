using System.Threading.Tasks;
using Topup.Report.Model.Dtos.RequestDto;
using Topup.Shared;

namespace Topup.Report.Domain.Services;

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