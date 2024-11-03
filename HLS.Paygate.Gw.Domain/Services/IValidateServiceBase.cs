using System.Threading.Tasks;
using HLS.Paygate.Gw.Model.Dtos;
using HLS.Paygate.Shared;

namespace HLS.Paygate.Gw.Domain.Services;

public interface IValidateServiceBase
{
    Task<NewMessageReponseBase<PartnerConfigDto>> VerifyPartnerAsync(ValidateRequestDto requestDto);
}