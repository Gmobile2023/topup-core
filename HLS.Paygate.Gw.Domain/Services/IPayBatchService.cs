using System.Threading.Tasks;
using HLS.Paygate.Gw.Model;
using HLS.Paygate.Gw.Model.Dtos;
using HLS.Paygate.Shared;

namespace HLS.Paygate.Gw.Domain.Services;

public interface IPayBatchService
{
    Task<NewMessageReponseBase<BatchRequestDto>> PayBatchProcess(PayBatchRequest request);
}