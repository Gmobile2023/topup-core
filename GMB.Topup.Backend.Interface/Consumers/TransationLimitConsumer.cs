using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Configuration;
using NLog;
using HLS.Paygate.Gw.Model.Events;

namespace HLS.Paygate.Backend.Interface.Consumers
{
    public class TransationLimitConsumer : IConsumer<CheckAmountTransLimit>
    {
        private readonly IConfiguration _configuration;
        private readonly Logger _logger = LogManager.GetLogger("TransationLimitConsumer");

        public TransationLimitConsumer(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public Task Consume(ConsumeContext<CheckAmountTransLimit> context)
        {
            throw new System.NotImplementedException();
        }
    }
}
