using System.Threading.Tasks;
using Topup.Gw.Model.RequestDtos;
using Topup.Shared;
using Topup.Gw.Domain.Entities;

namespace Topup.Gw.Domain.Services;

public interface ILimitTransAccountService
{
    Task<AccountTransLimitConfig> GetLimitAmountConfig(string accountCode, string serviceCode = null,
        string categoryCode = null, string prorudctCode = null);

    Task<MessageResponseBase> CheckLimitAccount(string accountCode, decimal amount, string serviceCode = null,
        string categoryCode = null,
        string productCode = null);

    // Task<bool> CheckAndResetLimitAmountPerTrans(string accountCode, decimal amount, string serviceCode = null,
    //     string categoryCode = null,
    //     string productCode = null);

    Task<decimal> GetAvailableLimitAccount(GetAvailableLimitAccount request);

    Task<bool> CreateOrUpdateLimitTransAccount(CreateOrUpdateLimitAccountTransRequest request);
}