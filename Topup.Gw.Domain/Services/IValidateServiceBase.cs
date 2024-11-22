using System.Threading.Tasks;
using Topup.Gw.Model.Dtos;
using Topup.Shared;

namespace Topup.Gw.Domain.Services;

public interface IValidateServiceBase
{
    Task<NewMessageResponseBase<PartnerConfigDto>> VerifyPartnerAsync(ValidateRequestDto requestDto);
}