using System;
using GreenPipes;
using MassTransit;
using MassTransit.ConsumeConfigurators;
using MassTransit.Definition;

namespace HLS.Paygate.Worker.Components.Consumers
{
    public class PayBillRequestConsumerDefinition :
        ConsumerDefinition<PayBillRequestConsumer>
    {
        public PayBillRequestConsumerDefinition()
        {
            ConcurrentMessageLimit = 256;
            
        }

        protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpointConfigurator,
            IConsumerConfigurator<PayBillRequestConsumer> consumerConfigurator)
        {
            endpointConfigurator.UseMessageRetry(r =>
            {
                r.Ignore<InvalidOperationException>();
                r.None();
            });
            // endpointConfigurator.UseServiceScope(_serviceProvider);
            endpointConfigurator.UseInMemoryOutbox();
            endpointConfigurator.DiscardFaultedMessages();
            // consumerConfigurator.Message<CardSaleRequestCommand>(m => m.UseFilter(new ContainerScopedFilter()));
        }
    }
}