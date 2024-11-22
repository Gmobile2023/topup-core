using System.Threading.Tasks;
using Orleans;

namespace Topup.Balance.Models.Grains;

public interface IAutoTransferGrain : IGrainWithStringKey, IRemindable
{
    Task Start();
}