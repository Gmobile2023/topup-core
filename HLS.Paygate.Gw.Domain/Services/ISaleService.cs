using System.Collections.Generic;
using System.Threading.Tasks;
using HLS.Paygate.Gw.Model;
using HLS.Paygate.Gw.Model.Dtos;
using HLS.Paygate.Gw.Model.RequestDtos;
using HLS.Paygate.Shared;
using HLS.Paygate.Shared.Dtos;
using Paygate.Discovery.Requests.Backends;

namespace HLS.Paygate.Gw.Domain.Services;

public interface ISaleService
{
    Task<SaleRequestDto> SaleRequestCreateAsync(SaleRequestDto saleRequestDto);
    Task<SaleRequestDto> SaleRequestUpdateAsync(SaleRequestDto saleRequestDto);
    Task<MessageResponseBase> SaleRequestCheckAsync(string transCode, string partnerCode);

    Task<bool> SaleRequestUpdateStatusAsync(string transCode, string provider, SaleRequestStatus status,
        string transCodeProvider = "", bool isBackend = false, string providerResponseTransCode = "",
        string receiverType = "", string referenceCode = "");

    Task<bool> SaleItemUpdateStatus(string transCode, SaleRequestStatus status);
    Task<MessagePagedResponseBase> SaleRequestGetListAsync(TopupListGetRequest topupListGetRequest);
    Task<SaleRequestDto> SaleRequestGetAsync(string transCode);

    Task<SaleRequestDto> SaleRequestGetTransRefAsync(string transRef, string partnerCode);

    Task<bool> SaleItemListCreateAsync(List<SaleItemDto> topupItemDto);

    Task<BatchRequestDto> BatchLotRequestCreateAsync(BatchRequestDto batchRequestDto);

    Task<MessagePagedResponseBase> BatchLotRequestGetListAsync(BatchListGetRequest request);

    Task<MessagePagedResponseBase> BatchLotRequestGetDetailAsync(BatchDetailGetRequest request);

    Task<BatchItemDto> BatchRequestSingleGetAsync(string batchCode);

    Task<MessagePagedResponseBase> BatchLotRequestStopAsync(Batch_StopRequest request);

    Task UpdateBatchRequestAsync(string batchCode, BatchLotRequestStatus status);

    Task<BatchDetailDto> BatchRequestGetDetailSingleAsync(string batchCode, string transRef);

    Task UpdateBatchDetailStatusAsync(string batchCode, string transRef, BatchLotRequestStatus status);

    Task UpdateBatchDetailStatusTowAsync(string batchCode, string transRef,
        BatchLotRequestStatus batchStatus, SaleRequestStatus status, string vender = "", string provider = "");

    Task PublishConsumerReport(SaleReponseDto reponse);

    Task<NewMessageReponseBase<BalanceResponse>> RefundTransaction(string transcode);
    Task<List<SaleRequestDto>> GetSaleRequestPending(int timePending = 0);

    Task<NewMessageReponseBase<string>> CardImportProvider(CardImportProviderRequest cardSaleRequest);
    Task CommissionRequest(SaleRequestDto request);

    Task TransactionCallBackCorrect(CallBackCorrectTransRequest request);

    Task<SaleOffsetRequestDto> SaleOffsetGetAsync(string transCode);

    Task<SaleOffsetRequestDto> SaleOffsetOriginGetAsync(string transCode, bool isProcess);

    Task<SaleOffsetRequestDto> SaleOffsetCreateAsync(SaleRequestDto saleRequestDto, string partnerCode);

    Task<SaleOffsetRequestDto> SaleOffsetUpdateAsync(SaleOffsetRequestDto saleOffsetDto);
    Task<SaleRequestDto> GetLastSaleRequest(string partnercode);

    Task<ResponseMesssageObject<CheckTransResult>> SaleRequestPartnerChecktransAsync(string transCode,
        string partnerCode,
        string secrectkey);

    Task<bool> CheckSaleItemAsync(string transCode);

    Task<SaleGateRequestDto> SaleGateCreateAsync(SaleGateRequestDto saleRequestDto);
    Task<SaleGateRequestDto> SaleGateRequestGetAsync(string transCode);

    Task<SaleGateRequestDto> SaleGateRequestUpdateAsync(SaleGateRequestDto saleRequestDto);

    Task<List<SaleGateRequestDto>> GetSaleGateRequestPending(int timePending = 0);
}