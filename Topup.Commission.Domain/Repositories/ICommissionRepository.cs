using System.Threading.Tasks;
using Topup.Shared;
using Topup.Commission.Domain.Entities;

namespace Topup.Commission.Domain.Repositories;

public interface ICommissionRepository
{
    Task<CommissionTransaction> CommissionInsertAsync(CommissionTransaction item);
    Task<bool> CommissionUpdateAsync(CommissionTransaction item);
    Task<bool> CommissionUpdateStatusAsync(string transcode, CommissionTransactionStatus status);
    Task<CommissionTransaction> GetCommissionByRef(string transRef);
}