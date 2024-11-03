using System;
using MassTransit;

namespace HLS.Paygate.Worker.Components.Consumers;

public class TopupRequestConsumerDefinition :
    ConsumerDefinition<TopupRequestConsumer>
{
    public TopupRequestConsumerDefinition()
    {
        ConcurrentMessageLimit = 256;
    }

    protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<TopupRequestConsumer> consumerConfigurator)
    {
        endpointConfigurator.UseMessageRetry(r =>
        {
            r.Ignore<InvalidOperationException>();
            r.None();
        });
        endpointConfigurator.PrefetchCount = 256;
        // endpointConfigurator.UseServiceScope(_serviceProvider);
        endpointConfigurator.UseInMemoryOutbox();
        // endpointConfigurator.DiscardFaultedMessages();
        // consumerConfigurator.Message<CardSaleRequestCommand>(m => m.UseFilter(new ContainerScopedFilter()));
    }
}