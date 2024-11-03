using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.ComTypes;
using System.Threading.Tasks;
using NLog;
using ServiceStack.OrmLite;
using Action = HLS.Paygate.Gw.Domain.Entities.Action;
using System.Linq;
using HLS.Paygate.Gw.Domain.Entities;

namespace HLS.Paygate.Gw.Domain.Repositories
{
    public class CommandRepository : ICommandRepository
    {
        private readonly IPaygateConnectionFactory _paygateConnectionFactory;
        private readonly Logger _logger = LogManager.GetLogger("CommandRepository");

        public CommandRepository(IPaygateConnectionFactory paygateConnectionFactory)
        {
            _paygateConnectionFactory = paygateConnectionFactory;
        }

        public async Task<List<Entities.Action>> ActionsGetByCommandAsync(string commandCode)
        {
            try
            {
                using (var db = _paygateConnectionFactory.Open())
                {
                    var command = await db.SingleAsync<Command>(p => p.CommandCode == commandCode);
                    if (command == null)
                        return null;
                    await db.LoadReferencesAsync(command);
                    //command = db.LoadSingleById<Command>(command.Id);
                    //command = await db.LoadSingleByIdAsync<Command>(command.Id, new[] {"Actions"});
                    return command.Actions.Where(x => x.Status == 1).ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("ActionsGetByCommandAsync error: " + ex);
                return null;
            }

        }

        public async Task<Command> CommandInsertAsync(Command command)
        {
            try
            {
                using (var db = _paygateConnectionFactory.Open())
                {
                    var id = await db.InsertAsync(command, true);
                    command.Id = (int)id;
                    return command;
                }
            }
            catch (Exception e)
            {
                _logger.LogError("Error insert command: " + e.Message);
                return null;
            }
        }

        public async Task<bool> CommandUpdateAsync(Command command)
        {
            try
            {
                using (var db = _paygateConnectionFactory.Open())
                {
                    await db.UpdateAsync(command);
                    return true;
                }
            }
            catch (Exception e)
            {
                _logger.LogError("Error update command: " + e.Message);
                return false;
            }
        }

        public async Task<bool> CommandUpdateStatusAsync(string commandCode, byte status)
        {
            try
            {
                using (var db = _paygateConnectionFactory.Open())
                {
                    await db.UpdateOnlyAsync(new Command(), p => p.Status, p => p.CommandCode == commandCode);
                    return true;
                }
            }
            catch (Exception e)
            {
                _logger.LogError("Error update command: " + e.Message);
                return false;
            }
        }
    }
}
