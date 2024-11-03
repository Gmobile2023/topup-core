using System.Collections.Generic;
using System.Threading.Tasks;
using HLS.Paygate.Shared;
using HLS.Paygate.Shared.Dtos;
using MassTransit;
using Paygate.Contracts.Commands.Worker;
using Paygate.Discovery.Requests.Workers;

namespace HLS.Paygate.Worker.Components.WorkerProcess;

public interface IWorkerProcess
{
    Task<NewMessageReponseBase<InvoiceResultDto>> BillQueryRequest(WorkerBillQueryRequest request);
    Task<NewMessageReponseBase<WorkerResult>> PayBillRequest(WorkerPayBillRequest request);

    Task<NewMessageReponseBase<WorkerResult>> TopupRequest(WorkerTopupRequest request,
        ConsumeContext<TopupRequestCommand> context = null);

    Task<NewMessageReponseBase<List<CardRequestResponseDto>>> CardSaleRequest(WorkerPinCodeRequest request);
}