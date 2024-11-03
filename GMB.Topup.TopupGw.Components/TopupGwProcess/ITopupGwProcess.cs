using System.Collections.Generic;
using System.Threading.Tasks;
using GMB.Topup.Shared;
using GMB.Topup.Shared.Dtos;
using GMB.Topup.Discovery.Requests.TopupGateways;

namespace GMB.Topup.TopupGw.Components.TopupGwProcess;

public interface ITopupGwProcess
{
    Task<NewMessageResponseBase<InvoiceResultDto>> BillQueryRequest(GateBillQueryRequest request);
    Task<NewMessageResponseBase<ResponseProvider>> PayBillRequest(GatePayBillRequest request);
    Task<NewMessageResponseBase<ResponseProvider>> TopupRequest(GateTopupRequest request);
    Task<NewMessageResponseBase<List<CardRequestResponseDto>>> GetCardToSaleRequest(GateCardRequest cardRequest);

    Task<NewMessageResponseBase<List<CardRequestResponseDto>>> GateCardBathToStockRequest(
        GateCardBathToStockRequest cardRequest);
}