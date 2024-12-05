using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Identity.Models;
using Identity.Route.Partner;
using IdentityModel;
using IdentityModel.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ServiceStack;
using Topup.Shared;
using Topup.Shared.ConfigDtos;
using Topup.Shared.Dtos;

namespace Identity.BussinessService;

public class IdentityService : IIdentityService
{
    private readonly ILogger<IdentityService> _logger;
    private readonly IdentityServerConfigDto _config;

    public IdentityService(ILogger<IdentityService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _config = new IdentityServerConfigDto();
        configuration.GetSection("OAuth").Bind(_config);
    }

    private async Task<NewMessageResponseBase<IdentityAuthResponse>> LoginIdentityServerRequest(
        LoginRequest request)
    {
        _logger.LogInformation($"LoginIdentityServerRequest request:{request.UserName}-{request.ClientId}");
        try
        {
            var client = new HttpClient();
            var disco = await client.GetDiscoveryDocumentAsync(
                new DiscoveryDocumentRequest
                {
                    Address = _config.IdentityServer.AuthorizeUrl,
                    Policy =
                    {
                        ValidateIssuerName = false,
                        RequireHttps = false
                    }
                });
            if (disco.IsError)
            {
                Console.WriteLine(disco.Error);
                return NewMessageResponseBase<IdentityAuthResponse>.Error(ResponseCodeConst.Error,
                    "Sai tên đăng nhập hoặc mật khẩu");
            }


            var rq = new PasswordTokenRequest
            {
                ClientId = request.ClientId,
                ClientSecret = request.ClientSecret,
                Address = disco.TokenEndpoint,
                Scope = request.Scope,
                UserName = request.UserName,
                Password = request.Password,
                GrantType = !string.IsNullOrEmpty(request.GrantType)
                    ? request.GrantType
                    : OidcConstants.GrantTypes.Password
            };
            var tokenResponse = await RequestPasswordTokenAsync(client, rq);
            if (tokenResponse.IsError)
            {
                _logger.LogError($"AuthenticateClient error {tokenResponse.Error}-{tokenResponse.ErrorDescription}");
                return NewMessageResponseBase<IdentityAuthResponse>.Error(
                    tokenResponse.ErrorDescription == "Tài khoản chưa đăng ký trên hệ thống"
                        ? ResponseCodeConst.PartnerNotFound
                        : ResponseCodeConst.Error,
                    tokenResponse.ErrorDescription);
            }

            if (!string.IsNullOrEmpty(tokenResponse.AccessToken))
            {
                _logger.LogInformation($"LoginIdentityServer success {request.UserName}");
                return NewMessageResponseBase<IdentityAuthResponse>.Success(new IdentityAuthResponse
                {
                    AccessToken = tokenResponse.AccessToken,
                    ExpiresIn = tokenResponse.ExpiresIn,
                    RefreshToken = tokenResponse.RefreshToken,
                    Scope = tokenResponse.Scope,
                    TokenType = tokenResponse.TokenType,
                    IdToken = tokenResponse.IdentityToken
                });
            }

            _logger.LogInformation($"LoginIdentityServer error:{request.UserName}");
            return NewMessageResponseBase<IdentityAuthResponse>.Error(ResponseCodeConst.InvalidAuth,
                "Đăng nhập không thành công");
        }
        catch (WebServiceException e)
        {
            _logger.LogError(
                $"LoginIdentityServer error:{e.StatusCode}-{e.ErrorCode}-{e.Message}-{e.ErrorMessage}-{e.ResponseBody}");
            return NewMessageResponseBase<IdentityAuthResponse>.Error(e.ResponseBody);
        }
        catch (Exception ex)
        {
            _logger.LogError($"LoginIdentityServer error:{ex}");
            return NewMessageResponseBase<IdentityAuthResponse>.Error(ResponseCodeConst.InvalidAuth,
                "Đăng nhập không thành công");
        }
    }

    private async Task<NewMessageResponseBase<IdentityAuthResponse>> RefreshTokenIdentityServerRequest(
        RefreshTokenRequest request)
    {
        _logger.LogInformation($"RefreshTokenIdentityServerRequest:{request.ClientId}");
        try
        {
            var client = new HttpClient();
            var disco = await client.GetDiscoveryDocumentAsync(
                new DiscoveryDocumentRequest
                {
                    Address = _config.IdentityServer.AuthorizeUrl,
                    Policy =
                    {
                        ValidateIssuerName = false,
                        RequireHttps = false
                    }
                });
            if (disco.IsError)
            {
                _logger.LogError(
                    $"RefreshTokenIdentityServerRequest error:{request.ClientId}-{disco.Error}-{disco.Exception}");
                return NewMessageResponseBase<IdentityAuthResponse>.Error(ResponseCodeConst.Error, disco.Error);
            }

            var rq = new RefreshTokenRequest()
            {
                ClientId = request.ClientId,
                ClientSecret = request.ClientSecret,
                Address = disco.TokenEndpoint,
                Scope = "default-api email offline_access openid phone profile",
                RefreshToken = request.RefreshToken
            };
            var tokenResponse = await client.RequestRefreshTokenAsync(rq);
            if (tokenResponse.IsError)
            {
                _logger.LogError(
                    $"RefreshTokenIdentityServerRequest error:{request.ClientId}-{tokenResponse.Error}-{tokenResponse.ErrorDescription}");
                return NewMessageResponseBase<IdentityAuthResponse>.Error(ResponseCodeConst.Error, tokenResponse.Error);
            }

            if (!string.IsNullOrEmpty(tokenResponse.AccessToken))
            {
                _logger.LogInformation($"RefreshTokenIdentityServerRequest success:{request.ClientId}");
                return NewMessageResponseBase<IdentityAuthResponse>.Success(new IdentityAuthResponse
                {
                    AccessToken = tokenResponse.AccessToken,
                    ExpiresIn = tokenResponse.ExpiresIn,
                    RefreshToken = tokenResponse.RefreshToken,
                    Scope = tokenResponse.Scope,
                    TokenType = tokenResponse.TokenType,
                    IdToken = tokenResponse.IdentityToken
                });
            }

            _logger.LogInformation($"RefreshTokenIdentityServerRequest error:{request.ClientId}");
            return NewMessageResponseBase<IdentityAuthResponse>.Error(ResponseCodeConst.InvalidAuth,
                "Đăng nhập không thành công");
        }
        catch (WebServiceException e)
        {
            _logger.LogError(
                $"RefreshTokenIdentityServerRequest error:{e.StatusCode}-{e.ErrorCode}-{e.Message}-{e.ErrorMessage}-{e.ResponseBody}");
            return NewMessageResponseBase<IdentityAuthResponse>.Error(e.ResponseBody);
        }
        catch (Exception ex)
        {
            _logger.LogError($"RefreshTokenIdentityServerRequest error:{ex}");
            return NewMessageResponseBase<IdentityAuthResponse>.Error(ResponseCodeConst.InvalidAuth,
                "Đăng nhập không thành công");
        }
    }


    private static async Task<TokenResponse> RequestPasswordTokenAsync(HttpMessageInvoker client,
        PasswordTokenRequest request, CancellationToken cancellationToken = default)
    {
        var clone = request.ConvertTo<TokenRequest>();
        clone.Parameters.AddRequired(OidcConstants.TokenRequest.GrantType, clone.GrantType);
        clone.Parameters.AddRequired(OidcConstants.TokenRequest.UserName, request.UserName);
        clone.Parameters.AddRequired(OidcConstants.TokenRequest.Password, request.Password, allowEmptyValue: true);
        clone.Parameters.AddOptional(OidcConstants.TokenRequest.Scope, request.Scope);
        foreach (var resource in request.Resource)
        {
            clone.Parameters.AddRequired(OidcConstants.TokenRequest.Resource, resource, allowDuplicates: true);
        }

        return await client.RequestTokenAsync(clone, cancellationToken).ConfigureAwait(false);
    }

    public async Task<NewMessageResponseBase<PartnerAuthResponse>> LoginRequest(LoginRequest request)
    {
        try
        {
            var loginResponse = await LoginIdentityServerRequest(new LoginRequest
            {
                ClientId = request.ClientId,
                ClientSecret = request.ClientSecret,
                UserName = request.UserName,
                Password = request.Password,
                Scope = request.Scope
            });
            if (loginResponse.ResponseStatus.ErrorCode != ResponseCodeConst.Success)
                return NewMessageResponseBase<PartnerAuthResponse>.Error(loginResponse.ResponseStatus.ErrorCode,
                    loginResponse.ResponseStatus.Message);
            return NewMessageResponseBase<PartnerAuthResponse>.Success(new PartnerAuthResponse
            {
                AccessToken = loginResponse.Results.AccessToken,
                ExpiresIn = loginResponse.Results.ExpiresIn,
                IdToken = loginResponse.Results.IdToken,
                RefreshToken = loginResponse.Results.RefreshToken
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"AppLoginRequest error:{ex}");
            return NewMessageResponseBase<PartnerAuthResponse>.Error(ResponseCodeConst.InvalidAuth,
                "Đăng nhập không thành công");
        }
    }

    public async Task<NewMessageResponseBase<PartnerAuthResponse>> RefreshTokenRequest(RefreshTokenRequest request)
    {
        try
        {
            var loginResponse = await RefreshTokenIdentityServerRequest(new RefreshTokenRequest
            {
                ClientId = request.ClientId,
                ClientSecret = request.ClientSecret,
                RefreshToken = request.RefreshToken
            });
            if (loginResponse.ResponseStatus.ErrorCode != ResponseCodeConst.Success)
                return NewMessageResponseBase<PartnerAuthResponse>.Error(loginResponse.ResponseStatus.ErrorCode,
                    loginResponse.ResponseStatus.Message);
            return NewMessageResponseBase<PartnerAuthResponse>.Success(new PartnerAuthResponse
            {
                AccessToken = loginResponse.Results.AccessToken,
                RefreshToken = loginResponse.Results.RefreshToken,
                ExpiresIn = loginResponse.Results.ExpiresIn,
                IdToken = loginResponse.Results.IdToken
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"RefreshTokenRequest error:{ex}");
            return NewMessageResponseBase<PartnerAuthResponse>.Error(ResponseCodeConst.InvalidAuth,
                "Không thành công");
        }
    }
}