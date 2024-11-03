using System;
using System.Threading.Tasks;
using HLS.Paygate.Gw.Model.Dtos;
using HLS.Paygate.Gw.Model.Enums;
using HLS.Paygate.Shared;
using HLS.Paygate.Stock.Contracts.ApiRequests;
using HLS.Paygate.Stock.Contracts.Dtos;

namespace HLS.Paygate.Stock.Domains.BusinessServices
{
    public interface ICardRequestService
    {
        Task<CardRequestDto> CardRequestCreateAsync(CardRequestDto cardRequest);
        Task<CardRequestDto> CardRequestGetAsync(Guid id);
        Task<MessageResponseBase> CardRequestCheckAsync(string transCode, string providerCode);
        Task<bool> CardRequestUpdateAsync(CardRequestDto cardRequest);

        Task<bool> CardRequestUpdateStatusAsync(string transcode, CardRequestStatus status);
        Task<bool> CardRequestUpdateStatusAsync(Guid id, CardRequestStatus status);

        Task<CardDto> CardRequestForMappingAsync(
            int cardValue,
            string stockType, bool isCardTimeOut = false);
        

        Task<MessagePagedResponseBase> CardRequestGetListAsync(CardRequestGetList cardGetList);
        Task<bool> CardRequestCheckDuplicate(string serial, string cardCode);
    }
}