using System.Threading.Tasks;

namespace Topup.Shared.UniqueIdGenerator;

public interface IRedisGenerator
{
    Task<string> GeneratorCode(string key, string prefix);
}