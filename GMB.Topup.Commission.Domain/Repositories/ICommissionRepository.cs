using System.Threading.Tasks;
using GMB.Topup.Commission.Domain.Entities;
using GMB.Topup.Shared;

namespace GMB.Topup.Commission.Domain.Repositories;

public interface ICommissionRepository
{
    Task<CommissionTransaction> CommissionInsertAsync(CommissionTransaction item);
    Task<bool> CommissionUpdateAsync(CommissionTransaction item);
    Task<bool> CommissionUpdateStatusAsync(string transcode, CommissionTransactionStatus status);
    Task<CommissionTransaction> GetCommissionByRef(string transRef);
}