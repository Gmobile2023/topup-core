using System.Threading.Tasks;
using GMB.Topup.Gw.Model.Dtos;
using GMB.Topup.Shared;

namespace GMB.Topup.Gw.Domain.Services;

public interface IValidateServiceBase
{
    Task<NewMessageResponseBase<PartnerConfigDto>> VerifyPartnerAsync(ValidateRequestDto requestDto);
}