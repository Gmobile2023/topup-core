using System;
using System.Threading.Tasks;
using HLS.Paygate.Gw.Domain.Services;
using HLS.Paygate.Gw.Model.Events.TopupGw;
using HLS.Paygate.Shared;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace HLS.Paygate.Worker.Components.StateMachines.AirtimeSaleStateMachineActivities;

public class UpdateSaleRequestStatus : IStateMachineActivity<AirtimeSaleState, TopupCompleted>
{
    //private readonly Logger _logger = LogManager.GetLogger("TopupRequestConsumer");
    private readonly ILogger<UpdateSaleRequestStatus> _logger;
    private readonly ISaleService _saleService;

    public UpdateSaleRequestStatus(ISaleService saleService, ILogger<UpdateSaleRequestStatus> logger)
    {
        _saleService = saleService;
        _logger = logger;
    }

    public void Probe(ProbeContext context)
    {
        context.CreateScope("topup-complated");
    }

    public void Accept(StateMachineVisitor visitor)
    {
        visitor.Visit(this);
    }

    public async Task Execute(BehaviorContext<AirtimeSaleState, TopupCompleted> context,
        IBehavior<AirtimeSaleState, TopupCompleted> next)
    {
        _logger.LogInformation("AirtimeSaleStateMachine timeout update status from result: " + context.Message.Result);
        await _saleService.SaleRequestUpdateStatusAsync(context.Saga.SaleRequest.TransCode,
            string.Empty,
            SaleRequestStatus.Failed);
    }

    public Task Faulted<TException>(BehaviorExceptionContext<AirtimeSaleState, TopupCompleted, TException> context,
        IBehavior<AirtimeSaleState, TopupCompleted> next) where TException : Exception
    {
        return next.Faulted(context);
    }
}