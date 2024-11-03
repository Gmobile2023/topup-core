using System.Threading.Tasks;
using HLS.Paygate.Commission.Domain.Entities;
using HLS.Paygate.Shared;

namespace HLS.Paygate.Commission.Domain.Repositories;

public interface ICommissionRepository
{
    Task<CommissionTransaction> CommissionInsertAsync(CommissionTransaction item);
    Task<bool> CommissionUpdateAsync(CommissionTransaction item);
    Task<bool> CommissionUpdateStatusAsync(string transcode, CommissionTransactionStatus status);
    Task<CommissionTransaction> GetCommissionByRef(string transRef);
}