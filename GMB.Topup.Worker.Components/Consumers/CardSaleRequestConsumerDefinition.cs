using System;
using GreenPipes;
using HLS.Paygate.Gw.Model.Commands;
using MassTransit;
using MassTransit.ConsumeConfigurators;
using MassTransit.Definition;

namespace HLS.Paygate.Worker.Components.Consumers
{
    public class CardSaleConsumerDefinition :
        ConsumerDefinition<CardSaleConsumer>
    {
        public CardSaleConsumerDefinition()
        {
            ConcurrentMessageLimit = 256;
        }

        protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpointConfigurator,
            IConsumerConfigurator<CardSaleConsumer> consumerConfigurator)
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