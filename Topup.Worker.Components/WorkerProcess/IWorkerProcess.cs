using System.Collections.Generic;
using System.Threading.Tasks;
using Topup.Shared;
using Topup.Shared.Dtos;
using MassTransit;
using Topup.Contracts.Commands.Worker;
using Topup.Discovery.Requests.Workers;

namespace Topup.Worker.Components.WorkerProcess;

public interface IWorkerProcess
{
    Task<NewMessageResponseBase<InvoiceResultDto>> BillQueryRequest(WorkerBillQueryRequest request);
    Task<NewMessageResponseBase<WorkerResult>> PayBillRequest(WorkerPayBillRequest request);

    Task<NewMessageResponseBase<WorkerResult>> TopupRequest(WorkerTopupRequest request,
        ConsumeContext<TopupRequestCommand> context = null);

    Task<NewMessageResponseBase<List<CardRequestResponseDto>>> CardSaleRequest(WorkerPinCodeRequest request);
}