using System.Threading.Tasks;
using GMB.Topup.Gw.Model;
using GMB.Topup.Gw.Model.Dtos;
using GMB.Topup.Shared;

namespace GMB.Topup.Gw.Domain.Services;

public interface IPayBatchService
{
    Task<NewMessageResponseBase<BatchRequestDto>> PayBatchProcess(PayBatchRequest request);
}