using System.Collections.Generic;
using System.Threading.Tasks;
using HLS.Paygate.Gw.Domain.Entities;

namespace HLS.Paygate.Gw.Domain.Repositories
{
    public interface ICommandRepository
    {
        Task<List<Action>> ActionsGetByCommandAsync(string commandCode);
        Task<Command> CommandInsertAsync(Command command);
        Task<bool> CommandUpdateAsync(Command command);
        Task<bool> CommandUpdateStatusAsync(string commandCode, byte status);
    }
}