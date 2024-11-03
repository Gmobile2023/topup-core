using System.Threading.Tasks;
using HLS.Paygate.Common.Model.Dtos.RequestDto;
using HLS.Paygate.Shared;

namespace HLS.Paygate.Common.Domain.Services;

public interface IAuditLogService
{
    Task<bool> AddAccountActivityHistory(AccountActivityHistoryRequest request);
    Task<MessagePagedResponseBase> GetAccountActivityHistories(GetAccountActivityHistoryRequest request);
}