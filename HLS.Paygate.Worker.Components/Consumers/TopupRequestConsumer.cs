using System;
using System.Threading.Tasks;
using HLS.Paygate.Shared;
using HLS.Paygate.Worker.Components.WorkerProcess;
using MassTransit;
using Microsoft.Extensions.Logging;
using Paygate.Contracts.Commands.Worker;
using Paygate.Discovery.Requests.Workers;
using ServiceStack;

namespace HLS.Paygate.Worker.Components.Consumers;

public class TopupRequestConsumer : IConsumer<TopupRequestCommand>
{
    private readonly ILogger<TopupRequestConsumer> _logger;
    private readonly IWorkerProcess _workerProcess;
    private readonly IBus _bus;

    public TopupRequestConsumer(IWorkerProcess workerProcess, ILogger<TopupRequestConsumer> logger, IBus bus)
    {
        _workerProcess = workerProcess;
        _logger = logger;
        _bus = bus;
    }

    public async Task Consume(ConsumeContext<TopupRequestCommand> context)
    {
        var request = context.Message.ConvertTo<WorkerTopupRequest>();
        _logger.LogInformation(
            $"TopupRequestConsumer request: {request.TransCode}-{request.ReceiverInfo}-{request.Channel}");
        var response = await _workerProcess.TopupRequest(request, context);
        _logger.LogInformation(
            $" {request.TransCode} --  TopupRequestConsumer response: {response.ToJson()} --- {response.Results.Responsed}");

        // _ = SendTeleMessageAsync(request, response);

        if (!response.Results.Responsed)
        {
            try
            {
                _logger.LogInformation($" {request.TransCode} --> return to queue");


                await context.RespondAsync<NewMessageReponseBase<WorkerResult>>(new
                {
                    Id = context.Message.CorrelationId,
                    ReceiveTime = DateTime.Now,
                    response.ResponseStatus,
                    response.Results
                });
                _logger.LogInformation($" {request.TransCode} --> return to queue --> Oke");
            }
            catch (Exception e)
            {
                _logger.LogError(e, $" {request.TransCode} --> return to queue --> {e.Message}");
            }
        }
    }
}