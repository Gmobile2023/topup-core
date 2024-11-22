using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Topup.Gw.Model.Commands;
using Topup.Shared;
using Topup.Worker.Components.Consumers;
using Microsoft.Extensions.Logging;
using Topup.Discovery.Requests.Backends;
using Topup.Discovery.Requests.Workers;
using ServiceStack;
using Topup.Worker.Components.WorkerProcess;

namespace Topup.Worker.Components;

public class WorkerService : Service
{
    private readonly ILogger<WorkerService> _logger;
    private readonly IWorkerProcess _workerProcess;
   // private readonly UpdateStatusRequestConsumer _consumer;

    public WorkerService(IWorkerProcess workerProcess, ILogger<WorkerService> logger
       // UpdateStatusRequestConsumer consumer
        )
    {
        _workerProcess = workerProcess;
     //   _consumer = consumer;
        _logger = logger;
    }
    public async Task<object> PostAsync(WorkerTopupRequest request)
    {

        _logger.LogDebug("WorkerTopupRequest:{Request}", request.ToJson());
        Console.WriteLine($"GateCardRequest: {Request}", request.TransCode);
        var rs = await _workerProcess.TopupRequest(request);
        _logger.LogDebug("WorkerTopupRequest:{Response}", rs.ToJson());
        return rs;
    }

    public async Task<object> GetAsync(WorkerBillQueryRequest request)
    {
        Console.WriteLine($"WorkerBillQueryRequest: {Request}", request.TransCode);
        _logger.LogDebug("WorkerBillQueryRequest:{Request}", request.ToJson());
        var rs = await _workerProcess.BillQueryRequest(request);
        _logger.LogDebug("WorkerBillQueryRequest:{Response}", rs.ToJson());
        return rs;
    }

    public async Task<object> PostAsync(WorkerPayBillRequest request)
    {
        Console.WriteLine($"WorkerPayBillRequest: {Request}", request.TransCode);
        _logger.LogDebug("WorkerPayBillRequest:{Request}", request.ToJson());
        var rs = await _workerProcess.PayBillRequest(request);
        _logger.LogDebug("WorkerPayBillRequest:{Response}", rs.ToJson());
        return rs;
    }

    public async Task<object> PostAsync(WorkerPinCodeRequest request)
    {
        Console.WriteLine($"WorkerPinCodeRequest: {Request}", request.TransCode);
        _logger.LogDebug("WorkerPinCodeRequest:{Request}", request.ToJson());
        var rs = await _workerProcess.CardSaleRequest(request);
        _logger.LogDebug("WorkerPinCodeRequest:{Response}", rs.ToJson());
        return rs;
    }


    //public async Task<object> PostAsync(Worker_Test_Request request)
    //{
    //    _logger.LogDebug("Worker_Test_Request:{Request}", request.ToJson());
    //    var msg = request.Data.FromJson<CallBackTransCommand>();
    //    if (msg == null)
    //        msg = new CallBackTransCommand()
    //        {
    //            TransCode = "NT23103100000014",
    //            Amount = 0000,
    //            ProviderCode = "GATE",
    //            Status = 0,
    //            IsRefund = false,
    //        };
    //    await _consumer.ProcessCallBackTrans(msg);
    //    return null;
    //}


    public async Task<object> GetAsync(PingRouteRequest request)
    {
        return await Task.FromResult("OK");
    }
}