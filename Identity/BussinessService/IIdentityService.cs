using System.Threading.Tasks;
using Identity.Models;
using IdentityModel.Client;
using Topup.Shared;

namespace Identity.BussinessService;

public interface IIdentityService
{
    Task<NewMessageResponseBase<PartnerAuthResponse>> LoginRequest(LoginRequest request);
    Task<NewMessageResponseBase<PartnerAuthResponse>> RefreshTokenRequest(RefreshTokenRequest request);
}