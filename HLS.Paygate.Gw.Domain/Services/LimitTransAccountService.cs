using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using HLS.Paygate.Gw.Domain.Entities;
using HLS.Paygate.Gw.Domain.Repositories;
using HLS.Paygate.Gw.Model.RequestDtos;
using HLS.Paygate.Shared;
using Microsoft.Extensions.Logging;

namespace HLS.Paygate.Gw.Domain.Services;

public class LimitTransAccountService : ILimitTransAccountService
{
    //private readonly Logger _logger = LogManager.GetLogger("LimitTransAccountService");
    private readonly ILogger<LimitTransAccountService> _logger;
    private readonly IPaygateMongoRepository _paygateMongoRepository;
    private readonly ITransactionService _transactionService;

    public LimitTransAccountService(IPaygateMongoRepository paygateMongoRepository,
        ITransactionService transactionService, ILogger<LimitTransAccountService> logger)
    {
        _paygateMongoRepository = paygateMongoRepository;
        _transactionService = transactionService;
        _logger = logger;
    }

    public async Task<AccountTransLimitConfig> GetLimitAmountConfig(string accountCode, string serviceCode,
        string categoryCode,
        string productCode)
    {
        Expression<Func<AccountTransLimitConfig, bool>> query = p =>
            p.AccountCode == accountCode;

        if (!string.IsNullOrEmpty(serviceCode))
        {
            Expression<Func<AccountTransLimitConfig, bool>> newQuery = p =>
                p.ServiceCode == serviceCode;
            query = query.And(newQuery);
        }

        if (!string.IsNullOrEmpty(categoryCode))
        {
            Expression<Func<AccountTransLimitConfig, bool>> newQuery = p =>
                p.CateroryCode == categoryCode;
            query = query.And(newQuery);
        }

        if (!string.IsNullOrEmpty(productCode))
        {
            Expression<Func<AccountTransLimitConfig, bool>> newQuery = p =>
                p.ProductCode == productCode;
            query = query.And(newQuery);
        }

        var values = await _paygateMongoRepository.GetOneAsync(query);
        return values;
    }

    public async Task<MessageResponseBase> CheckLimitAccount(string accountCode, decimal amount,
        string serviceCode = null,
        string categoryCode = null, string prorudctCode = null)
    {
        _logger.LogInformation($"CheckLimitAccount reuqest: {accountCode}-{amount}-{categoryCode}-{prorudctCode}");
        var amountConfig = await GetLimitAmountConfig(accountCode, serviceCode, categoryCode, prorudctCode);
        _logger.LogInformation(
            $"amountConfig:{amountConfig?.AccountCode}-{amountConfig?.LimitPerDay}-{amountConfig?.LimitPerTrans}");
        if (amountConfig == null)
            return new MessageResponseBase
            {
                ResponseCode = "01"
            };
        // if (amount > amountConfig.LimitPerTrans)
        //     return new MessageResponseBase
        //     {
        //         ResponseCode = ResponseCodeConst.Error,
        //         ResponseMessage =
        //             "Không thể thực hiện giao dịch. Tài khoản đã thực hiện quá hạn mức cho phép của 1 giao dịch"
        //     };
        var totalAmount =
            await _transactionService.GetTotalAmountPerDay(accountCode, serviceCode, categoryCode, prorudctCode);
        _logger.LogInformation($"Total amount in day:{totalAmount}");
        if (totalAmount + amount > amountConfig?.LimitPerDay)
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage =
                    "Không thể thực hiện giao dịch. Tài khoản đã thực hiện quá hạn mức giao dịch trong ngày"
            };
        return new MessageResponseBase
        {
            ResponseCode = "01"
        };
    }

    // public async Task<bool> CheckAndResetLimitAmountPerTrans(string accountCode, decimal amount,
    //     string serviceCode = null,
    //     string categoryCode = null, string productCode = null)
    // {
    //     // var amountConfig = await GetLimitAmountConfig(accountCode, serviceCode, categoryCode, productCode);
    //     // if (amountConfig == null || amountConfig.LimitAmount == amountConfig.LimitConfig)
    //     //     return true;
    //     // var totalAmount =
    //     //     await _transactionService.GetTotalAmountPerDay(accountCode, serviceCode, categoryCode, productCode);
    //     // if (totalAmount == 0)
    //     // {
    //     //     //reset hạn mức
    //     //     amountConfig.LimitAmount = amountConfig.LimitConfig;
    //     //     await _paygateMongoRepository.UpdateOneAsync(amountConfig);
    //     //     return true;
    //     // }
    //     //
    //     // if (totalAmount + amount > amountConfig?.LimitAmount)
    //     //     return false;
    //     // return true;
    //     return true;
    // }

    public async Task<decimal> GetAvailableLimitAccount(GetAvailableLimitAccount request)
    {
        var amountConfig = await GetLimitAmountConfig(request.AccountCode, request.ServiceCode,
            request.CategoryCode, request.ProductCode);
        if (amountConfig == null)
            return 0;
        var totalAmount =
            await _transactionService.GetTotalAmountPerDay(request.AccountCode, request.ServiceCode,
                request.CategoryCode, request.ProductCode);
        if (totalAmount == 0)
            return amountConfig.LimitPerDay;
        var balance = amountConfig.LimitPerDay - totalAmount;
        return balance < 0 ? 0 : balance;
    }

    public async Task<bool> CreateOrUpdateLimitTransAccount(CreateOrUpdateLimitAccountTransRequest request)
    {
        try
        {
            Expression<Func<AccountTransLimitConfig, bool>> query = p =>
                p.AccountCode == request.AccountCode;

            if (!string.IsNullOrEmpty(request.ServiceCode))
            {
                Expression<Func<AccountTransLimitConfig, bool>> newQuery = p =>
                    p.ServiceCode == request.ServiceCode;
                query = query.And(newQuery);
            }

            if (!string.IsNullOrEmpty(request.CategoryCode))
            {
                Expression<Func<AccountTransLimitConfig, bool>> newQuery = p =>
                    p.CateroryCode == request.CategoryCode;
                query = query.And(newQuery);
            }

            if (!string.IsNullOrEmpty(request.ProductCode))
            {
                Expression<Func<AccountTransLimitConfig, bool>> newQuery = p =>
                    p.ProductCode == request.ProductCode;
                query = query.And(newQuery);
            }

            var item = await _paygateMongoRepository.GetOneAsync(query);
            if (item != null)
            {
                item.LimitPerDay = request.LimitPerDay;
                item.LimitPerTrans = request.LimitPerTrans;
                await _paygateMongoRepository.UpdateOneAsync(item);
                return true;
            }

            await _paygateMongoRepository.AddOneAsync(new AccountTransLimitConfig
            {
                AccountCode = request.AccountCode,
                CateroryCode = request.CategoryCode,
                ServiceCode = request.ServiceCode,
                LimitPerDay = request.LimitPerDay,
                LimitPerTrans = request.LimitPerTrans,
                ProductCode = request.ProductCode
            });
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}