using System.Threading.Tasks;
using HLS.Paygate.Balance.Models.Requests;
using HLS.Paygate.Shared;
using Paygate.Discovery.Requests.Balance;

namespace HLS.Paygate.Balance.Components.Services;

/// <summary>
///     API cho đối tác
/// </summary>
public partial class MainService
{
    public async Task<object> GetAsync(AccountBalanceGetRequest accountBalanceCheckRequest)
    {
        //_logger.LogInformation("ReceivedAccountBalanceCheckRequest: {Request}", accountBalanceCheckRequest.ToJson());
        if (accountBalanceCheckRequest == null || string.IsNullOrEmpty(accountBalanceCheckRequest.PartnerCode))
            return new NewMessageReponseBase<decimal>
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
        return new NewMessageReponseBase<decimal>
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