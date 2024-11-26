using System.Threading.Tasks;

namespace Topup.Shared.UniqueIdGenerator;

public interface ITransCodeGenerator
{
    Task<string> SaleTransCodeGeneratorAsync(string prefix=null);
    Task<long> IncrementValueAsync(string key);
    Task<long> AutoCloseIndex(string provider, bool success);
    Task ResetAutoCloseIndex(string provider);
    Task<int> GetAutoCloseIndex(string provider);
}