using System.Threading.Tasks;

namespace HLS.Paygate.Shared.UniqueIdGenerator;

public interface IRedisGenerator
{
    Task<string> GeneratorCode(string key, string prefix);
}