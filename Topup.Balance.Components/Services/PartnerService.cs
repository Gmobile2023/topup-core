using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ServiceStack;
using Topup.Balance.Domain.Services;
using Topup.Balance.Models.Requests;
using Topup.Discovery.Requests.Balance;
using Topup.Shared;


namespace Topup.Balance.Components.Services;

/// <summary>
///     API cho đối tác
/// </summary>
[Authenticate]
public class PartnerService : AppServiceBase
{
    private readonly IBalanceService _balanceService;
    private readonly ILogger<PartnerService> _logger;

    public PartnerService(IBalanceService balanceService, ILogger<PartnerService> logger)
    {
        _balanceService = balanceService;
        _logger = logger;
    }

    public async Task<object> GetAsync(PartnerBalanceGetRequest accountBalanceCheckRequest)
    {
        try
        {
            //_logger.LogInformation("ReceivedAccountBalanceCheckRequest: {Request}", accountBalanceCheckRequest.ToJson());
            if (accountBalanceCheckRequest == null || string.IsNullOrEmpty(accountBalanceCheckRequest.PartnerCode))
                return new PartnerResponseBase<decimal>(ResponseCodeConst.Error, "Yêu cầu không hợp lệ");
            var request = new AccountBalanceCheckRequest
            {
                AccountCode = accountBalanceCheckRequest.PartnerCode,
                CurrencyCode = CurrencyCode.VND.ToString("G")
            };
            var rs = await _balanceService.AccountBalanceCheckAsync(request);
            //_logger.LogInformation("AccountBalanceCheckRequestReturn: {Response}", rs.ToJson());
            return new PartnerResponseBase<decimal>(ResponseCodeConst.Success, "Success")
            {
                Data = rs
            };
        }
        catch (Exception e)
        {
            return new PartnerResponseBase<decimal>(ResponseCodeConst.Error, "Yêu cầu không hợp lệ");
        }
    }

    public async Task<object> GetAsync(PingRouteRequest request)
    {
        return await Task.FromResult("OK");
    }
}