using System.Threading.Tasks;
using HLS.Paygate.Commission.Model.Dtos;
using HLS.Paygate.Shared;
using Paygate.Contracts.Commands.Commissions;

namespace HLS.Paygate.Commission.Domain.Services;

public interface ICommissionService
{
    Task<CommissionTransactionDto> CommissionInsertAsync(CommissionTransactionDto item);
    Task<bool> CommissionUpdateAsync(CommissionTransactionDto item);
    Task<NewMessageReponseBase<object>> CommissionRequest(CommissionTransactionCommand request);
    Task<bool> CommissionUpdateStatusAsync(string transcode, CommissionTransactionStatus status);
    Task<CommissionTransactionDto> GetCommissionByRef(string transRef);
}