using System.Threading.Tasks;
using Topup.Common.Model.Dtos.RequestDto;
using Topup.Shared;

namespace Topup.Common.Domain.Services;

public interface IAuditLogService
{
    Task<bool> AddAccountActivityHistory(AccountActivityHistoryRequest request);
    Task<MessagePagedResponseBase> GetAccountActivityHistories(GetAccountActivityHistoryRequest request);
}