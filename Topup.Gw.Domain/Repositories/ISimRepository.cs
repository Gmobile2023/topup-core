using System.Collections.Generic;
using System.Threading.Tasks;
using HLS.Paygate.Gw.Domain.Entities;
using HLS.Paygate.Shared;

namespace HLS.Paygate.Gw.Domain.Repositories
{
    public interface ISimRepository
    {
        Task<Sim> GetSimForMappingAsync(string stockType, SimAppType simType, SimMappingType simMappingType, int cardValue);
    }
}