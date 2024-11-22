using System.Threading.Tasks;
using Topup.Balance.Models.Requests;
using Topup.Discovery.Requests.Balance;
using Topup.Shared;


namespace Topup.Balance.Components.Services;

/// <summary>
///     API cho đối tác
/// </summary>
public partial class MainService
{
    public async Task<object> GetAsync(AccountBalanceGetRequest accountBalanceCheckRequest)
    {
        //_logger.LogInformation("ReceivedAccountBalanceCheckRequest: {Request}", accountBalanceCheckRequest.ToJson());
        if (accountBalanceCheckRequest == null || string.IsNullOrEmpty(accountBalanceCheckRequest.PartnerCode))
            return new NewMessageResponseBase<decimal>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error, "Yêu cầu không hợp lệ")
            };
        var request = new AccountBalanceCheckRequest
        {
            AccountCode = accountBalanceCheckRequest.PartnerCode,
            CurrencyCode = CurrencyCode.VND.ToString("G")
        };
        var rs = await _balanceService.AccountBalanceCheckAsync(request);
        //_logger.LogInformation("AccountBalanceCheckRequestReturn: {Response}", rs.ToJson());
        return new NewMessageResponseBase<decimal>
        {
            Results = rs,
            ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Success, "Success")
        };
    }

    public async Task<object> GetAsync(PingRouteRequest request)
    {
        return await Task.FromResult("OK");
    }
}