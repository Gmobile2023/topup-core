using System.Threading.Tasks;
using GMB.Topup.Commission.Model.Dtos;
using GMB.Topup.Shared;
using GMB.Topup.Contracts.Commands.Commissions;

namespace GMB.Topup.Commission.Domain.Services;

public interface ICommissionService
{
    Task<CommissionTransactionDto> CommissionInsertAsync(CommissionTransactionDto item);
    Task<bool> CommissionUpdateAsync(CommissionTransactionDto item);
    Task<NewMessageResponseBase<object>> CommissionRequest(CommissionTransactionCommand request);
    Task<bool> CommissionUpdateStatusAsync(string transcode, CommissionTransactionStatus status);
    Task<CommissionTransactionDto> GetCommissionByRef(string transRef);
}