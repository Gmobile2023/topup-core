using System.Collections.Generic;
using System.Threading.Tasks;
using GMB.Topup.Shared;
using GMB.Topup.Shared.Dtos;
using MassTransit;
using GMB.Topup.Contracts.Commands.Worker;
using GMB.Topup.Discovery.Requests.Workers;

namespace GMB.Topup.Worker.Components.WorkerProcess;

public interface IWorkerProcess
{
    Task<NewMessageResponseBase<InvoiceResultDto>> BillQueryRequest(WorkerBillQueryRequest request);
    Task<NewMessageResponseBase<WorkerResult>> PayBillRequest(WorkerPayBillRequest request);

    Task<NewMessageResponseBase<WorkerResult>> TopupRequest(WorkerTopupRequest request,
        ConsumeContext<TopupRequestCommand> context = null);

    Task<NewMessageResponseBase<List<CardRequestResponseDto>>> CardSaleRequest(WorkerPinCodeRequest request);
}