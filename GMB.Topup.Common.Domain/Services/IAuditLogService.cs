using System.Threading.Tasks;
using GMB.Topup.Common.Model.Dtos.RequestDto;
using GMB.Topup.Shared;

namespace GMB.Topup.Common.Domain.Services;

public interface IAuditLogService
{
    Task<bool> AddAccountActivityHistory(AccountActivityHistoryRequest request);
    Task<MessagePagedResponseBase> GetAccountActivityHistories(GetAccountActivityHistoryRequest request);
}