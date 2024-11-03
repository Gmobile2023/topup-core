using System.Threading.Tasks;
using HLS.Paygate.Backend.Interface.Hubs;
using MassTransit;
using Microsoft.AspNetCore.SignalR;
using HLS.Paygate.Gw.Model.Events;

namespace HLS.Paygate.Backend.Interface.Consumers
{
    public class SimStatusChangedConsumer : IConsumer<SimCommandSent>, IConsumer<SimCommandResponsed>
    {
        private readonly IHubContext<SimStatusHub> _hubContext;

        public SimStatusChangedConsumer(IHubContext<SimStatusHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task Consume(ConsumeContext<SimCommandSent> context)
        {
            await _hubContext.Clients.All
                //.Group(context.Message.SimNumber)
                .SendAsync("SentCommand", new {SimNumber = context.Message.SimNumber, Command = context.Message.Command });
        }

        public async Task Consume(ConsumeContext<SimCommandResponsed> context)
        {
            await _hubContext.Clients.All
                //.Group(context.Message.SimNumber)
                .SendAsync("ReceivedMessage", new { context.Message.SimNumber, Message = context.Message.Message });
        }
    }
}