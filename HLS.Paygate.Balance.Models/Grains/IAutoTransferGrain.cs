using System.Threading.Tasks;
using Orleans;

namespace HLS.Paygate.Balance.Models.Grains;

public interface IAutoTransferGrain : IGrainWithStringKey, IRemindable
{
    Task Start();
}