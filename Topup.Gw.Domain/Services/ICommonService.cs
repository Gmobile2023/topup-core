using System.Threading.Tasks;
using Topup.Shared.ConfigDtos;
using Topup.Gw.Domain.Entities;

namespace Topup.Gw.Domain.Services;

public interface ICommonService
{
    Task<WorkerConfig> GetWorkerConfigAsync();
    Task<AppSettings> GetSettingAsync(string key);
    Task<bool> SettingUpdateAsync(AppSettings item);
    Task<bool> SettingCreateAsync(AppSettings item);
    Task<string> GetReferenceCodeAsync(string providerCode, string partnerCode,string transCode);

    Task<int> GetSettingDefaultValue(string key, int value = 0);
    Task<string> GetSettingDefaultValue(string key, string value = null);
    Task<bool> GetSettingDefaultValue(string key, bool value = false);
}