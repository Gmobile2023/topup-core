using System.Threading.Tasks;
using Orleans;

namespace GMB.Topup.Balance.Models.Grains;

public interface IAutoTransferGrain : IGrainWithStringKey, IRemindable
{
    Task Start();
}