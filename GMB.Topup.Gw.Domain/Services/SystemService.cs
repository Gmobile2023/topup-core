using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GMB.Topup.Gw.Domain.Entities;
using GMB.Topup.Gw.Domain.Repositories;
using GMB.Topup.Gw.Model.Dtos;
using GMB.Topup.Shared.CacheManager;
using Microsoft.Extensions.Logging;
using GMB.Topup.Discovery.Requests.Backends;
using ServiceStack;

namespace GMB.Topup.Gw.Domain.Services;

public class SystemService : ISystemService
{
    private readonly ICacheManager _cacheManager;
    private readonly ILogger<SystemService> _logger;
    private readonly IPaygateMongoRepository _repository;

    public SystemService(ILogger<SystemService> logger, IPaygateMongoRepository repository,
        ICacheManager cacheManager)
    {
        _logger = logger;
        _repository = repository;
        _cacheManager = cacheManager;
    }

    public async Task<bool> CreatePartnerAsync(CreatePartnerRequest item)
    {
        try
        {
            var partner = item.ConvertTo<PartnerConfig>();
            partner.ServiceConfig = item.ServiceConfigs.ToJson();
            partner.CategoryConfigs = item.CategoryConfigs.ToJson();
            partner.ProductConfigsNotAllow = item.ProductConfigsNotAllow.ToJson();
            partner.CreatedTime = DateTime.UtcNow;
            await _repository.AddOneAsync(partner);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<bool> CreateOrUpdatePartnerAsync(CreateOrUpdatePartnerRequest item)
    {
        var partner = await _repository.GetOneAsync<PartnerConfig>(x => x.PartnerCode == item.PartnerCode);
        if (partner != null) return await UpdatePartnerAsync(item.ConvertTo<UpdatePartnerRequest>());
        return await CreatePartnerAsync(item.ConvertTo<CreatePartnerRequest>());
    }

    public async Task<bool> UpdatePartnerAsync(UpdatePartnerRequest item)
    {
        try
        {
            var partner = await _repository.GetOneAsync<PartnerConfig>(x => x.PartnerCode == item.PartnerCode);
            if (partner == null)
                return false;
            partner.ModifiedDate = DateTime.UtcNow;
            if (!string.IsNullOrEmpty(item.Password) && item.Password != partner.Password)
                partner.Password = item.Password;
            if (!string.IsNullOrEmpty(item.PartnerName) && item.PartnerName != partner.PartnerName)
                partner.PartnerName = item.PartnerName;
            if (!string.IsNullOrEmpty(item.Ips) && item.Ips != partner.Ips)
                partner.Ips = item.Ips;
            if (!string.IsNullOrEmpty(item.SecretKey) && item.SecretKey != partner.SecretKey)
                partner.SecretKey = item.SecretKey;
            if (!string.IsNullOrEmpty(item.ClientId) && item.ClientId != partner.ClientId)
                partner.ClientId = item.ClientId;
            if (!string.IsNullOrEmpty(item.UserName) && item.UserName != partner.UserName)
                partner.UserName = item.UserName;
            if (item.EnableSig != partner.EnableSig)
                partner.EnableSig = item.EnableSig;
            if (!string.IsNullOrEmpty(item.PublicKeyFile) && item.PublicKeyFile != partner.PublicKeyFile)
                partner.PublicKeyFile = item.PublicKeyFile;
            if (!string.IsNullOrEmpty(item.PrivateKeyFile) && item.PrivateKeyFile != partner.PrivateKeyFile)
                partner.PrivateKeyFile = item.PrivateKeyFile;
            partner.ServiceConfig = item.ServiceConfigs.ToJson();
            partner.CategoryConfigs = item.CategoryConfigs.ToJson();
            partner.ProductConfigsNotAllow = item.ProductConfigsNotAllow.ToJson();
            partner.LastTransTimeConfig = item.LastTransTimeConfig;
            partner.IsCheckReceiverType = item.IsCheckReceiverType;
            partner.IsNoneDiscount=item.IsNoneDiscount;
            partner.IsCheckPhone=item.IsCheckReceiverType;
            partner.MaxTotalTrans = item.MaxTotalTrans;
            partner.IsCheckAllowTopupReceiverType = item.IsCheckAllowTopupReceiverType;
            partner.DefaultReceiverType = item.DefaultReceiverType;
            if (item.IsActive != partner.IsActive)
                partner.IsActive = item.IsActive;
            await _repository.UpdateOneAsync(partner);
            var key = $"PayGate_PartnerInfo:Items:{partner.PartnerCode}";
            await _cacheManager.ClearCache(key);

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<PartnerConfigDto> GetPartnerCache(string partnercode)
    {
        try
        {
            var key = $"PayGate_PartnerInfo:Items:{partnercode}";
            var response = await _cacheManager.GetEntity<PartnerConfig>(key);
            if (response != null) return response.ConvertTo<PartnerConfigDto>();
            response = await _repository.GetOneAsync<PartnerConfig>(c => c.PartnerCode == partnercode);
            if (response != null) await _cacheManager.AddEntity(key, response, TimeSpan.FromDays(300));

            return response?.ConvertTo<PartnerConfigDto>();
        }
        catch (Exception e)
        {
            _logger.Log(LogLevel.Error, "GetPartnerCache error: " + e.Message);
            return null;
        }
    }

    public async Task<List<PartnerConfigDto>> GetListPartner()
    {
        try
        {
            var rs = await _repository.GetAllAsync<PartnerConfig>(x => x.ClientId != null);
            return rs?.ConvertTo<List<PartnerConfigDto>>();
        }
        catch (Exception e)
        {
            _logger.Log(LogLevel.Error, "GetListPartner error: " + e.Message);
            return null;
        }
    }

    public async Task<List<PartnerConfigDto>> GetListPartnerCheckLastTrans()
    {
        try
        {
            var rs = await _repository.GetAllAsync<PartnerConfig>(x => x.LastTransTimeConfig != 0);
            return rs?.ConvertTo<List<PartnerConfigDto>>();
        }
        catch (Exception e)
        {
            _logger.Log(LogLevel.Error, "GetListPartner error: " + e.Message);
            return null;
        }
    }

    public async Task<bool> CreateOrUpdateServiceAsync(CreateOrUpdateServiceRequest item)
    {
        var service = await _repository.GetOneAsync<ServiceConfig>(x => x.ServiceCode == item.ServiceCode);
        if (service != null) return await UpdateServiceAsync(item);
        return await CreateServiceAsync(item);
    }

    public async Task<ServiceConfigDto> GetServiceCache(string serviceCode)
    {
        try
        {
            var key = $"PayGate_ServiceConfigInfo:Items:{serviceCode}";
            var response = await _cacheManager.GetEntity<ServiceConfig>(key);
            if (response != null) return response.ConvertTo<ServiceConfigDto>();
            response = await _repository.GetOneAsync<ServiceConfig>(c => c.ServiceCode == serviceCode);
            if (response != null) await _cacheManager.AddEntity(key, response, TimeSpan.FromDays(300));

            return response?.ConvertTo<ServiceConfigDto>();
        }
        catch (Exception e)
        {
            _logger.Log(LogLevel.Error, "GetServiceCache error: " + e.Message);
            return null;
        }
    }


    private async Task<bool> CreateServiceAsync(CreateOrUpdateServiceRequest item)
    {
        try
        {
            var partner = item.ConvertTo<ServiceConfig>();
            partner.CreatedTime = DateTime.UtcNow;
            await _repository.AddOneAsync(partner);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private async Task<bool> UpdateServiceAsync(CreateOrUpdateServiceRequest item)
    {
        try
        {
            var service = await _repository.GetOneAsync<ServiceConfig>(x => x.ServiceCode == item.ServiceCode);
            if (service == null)
                return false;
            service.ModifiedDate = DateTime.UtcNow;
            if (!string.IsNullOrEmpty(item.ServiceName) && item.ServiceName != service.ServiceName)
                service.ServiceName = item.ServiceName;
            if (!string.IsNullOrEmpty(item.Description) && item.Description != service.Description)
                service.Description = item.Description;
            if (item.IsActive != service.IsActive)
                service.IsActive = item.IsActive;

            await _repository.UpdateOneAsync(service);
            var key = $"PayGate_ServiceConfigInfo:Items:{service.ServiceCode}";
            await _cacheManager.ClearCache(key);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}