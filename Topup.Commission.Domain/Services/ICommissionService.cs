using System.Threading.Tasks;
using Topup.Commission.Model.Dtos;
using Topup.Shared;
using Topup.Contracts.Commands.Commissions;

namespace Topup.Commission.Domain.Services;

public interface ICommissionService
{
    Task<CommissionTransactionDto> CommissionInsertAsync(CommissionTransactionDto item);
    Task<bool> CommissionUpdateAsync(CommissionTransactionDto item);
    Task<NewMessageResponseBase<object>> CommissionRequest(CommissionTransactionCommand request);
    Task<bool> CommissionUpdateStatusAsync(string transcode, CommissionTransactionStatus status);
    Task<CommissionTransactionDto> GetCommissionByRef(string transRef);
}