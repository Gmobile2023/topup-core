using System;
using System.Threading.Tasks;
using Topup.Contracts.Commands.Backend;
using Topup.Shared.UniqueIdGenerator;
using MassTransit;
using Microsoft.Extensions.Logging;


namespace Topup.Backend.Interface.Consumers
{
    public class ProviderBackendConsumer : IConsumer<ResetAutoLockProviderCommand>
    {
        private readonly ILogger<ProviderBackendConsumer> _logger;
        private readonly ITransCodeGenerator _transCodeGenerator;

        public ProviderBackendConsumer(ILogger<ProviderBackendConsumer> logger, ITransCodeGenerator transCodeGenerator)
        {
            _logger = logger;
            _transCodeGenerator = transCodeGenerator;
        }

        public async Task Consume(ConsumeContext<ResetAutoLockProviderCommand> context)
        {
            try
            {
                _logger.LogInformation("ResetAutoLockProviderCommand request: " + context.Message.ProviderCode);
                await _transCodeGenerator.ResetAutoCloseIndex(context.Message.ProviderCode); //
            }
            catch (Exception e)
            {
                _logger.LogError($"ResetAutoLockProviderCommand error: {e}");
            }
        }
    }
}