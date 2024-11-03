using System.Collections.Generic;
using System.Threading.Tasks;
using GMB.Topup.Shared;
using GMB.Topup.TopupGw.Contacts.ApiRequests;
using GMB.Topup.TopupGw.Contacts.Dtos;
using GMB.Topup.TopupGw.Domains.Entities;

namespace GMB.Topup.TopupGw.Domains.BusinessServices;

public interface ITopupGatewayService
{
    Task<TopupRequestLogDto> TopupRequestLogCreateAsync(TopupRequestLogDto topupRequestLogDto);
    Task<bool> TopupRequestLogUpdateAsync(TopupRequestLogDto topupRequestLogDto);
    Task<PayBillRequestLogDto> PayBillRequestLogCreateAsync(PayBillRequestLogDto payBillRequestLog);
    Task<bool> PayBillRequestLogUpdateAsync(PayBillRequestLogDto payBillRequestLogDto);
    Task<CardRequestLogDto> CardRequestLogCreateAsync(CardRequestLogDto topupRequestLogDto);
    Task<bool> CardRequestLogUpdateAsync(CardRequestLogDto cardRequestLogDto);
    Task<ProviderInfoDto> ProviderInfoCacheGetAsync(string providerCode);
    Task<ProviderInfoDto> ProviderInfoCreateAsync(ProviderInfoDto topupRequestLogDto);
    Task<bool> ProviderInfoEditAsync(ProviderInfoDto cardRequestLogDto);

    Task<bool> ProviderResponseCreateAsync(ProviderReponseDto request);
    Task<ProviderResponse> GetResponseMassageCacheAsync(string provider, string code, string transcode);
    Task<TopupRequestLog> GetTopupRequestLogAsync(string transRef, string provider = "");
    Task<PayBillRequestLog> GetPayBillRequestLogAsync(string transRef, string provider = "");
    Task<CardRequestLog> CardRequestLogAsync(string transRef);
    Task<TopupGwLog> GetTopupGateTransCode(string transRef, string serviceCode);

    Task<ResponseCallBackReponse> TopupRequestLogUpdateStatusAsync(string transCode, string provider, int status, decimal transAmount = 0);

    // Task SendTelegram(MessageResponseBase result,
    //     SendWarningDto logRequest);

    bool ValidConnector(string currentProvider, string providerCheck);

    Task<bool> ProviderResponseUpdateAsync(ProviderReponseDto request);
    Task<bool> ProviderResponseDeleteAsync(ProviderReponseDto request);
    Task<bool> ImportListProviderResponseAsync(ImportListProviderResponse request);
    Task<MessagePagedResponseBase> GetListResponseMessageAsync(
        GetListProviderResponse request);

    Task<bool> ProviderSalePriceInfoCreateAsync(List<ProviderSalePriceDto> dtos);

    Task<List<ProviderSalePriceDto>> ProviderSalePriceGetAsync(string providerCode, string providerType,
        string topupType);

    Task<ProviderSalePriceDto> ProviderSalePriceGetAsync(string providerCode, string providerType, string topupType,
        decimal value);

    int[] ConvertArrayCode(string extraInfo);
}