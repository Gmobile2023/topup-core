using System;
using System.Threading.Tasks;
using GreenPipes;
using HLS.Paygate.Gw.Model.Commands;
using MassTransit;

namespace HLS.Paygate.Worker.Components.Consumers
{
    public class ContainerScopedFilter :
        IFilter<ConsumeContext<CardSaleRequestCommand>>
    {
        public Task Send(ConsumeContext<CardSaleRequestCommand> context, IPipe<ConsumeContext<CardSaleRequestCommand>> next)
        {
            var provider = context.GetPayload<IServiceProvider>();

            Console.WriteLine("Filter ran");

            return next.Send(context);
        }

        public void Probe(ProbeContext context)
        {
        }
    }
}