using System.Threading.Tasks;
using HLS.Paygate.Gw.Model.Events;
using HLS.Paygate.Report.Model.Dtos.RequestDto;
using HLS.Paygate.Shared;

namespace HLS.Paygate.Report.Domain.Services
{
    public interface ISimBalanceReportService
    {
        Task<MessageResponseBase> SimBalanceReportInsertAsync(SimBalanceMessage request);
        Task<MessageResponseBase> CreateOrUpdateReportSimBalanceDate(SimBalanceMessage request);
        Task<MessagePagedResponseBase> SimBalanceHistories(SimBalanceHistoriesRequest request);
        Task<MessagePagedResponseBase> SimBalanceDate(SimBalanceDateRequest request);
        Task SysSimBalanceDay();
    }
}