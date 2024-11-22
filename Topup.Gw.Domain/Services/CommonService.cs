using System;
using System.Linq;
using System.Threading.Tasks;
using Topup.Shared.CacheManager;
using Topup.Shared.ConfigDtos;
using Microsoft.Extensions.Configuration;
using Topup.Gw.Domain.Entities;
using Topup.Gw.Domain.Repositories;

namespace Topup.Gw.Domain.Services;

public class CommonService : ICommonService
{
    private readonly IConfiguration _configuration;
    private readonly IPaygateMongoRepository _repository;
    private readonly ICacheManager _cacheManager;
    private readonly WorkerConfig _workerConfig;

    public CommonService(IConfiguration configuration, IPaygateMongoRepository repository, ICacheManager cacheManager)
    {
        _configuration = configuration;
        _repository = repository;
        _cacheManager = cacheManager;
        _workerConfig = new WorkerConfig();
        configuration.GetSection("WorkerConfig").Bind(_workerConfig);
    }

    public async Task<WorkerConfig> GetWorkerConfigAsync()
    {
        try
        {
            _workerConfig.TimeOutProcess =
                await GetSettingDefaultValue("WorkerConfig:TimeOutProcess", _workerConfig.TimeOutProcess);
            _workerConfig.MaxNumOfParallelBackgroundOperations = await GetSettingDefaultValue(
                "WorkerConfig:MaxNumOfParallelBackgroundOperations",
                _workerConfig.MaxNumOfParallelBackgroundOperations);
            _workerConfig.IsEnableCheckMobileSystem =
                await GetSettingDefaultValue("WorkerConfig:IsEnableCheckMobileSystem",
                    _workerConfig.IsEnableCheckMobileSystem);
            _workerConfig.TimeoutCheckMobile = await GetSettingDefaultValue("WorkerConfig:TimeoutCheckMobile",
                _workerConfig.TimeoutCheckMobile);
            _workerConfig.IsTest = await GetSettingDefaultValue("WorkerConfig:IsTest", _workerConfig.IsTest);
            _workerConfig.IsCheckLimit =
                await GetSettingDefaultValue("WorkerConfig:IsCheckLimit", _workerConfig.IsCheckLimit);
            _workerConfig.ErrorCodeRefund =
                await GetSettingDefaultValue("WorkerConfig:ErrorCodeRefund", _workerConfig.ErrorCodeRefund);
            _workerConfig.IsEnableResponseCode = await GetSettingDefaultValue("WorkerConfig:IsEnableResponseCode",
                _workerConfig.IsEnableResponseCode);
            _workerConfig.PartnerAllowResponseConfig =
                await GetSettingDefaultValue("WorkerConfig:PartnerAllowResponseConfig",
                    _workerConfig.PartnerAllowResponseConfig);
            return _workerConfig;
        }
        catch (Exception e)
        {
            return _workerConfig;
        }
    }

    public async Task<AppSettings> GetSettingAsync(string key)
    {
        return await _repository.GetOneAsync<AppSettings>(x => x.Key == key);
    }

    public async Task<bool> SettingUpdateAsync(AppSettings item)
    {
        try
        {
            item.LastTransTime = DateTime.UtcNow;
            await _repository.UpdateOneAsync(item);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<bool> SettingCreateAsync(AppSettings item)
    {
        try
        {
            item.CreatedDate = DateTime.UtcNow;
            await _repository.AddOneAsync(item);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public Task<string> GetReferenceCodeAsync(string providerCode, string partnerCode, string transCode)
    {
        try
        {
            if (_workerConfig.IsEnableResponseCode && !string.IsNullOrEmpty(providerCode) &&
                !string.IsNullOrEmpty(_workerConfig.PartnerAllowResponseConfig))
            {
                var items = _workerConfig.PartnerAllowResponseConfig.Split(';').ToList();
                foreach (var item in items)
                {
                    var partner = item.Split('|')[0];
                    var prefix = item.Split('|')[1];
                    var provider = item.Split('|')[2];
                    if (partner == partnerCode && providerCode == provider)
                    {
                        return Task.FromResult(prefix + transCode);
                    }
                }
            }

            return Task.FromResult(transCode);
        }
        catch (Exception e)
        {
            return Task.FromResult(transCode);
        }
    }

    public async Task<int> GetSettingDefaultValue(string key, int value = 0)
    {
        var configCache = await _cacheManager.GetEntity<int>(key);
        if (configCache > 0)
            return configCache;

        var configDb = await GetSettingAsync(key);
        if (configDb == null)
        {
            configDb = new AppSettings()
            {
                Key = key,
                Value = value.ToString()
            };
            await SettingCreateAsync(configDb);
        }

        await _cacheManager.SetCache(key, configDb.Value, TimeSpan.FromDays(100));
        return int.Parse(configDb.Value);
    }

    public async Task<string> GetSettingDefaultValue(string key, string value = null)
    {
        var configCache = await _cacheManager.GetEntity<string>(key);
        if (!string.IsNullOrEmpty(configCache))
        {
            return configCache;
        }

        var configDb = await GetSettingAsync(key);
        if (configDb == null)
        {
            configDb = new AppSettings()
            {
                Key = key,
                Value = value
            };
            await SettingCreateAsync(configDb);
        }

        await _cacheManager.SetCache(key, configDb.Value, TimeSpan.FromDays(100));
        return configDb.Value;
    }

    public async Task<bool> GetSettingDefaultValue(string key, bool value = false)
    {
        var configCache = await _cacheManager.GetEntity<string>(key);
        if (!string.IsNullOrEmpty(configCache))
        {
            return bool.Parse(configCache);
        }

        var configDb = await GetSettingAsync(key);
        if (configDb == null)
        {
            configDb = new AppSettings()
            {
                Key = key,
                Value = value.ToString()
            };
            await SettingCreateAsync(configDb);
        }

        await _cacheManager.SetCache(key, configDb.Value, TimeSpan.FromDays(100));
        return bool.Parse(configDb.Value);
    }
}