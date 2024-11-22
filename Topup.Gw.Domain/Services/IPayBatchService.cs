using System.Threading.Tasks;
using Topup.Gw.Model;
using Topup.Gw.Model.Dtos;
using Topup.Shared;

namespace Topup.Gw.Domain.Services;

public interface IPayBatchService
{
    Task<NewMessageResponseBase<BatchRequestDto>> PayBatchProcess(PayBatchRequest request);
}