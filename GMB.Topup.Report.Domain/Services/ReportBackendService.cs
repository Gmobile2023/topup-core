using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using GMB.Topup.Gw.Model.Events;
using GMB.Topup.Report.Domain.Entities;

using GMB.Topup.Report.Model.Dtos;
using GMB.Topup.Shared;
using Microsoft.Extensions.Logging;

namespace GMB.Topup.Report.Domain.Services;

public partial class BalanceReportService
{
    public async Task<bool> ReportCommistionMessage(ReportCommistionMessage message)
    {
        try
        {
            var item = await _reportMongoRepository.GetReportItemByTransCode(message.TransCode);
            if (item == null) return false;
            switch (message.Type)
            {
                case 0:
                    {
                        if (message.Status == 1)
                            item.CommissionDate = message.CommissionDate;

                        item.ParentCode = message.ParentCode;
                        item.CommissionAmount = Convert.ToDouble(message.CommissionAmount);
                        item.CommissionStatus = message.Status;
                        if (string.IsNullOrEmpty(item.ParentName))
                        {
                            var account = await GetAccountBackend(message.ParentCode);
                            if (account != null)
                                item.ParentName = account.FullName;
                        }

                        break;
                    }
                case 1:
                    item.CommissionPaidCode = message.CommissionCode;
                    item.CommissionAmount = Convert.ToDouble(message.CommissionAmount);
                    item.CommissionDate = message.CommissionDate;
                    item.CommissionStatus = 1;
                    break;
            }

            await _reportMongoRepository.UpdateOneAsync(item);
            _logger.LogInformation($"{message.TransCode}|{message.Status} ReportCommistionMessage OK.");

            return true;

        }
        catch (Exception ex)
        {
            _logger.LogError($"{message.TransCode} ReportCommistionMessage error: {ex}");
            return true;
        }
    }

    public async Task<ReportAccountDto> GetAccountBackend(string accountCode)
    {
        try
        {
            if (accountCode == "MASTER"
                || accountCode == "PAYMENT"
                || accountCode == "COMMISSION"
                || accountCode == "TEMP"
                || accountCode == "CONTROL"
                || accountCode == "CASHOUT")

                return new ReportAccountDto()
                {
                    AccountCode = accountCode,
                    FullName = accountCode,
                    Mobile = accountCode,
                    AgentType = 0,
                    AccountType = 0,
                    AgentName = "",
                };

            ReportInfoCache.Accounts ??= new List<ReportAccountDto>();
            var account = ReportInfoCache.Accounts.FirstOrDefault(c => c.AccountCode == accountCode);
            if (account != null)
                return account;

            account = await _reportMongoRepository.GetReportAccountByAccountCode(accountCode);
            if (account == null)
            {
                var sv = await _externalServiceConnector.GetUserInfoDtoAsync(accountCode);
                if (sv != null)
                {
                    account = new ReportAccountDto
                    {
                        UserId = sv.Id,
                        AccountCode = sv.AccountCode,
                        UserName = sv.UserName,
                        FullName = sv.FullName,
                        Mobile = sv.PhoneNumber,
                        AccountType = sv.AccountType,
                        AgentType = sv.AgentType,
                        AgentName = sv.AgentName,
                        ParentCode = sv.ParentCode,
                        TreePath = sv.TreePath,
                        UserSaleLeadId = sv.UserSaleLeadId ?? 0,
                        CityId = sv.Unit?.CityId ?? 0,
                        DistrictId = sv.Unit?.DistrictId ?? 0,
                        WardId = sv.Unit?.WardId ?? 0,
                        CityName = sv.Unit != null ? sv.Unit.CityName : string.Empty,
                        DistrictName = sv.Unit != null ? sv.Unit.DistrictName : string.Empty,
                        WardName = sv.Unit != null ? sv.Unit.WardName : string.Empty,
                        IdIdentity = sv.Unit != null ? sv.Unit.IdIdentity : string.Empty,
                        ChatId = sv.Unit != null ? sv.Unit.ChatId : string.Empty,
                        SaleCode = sv.SaleCode,
                        LeaderCode = sv.LeaderCode,
                        NetworkLevel = sv.NetworkLevel,
                        CreationTime = sv.CreationTime
                    };
                    await _reportMongoRepository.UpdateAccount(account);
                }
            }

            if (ReportInfoCache.Accounts.FirstOrDefault(c => c.AccountCode == accountCode) == null && account != null)
                ReportInfoCache.Accounts.Add(account);

            return account;
        }
        catch (Exception ex)
        {
            _logger.LogError($"{accountCode} GetAccountBackend  Exception: {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
            return null;
        }
    }

    public async Task<ReportProviderDto> GetProviderBackend(string providerCode)
    {
        try
        {
            ReportInfoCache.Providers ??= new List<ReportProviderDto>();
            var provider = ReportInfoCache.Providers.FirstOrDefault(c => c.ProviderCode == providerCode);
            if (provider != null)
                return provider;

            provider = await _reportMongoRepository.GetReportProviderByProviderCode(providerCode);

            if (provider == null)
            {
                var sv = await _externalServiceConnector.GetProviderInfoDtoAsync(providerCode);
                provider = new ReportProviderDto
                {
                    ProviderCode = providerCode,
                    ProviderId = sv?.Id ?? 0,
                    ProviderName = sv != null ? sv.Name : ""
                };
                if (sv != null)
                    await _reportMongoRepository.UpdateProvider(provider);
            }

            if (ReportInfoCache.Providers.FirstOrDefault(c => c.ProviderCode == providerCode) == null)
                ReportInfoCache.Providers.Add(provider);

            return provider;
        }
        catch (Exception exp)
        {
            _logger.LogError($"GetProviderBackend error: {exp}");

            return null;
        }
    }

    public async Task<ReportProductDto> GetProductBackend(string productCode)
    {
        try
        {
            ReportInfoCache.Products ??= new List<ReportProductDto>();
            var product = ReportInfoCache.Products.FirstOrDefault(c => c.ProductCode == productCode);
            if (product != null)
                return product;

            product = await _reportMongoRepository.GetReportProductByProductCode(productCode);

            if (product == null)
            {
                var sv = await _externalServiceConnector.GetProductInfoAsync(productCode);
                if (sv != null)
                {
                    product = new ReportProductDto
                    {
                        ProductId = sv.Id,
                        ProductCode = productCode,
                        ProductName = sv.ProductName,
                        ProductValue = sv.ProductValue,
                        CategoryCode = sv.CategoryCode,
                        CategoryName = sv.CategoryName,
                        CategoryId = sv.CategoryId,
                        ServiceId = sv.ServiceId,
                        ServiceCode = sv.ServiceCode,
                        ServiceName = sv.ServiceName
                    };
                    await _reportMongoRepository.UpdateProduct(product);
                }
            }

            if (ReportInfoCache.Products.FirstOrDefault(c => c.ProductCode == productCode) == null && product != null)
                ReportInfoCache.Products.Add(product);

            return product;
        }
        catch (Exception exp)
        {
            _logger.LogError($"GetProductBackend error: {exp}");

            return null;
        }
    }

    public async Task<ReportServiceDto> GetServiceBackend(string serviceCode)
    {
        try
        {
            ReportInfoCache.Services ??= new List<ReportServiceDto>();
            var service = ReportInfoCache.Services.FirstOrDefault(c => c.ServiceCode == serviceCode);
            if (service != null)
                return service;

            service = await _reportMongoRepository.GetReportServiceByServiceCode(serviceCode);
            if (service == null)
            {
                var sv = await _externalServiceConnector.GetServiceInfoDtoAsync(serviceCode);
                if (sv != null)
                {
                    service = new ReportServiceDto
                    {
                        ServiceCode = serviceCode,
                        ServiceId = sv.Id,
                        ServiceName = sv.ServicesName
                    };
                    await _reportMongoRepository.UpdateService(service);
                }
            }

            if (ReportInfoCache.Services.FirstOrDefault(c => c.ServiceCode == serviceCode) == null && service != null)
                ReportInfoCache.Services.Add(service);

            return service ?? new ReportServiceDto
            {
                ServiceCode = serviceCode,
                ServiceName = serviceCode,
                ServiceId = 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"GetServiceBackend error: {ex}");
            return null;
        }
    }

    private async Task<UserLimitDebtDto> GetLimitDebtAccount(string accountCode)
    {
        var limit = await _externalServiceConnector.GetLimitDebtAccount(accountCode);
        return limit;
    }

    public async Task<ReportVenderDto> GetVenderBackend(string venderCode)
    {
        try
        {
            if (ReportInfoCache.Venders == null) ReportInfoCache.Venders = new List<ReportVenderDto>();
            var vender = ReportInfoCache.Venders.FirstOrDefault(c => c.VenderCode == venderCode);
            if (vender != null)
                return vender;

            vender = await _reportMongoRepository.GetReportVenderByVenderCode(venderCode);
            if (vender == null)
            {
                var sv = await _externalServiceConnector.GetVenderInfoDtoAsync(venderCode);
                vender = new ReportVenderDto()
                {
                    VenderCode = venderCode,
                    VenderId = sv != null ? sv.Id : 0,
                    VenderName = sv != null ? sv.Name : "",
                };
                if (sv != null)
                    await _reportMongoRepository.UpdateVender(vender);
            }

            if (ReportInfoCache.Venders.FirstOrDefault(c => c.VenderCode == venderCode) == null && vender != null)
                ReportInfoCache.Venders.Add(vender);

            return vender;
        }
        catch (Exception exp)
        {
            _logger.LogError($"GetVenderBackend error: {exp}");
            return null;
        }
    }

    public async Task<List<ReportAccountDto>> ReportQueryAccountRequest(string accountCode)
    {
        try
        {
            Expression<Func<ReportAccountDto, bool>> query = p => true;
            if (!string.IsNullOrEmpty(accountCode))
            {
                Expression<Func<ReportAccountDto, bool>> newQuery = p =>
                    p.AccountCode == accountCode;
                query = query.And(newQuery);
            }

            var lstSearch = await _reportMongoRepository.GetAllAsync(query);
            return lstSearch;
        }
        catch (Exception ex)
        {
            _logger.LogError($"ReportQueryAccountRequest: {ex.Message}|{ex.StackTrace}|{ex.InnerException}");
            return new List<ReportAccountDto>();
        }
    }
}