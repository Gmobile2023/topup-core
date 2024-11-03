using System.Collections.Generic;
using System.Threading.Tasks;
using HLS.Paygate.Shared;
using HLS.Paygate.Shared.Dtos;
using Paygate.Discovery.Requests.TopupGateways;

namespace HLS.Paygate.TopupGw.Components.TopupGwProcess;

public interface ITopupGwProcess
{
    Task<NewMessageReponseBase<InvoiceResultDto>> BillQueryRequest(GateBillQueryRequest request);
    Task<NewMessageReponseBase<ResponseProvider>> PayBillRequest(GatePayBillRequest request);
    Task<NewMessageReponseBase<ResponseProvider>> TopupRequest(GateTopupRequest request);
    Task<NewMessageReponseBase<List<CardRequestResponseDto>>> GetCardToSaleRequest(GateCardRequest cardRequest);

    Task<NewMessageReponseBase<List<CardRequestResponseDto>>> GateCardBathToStockRequest(
        GateCardBathToStockRequest cardRequest);
}