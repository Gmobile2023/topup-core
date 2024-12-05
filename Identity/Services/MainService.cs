using System.Threading.Tasks;
using Identity.BussinessService;
using Identity.Models;
using Identity.Route.Partner;
using IdentityModel.Client;
using Microsoft.Extensions.Logging;
using ServiceStack;
using Topup.Gw.Model.RequestDtos;
using Topup.Shared;

namespace Identity.Services;

public class MainService : Service
{
    private readonly ILogger<MainService> _logger;
    private readonly IIdentityService _identityService;

    public MainService(ILogger<MainService> logger, IIdentityService identityService)
    {
        _logger = logger;
        _identityService = identityService;
    }

    public async Task<object> PostAsync(PartnerLoginRequest request)
    {
        _logger.LogInformation($"PartnerLoginRequest {request.ClientId} {request.UserName}");
        var rs = await _identityService.LoginRequest(request.ConvertTo<LoginRequest>());
        return new PartnerResponseBase<object>(rs.ResponseStatus.ErrorCode, rs.ResponseStatus.Message)
        {
            Data = rs.Results
        };
    }

    public async Task<object> PostAsync(PartnerRefreshTokenRequest request)
    {
        _logger.LogInformation($"RefreshTokenRequest {request.ClientId}");
        var rs = await _identityService.RefreshTokenRequest(request.ConvertTo<RefreshTokenRequest>());
        return new PartnerResponseBase<object>(rs.ResponseStatus.ErrorCode, rs.ResponseStatus.Message)
        {
            Data = rs.Results
        };
    }
}