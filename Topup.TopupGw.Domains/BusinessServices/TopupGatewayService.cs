using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Topup.Shared;
using Topup.Shared.CacheManager;
using Topup.Shared.Helpers;
using Topup.TopupGw.Contacts.ApiRequests;
using Topup.TopupGw.Contacts.Dtos;
using Topup.TopupGw.Contacts.Enums;
using MassTransit;
using Microsoft.Extensions.Logging;
using Topup.Contracts.Commands.Commons;
using Topup.Contracts.Requests.Commons;
using ServiceStack;
using Topup.TopupGw.Domains.Entities;
using Topup.TopupGw.Domains.Repositories;

namespace Topup.TopupGw.Domains.BusinessServices;

public class TopupGatewayService : BusinessServiceBase, ITopupGatewayService
{
    private readonly IBusControl _bus;
    private readonly ICacheManager _cacheManager;
    private readonly ILogger<TopupGatewayService> _logger; // = LogManager.GetLogger("TopupGatewayService");
    private readonly ITransRepository _transRepository;
    private readonly IDateTimeHelper _dateTimeHelper;

    public TopupGatewayService(ITransRepository transRepository, ILogger<TopupGatewayService> logger,
        IBusControl bus, ICacheManager cacheManager, IDateTimeHelper dateTimeHelper)
    {
        _transRepository = transRepository;
        _logger = logger;
        _bus = bus;
        _cacheManager = cacheManager;
        _dateTimeHelper = dateTimeHelper;
    }

    public async Task<TopupRequestLogDto> TopupRequestLogCreateAsync(TopupRequestLogDto topupRequestLogDto)
    {
        var transRequest = topupRequestLogDto.ConvertTo<TopupRequestLog>();
        if (transRequest.Id != Guid.Empty) transRequest.Id = Guid.NewGuid();
        try
        {
            transRequest.RequestDate = DateTime.UtcNow;
            transRequest.AddedAtUtc = DateTime.UtcNow;
            await _transRepository.AddOneAsync(transRequest);
            return transRequest.ConvertTo<TopupRequestLogDto>();
        }
        catch (Exception e)
        {
            _logger.Log(LogLevel.Error, "Insert transRequest error: " + e.Message);
        }

        return null;
    }

    public async Task<bool> TopupRequestLogUpdateAsync(TopupRequestLogDto topupRequestLogDto)
    {
        var transRequest = topupRequestLogDto.ConvertTo<TopupRequestLog>();
        try
        {
            transRequest.ModifiedDate = DateTime.Now;
            try
            {
                var endDate = _dateTimeHelper.ConvertToUtcTime(DateTime.Now, _dateTimeHelper.CurrentTimeZone());
                var differenceInSeconds = endDate.Subtract(transRequest.RequestDate).TotalSeconds;
                transRequest.ProcessedTime = Math.Round(differenceInSeconds);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                transRequest.ProcessedTime = 0;
            }

            await _transRepository.UpdateOneAsync(transRequest);
            return true;
        }
        catch (Exception e)
        {
            _logger.Log(LogLevel.Error, "Update transRequest error: " + e.Message);
            return false;
        }
    }

    public async Task<ResponseCallBackReponse> TopupRequestLogUpdateStatusAsync(string transCode, string provider,
        int status, decimal transAmount = 0)
    {
        var response = new ResponseCallBackReponse
        {
            ResponseCode = 1,
            IsRefund = false
        };
        try
        {
            var log = await _transRepository.GetOneAsync<TopupRequestLog>(x =>
                x.TransCode == transCode && x.ProviderCode == provider);
            if (log != null)
            {
                if ((log.Status == TransRequestStatus.Timeout || log.Status == TransRequestStatus.Init) &&
                    (status == 1 || status == 3))
                {
                    if (status == 1)
                    {
                        log.Status = TransRequestStatus.Success;
                        log.AmountProvider = transAmount;
                    }
                    else
                    {
                        log.Status = TransRequestStatus.Fail;
                        response.IsRefund = true;
                    }

                    var key = $"PayGate_TopupRequest:Items:{string.Join("_", log.ProviderCode, log.TransCode)}";
                    log.ModifiedDate = DateTime.Now;
                    await _transRepository.UpdateOneAsync(log);
                    await _cacheManager.AddEntity(key, log, TimeSpan.FromMinutes(5));
                }

                response.ReceiverInfo = log.ReceiverInfo;
                response.RequestAmount = log.TransAmount;
                response.TransRef = log.TransRef;
                response.TransCode = log.TransCode;
                response.TopupGateTimeOut = log.TopupGateTimeOut ?? string.Empty;
                return response;
            }

            return null;
        }
        catch (Exception e)
        {
            response.ResponseCode = 2;
            _logger.Log(LogLevel.Error,
                $"{transCode}|{provider}|{status} RequestLogUpdateStatusAsync error: " + e.Message);
            return response;
        }
    }

    public async Task<PayBillRequestLogDto> PayBillRequestLogCreateAsync(PayBillRequestLogDto payBillRequestLog)
    {
        var transRequest = payBillRequestLog.ConvertTo<PayBillRequestLog>();
        if (transRequest.Id != Guid.Empty) transRequest.Id = Guid.NewGuid();
        try
        {
            transRequest.RequestDate = DateTime.UtcNow;
            transRequest.AddedAtUtc = DateTime.UtcNow;
            await _transRepository.AddOneAsync(transRequest);
            return transRequest.ConvertTo<PayBillRequestLogDto>();
        }
        catch (Exception e)
        {
            _logger.Log(LogLevel.Error, "Insert payBillRequest error: " + e.Message);
        }

        return null;
    }

    public async Task<bool> PayBillRequestLogUpdateAsync(PayBillRequestLogDto payBillRequestLogDto)
    {
        var transRequest = payBillRequestLogDto.ConvertTo<PayBillRequestLog>();
        try
        {
            transRequest.ModifiedDate = DateTime.Now;
            await _transRepository.UpdateOneAsync(transRequest);
            return true;
        }
        catch (Exception e)
        {
            _logger.Log(LogLevel.Error, "Update payBillRequest error: " + e.Message);
            return false;
        }
    }

    public async Task<CardRequestLogDto> CardRequestLogCreateAsync(CardRequestLogDto topupRequestLogDto)
    {
        var transRequest = topupRequestLogDto.ConvertTo<CardRequestLog>();
        if (transRequest.Id != Guid.Empty) transRequest.Id = Guid.NewGuid();
        var i = 0;
        var retry = false;
        do
        {
            try
            {
                retry = false;
                transRequest.RequestDate = DateTime.UtcNow;
                transRequest.AddedAtUtc = DateTime.UtcNow;
                await _transRepository.AddOneAsync(transRequest);
                return transRequest.ConvertTo<CardRequestLogDto>();
            }
            catch (Exception e)
            {
                _logger.Log(LogLevel.Error, "Insert cardRequest error: " + e.Message);

                if (e.Message.Contains("Server sent an invalid nonce."))
                {
                    i++;
                    retry = true;
                    _logger.LogInformation($"Cardrequest insert retry {i}");
                }
            }
        } while (retry && i <= 3);

        return null;
    }

    public async Task<bool> CardRequestLogUpdateAsync(CardRequestLogDto cardRequestLogDto)
    {
        var transRequest = cardRequestLogDto.ConvertTo<CardRequestLog>();
        try
        {
            transRequest.RequestDate = DateTime.Now;
            await _transRepository.UpdateOneAsync(transRequest);
            return true;
        }
        catch (Exception e)
        {
            _logger.Log(LogLevel.Error, "Update cardRequest error: " + e.Message);
            return false;
        }
    }

    public async Task<ProviderInfoDto> ProviderInfoCacheGetAsync(string providerCode)
    {
        try
        {
            var key = $"PayGate_ProviderInfo:Items:{providerCode}";
            var providerInfo = await _cacheManager.GetEntity<ProviderInfoDto>(key);
            if (providerInfo != null) return providerInfo;
            var item = await _transRepository.GetOneAsync<ProviderInfo>(p => p.ProviderCode == providerCode);
            if (item != null)
            {
                providerInfo = item?.ConvertTo<ProviderInfoDto>();
                await _cacheManager.AddEntity(key, providerInfo, TimeSpan.FromDays(300));
                return providerInfo;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                $"providerCode= {providerCode}|ProviderInfoCacheGetAsync error: {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
            return null;
        }
    }

    public async Task<ProviderInfoDto> ProviderInfoCreateAsync(ProviderInfoDto topupRequestLogDto)
    {
        var providerInfo = topupRequestLogDto.ConvertTo<ProviderInfo>();
        if (providerInfo.Id != Guid.Empty) providerInfo.Id = Guid.NewGuid();
        try
        {
            providerInfo.AddedAtUtc = DateTime.UtcNow;
            await _transRepository.AddOneAsync(providerInfo);
            return providerInfo.ConvertTo<ProviderInfoDto>();
        }
        catch (Exception e)
        {
            _logger.Log(LogLevel.Error, "Insert providerInfo error: " + e.Message);
        }

        return null;
    }

    public async Task<bool> ProviderInfoEditAsync(ProviderInfoDto request)
    {
        _logger.LogInformation("ProviderInfoEditAsync request:{providerInfo}", request.ToJson());
        var providerInfo =
            await _transRepository.GetOneAsync<ProviderInfo>(p => p.ProviderCode == request.ProviderCode);

        if (providerInfo == null)
            return false;
        try
        {
            if (!string.IsNullOrEmpty(request.Password) && providerInfo.Password != request.Password)
                providerInfo.Password = request.Password;

            if (!string.IsNullOrEmpty(request.Username) && providerInfo.Username != request.Username)
                providerInfo.Username = request.Username;

            if (!string.IsNullOrEmpty(request.ApiPassword) && providerInfo.ApiPassword != request.ApiPassword)
                providerInfo.ApiPassword = request.ApiPassword;

            if (!string.IsNullOrEmpty(request.ApiUrl) && providerInfo.ApiUrl != request.ApiUrl)
                providerInfo.ApiUrl = request.ApiUrl;

            if (!string.IsNullOrEmpty(request.ApiUser) && providerInfo.ApiUser != request.ApiUser)
                providerInfo.ApiUser = request.ApiUser;

            if (!string.IsNullOrEmpty(request.ExtraInfo) && providerInfo.ExtraInfo != request.ExtraInfo)
                providerInfo.ExtraInfo = request.ExtraInfo;

            if (!string.IsNullOrEmpty(request.PublicKey) && providerInfo.PublicKey != request.PublicKey)
                providerInfo.PublicKey = request.PublicKey;

            if (request.Timeout > 0 && request.Timeout != providerInfo.Timeout)
                providerInfo.Timeout = request.Timeout;

            if (request.TimeoutProvider != providerInfo.TimeoutProvider)
                providerInfo.TimeoutProvider = request.TimeoutProvider;

            if (request.TotalTransError != providerInfo.TotalTransError)
                providerInfo.TotalTransError = request.TotalTransError;

            if (request.TimeClose != providerInfo.TimeClose)
                providerInfo.TimeClose = request.TimeClose;

            if (request.IgnoreCode != providerInfo.IgnoreCode)
                providerInfo.IgnoreCode = request.IgnoreCode;

            if (request.IsAutoCloseFail != providerInfo.IsAutoCloseFail)
                providerInfo.IsAutoCloseFail = request.IsAutoCloseFail;

            if (!string.IsNullOrEmpty(request.PrivateKeyFile) && providerInfo.PrivateKeyFile != request.PrivateKeyFile)
                providerInfo.PrivateKeyFile = request.PrivateKeyFile;

            if (!string.IsNullOrEmpty(request.PublicKeyFile) && providerInfo.PublicKeyFile != request.PublicKeyFile)
                providerInfo.PublicKeyFile = request.PublicKeyFile;

            providerInfo.ParentProvider = request.ParentProvider;

            if (request.ProviderServices != null && request.ProviderServices.Any())
                providerInfo.ProviderServices = request.ProviderServices.ConvertTo<List<ProviderService>>();

            providerInfo.TimeScan = request.TimeScan;
            providerInfo.TotalTransDubious = request.TotalTransDubious;
            providerInfo.TotalTransScan = request.TotalTransScan;
            providerInfo.TotalTransErrorScan = request.TotalTransErrorScan;

            providerInfo.IsAlarm = request.IsAlarm;
            providerInfo.AlarmChannel = request.AlarmChannel;
            providerInfo.AlarmTeleChatId = request.AlarmTeleChatId;
            providerInfo.ErrorCodeNotAlarm = request.ErrorCodeNotAlarm;
            providerInfo.MessageNotAlarm = request.MessageNotAlarm;
            providerInfo.ProcessTimeAlarm = request.ProcessTimeAlarm;
            _logger.LogInformation("ProviderInfo return:{providerInfo}", providerInfo.ProviderCode);

            await _transRepository.UpdateOneAsync(providerInfo);
            var key = $"PayGate_ProviderInfo:Items:{providerInfo.ProviderCode}";
            await _cacheManager.ClearCache(key);
            return true;
        }
        catch (Exception e)
        {
            _logger.Log(LogLevel.Error, "Update providerInfo error: " + e.Message);
            return false;
        }
    }

    public async Task<bool> ProviderResponseCreateAsync(ProviderReponseDto request)
    {
        var provider = request.ConvertTo<ProviderResponse>();
        provider.AddedAtUtc = DateTime.UtcNow;
        try
        {
            await _transRepository.AddOneAsync(provider);
            return true;
        }
        catch (Exception e)
        {
            _logger.Log(LogLevel.Error, "ProviderReponseCreateAsync error: " + e.Message);
            return false;
        }
    }

    public async Task<bool> ProviderResponseUpdateAsync(ProviderReponseDto request)
    {
        var providerReponse =
            await _transRepository.GetOneAsync<ProviderResponse>(p =>
                p.Provider == request.Provider && p.Code == request.Code);
        if (providerReponse == null)
            return false;
        try
        {
            if (!string.IsNullOrEmpty(request.Name) && providerReponse.Name != request.Name)
                providerReponse.Name = request.Name;

            if (!string.IsNullOrEmpty(request.ResponseName) && providerReponse.ResponseName != request.ResponseName)
                providerReponse.ResponseName = request.ResponseName;

            if (!string.IsNullOrEmpty(request.ResponseCode) && providerReponse.ResponseCode != request.ResponseCode)
                providerReponse.ResponseCode = request.ResponseCode;
            await _transRepository.UpdateOneAsync(providerReponse);
            var key =
                $"PayGate:ResponseMessageItems:{string.Join("_", providerReponse.Provider, providerReponse.Code)}";
            await _cacheManager.ClearCache(key);
            return true;
        }
        catch (Exception e)
        {
            _logger.Log(LogLevel.Error, "ProviderResponseUpdateAsync error: " + e.Message);
            return false;
        }
    }

    public async Task<bool> ProviderResponseDeleteAsync(ProviderReponseDto request)
    {
        try
        {
            await _transRepository.DeleteOneAsync<ProviderResponse>(x =>
                x.Provider == request.Provider && x.Code == request.Code);
            var key =
                $"PayGate:ResponseMessageItems:{string.Join("_", request.Provider, request.Code)}";
            await _cacheManager.ClearCache(key);
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return false;
        }
    }

    public async Task<bool> ImportListProviderResponseAsync(ImportListProviderResponse request)
    {
        try
        {
            var listproviderResponse = request.ListProviderResponse.ConvertTo<List<ProviderResponse>>();
            await _transRepository.AddManyAsync<ProviderResponse>(listproviderResponse);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError("ImportListResponseMessageAsync error {ex} ", ex);
            return false;
        }
    }

    public async Task<MessagePagedResponseBase> GetListResponseMessageAsync(GetListProviderResponse request)
    {
        try
        {
            Expression<Func<ProviderResponse, bool>> query = p => true;
            if (!string.IsNullOrEmpty(request.ResponseName))
            {
                Expression<Func<ProviderResponse, bool>> newQuery = p =>
                    p.ResponseCode.ToLower().Contains(request.ResponseName.ToLower());
                query = query.And(newQuery);
            }

            if (!string.IsNullOrEmpty(request.ResponseCode))
            {
                Expression<Func<ProviderResponse, bool>> newQuery = p =>
                    p.ResponseCode == request.ResponseCode;
                query = query.And(newQuery);
            }

            if (!string.IsNullOrEmpty(request.Provider))
            {
                Expression<Func<ProviderResponse, bool>> newQuery = p =>
                    p.Provider.ToLower().Contains(request.Provider);
                query = query.And(newQuery);
            }

            if (!string.IsNullOrEmpty(request.Name))
            {
                Expression<Func<ProviderResponse, bool>> newQuery = p =>
                    p.Name.ToLower().Contains(request.Name.ToLower());
                query = query.And(newQuery);
            }

            if (!string.IsNullOrEmpty(request.Code))
            {
                Expression<Func<ProviderResponse, bool>> newQuery = p =>
                    p.Code == request.Code;
                query = query.And(newQuery);
            }

            var total = await _transRepository.CountAsync(query);

            var result = await _transRepository.GetSortedPaginatedAsync<ProviderResponse, Guid>(query,
                s => s.AddedAtUtc, false, request.SkipCount, request.MaxResultCount);

            return new MessagePagedResponseBase
            {
                ResponseCode = ResponseCodeConst.Success,
                ResponseMessage = "Thành công",
                Total = (int)total,
                Payload = result.OrderBy(x => x.Provider).ThenBy(x => x.Code).ThenBy(x => x.Name)
            };
        }
        catch (Exception e)
        {
            _logger.Log(LogLevel.Error, "GetListResponseMessageAsync error: " + e.Message);
            return null;
        }
    }

    [DebuggerStepThrough]
    public async Task<ProviderResponse> GetResponseMassageCacheAsync(string provider, string code, string transcode)
    {
        try
        {
            var key = $"PayGate:ResponseMessageItems:{string.Join("_", provider, code)}";
            var response = await _cacheManager.GetEntity<ProviderResponse>(key);
            if (response != null) return response;
            response = await _transRepository.GetOneAsync<ProviderResponse>(c =>
                c.Provider == provider && c.Code == code);
            if (response != null) await _cacheManager.AddEntity(key, response, TimeSpan.FromDays(300));
            return response;
        }
        catch (Exception e)
        {
            _logger.Log(LogLevel.Error, "GetReponseMasagerAsync error: " + e.Message);
            return null;
        }
    }

    public async Task<TopupRequestLog> GetTopupRequestLogAsync(string transRef, string provider = "")
    {
        if (string.IsNullOrEmpty(provider))
            return await _transRepository.GetOneAsync<TopupRequestLog>(x => x.TransRef == transRef);
        return await _transRepository.GetOneAsync<TopupRequestLog>(x =>
            x.TransRef == transRef && x.ProviderCode == provider);
    }

    public async Task<PayBillRequestLog> GetPayBillRequestLogAsync(string transRef, string provider = "")
    {
        if (string.IsNullOrEmpty(provider))
            return await _transRepository.GetOneAsync<PayBillRequestLog>(x => x.TransRef == transRef);
        return await _transRepository.GetOneAsync<PayBillRequestLog>(x =>
            x.TransRef == transRef && x.ProviderCode == provider);
    }

    public async Task<CardRequestLog> CardRequestLogAsync(string transRef)
    {
        return await _transRepository.GetOneAsync<CardRequestLog>(x => x.TransRef == transRef);
    }

    public async Task<TopupGwLog> GetTopupGateTransCode(string transRef, string serviceCode)
    {
        var logRequest = new TopupGwLog();
        if (serviceCode == ServiceCodes.TOPUP || serviceCode == ServiceCodes.TOPUP_DATA)
        {
            var log = await GetTopupRequestLogAsync(transRef);
            if (log == null)
                return null;
            logRequest.TransCode = log.TransCode;
            logRequest.TransRef = log.TransRef;
            logRequest.ProviderCode = log.ProviderCode;
            logRequest.ProductCode = log.ProductCode;
            logRequest.TransDate = log.RequestDate;
            logRequest.TransIndex = log.TransIndex;
        }

        if (serviceCode == ServiceCodes.PAY_BILL)
        {
            var log = await GetPayBillRequestLogAsync(transRef);
            if (log == null)
                return null;
            logRequest.TransCode = log.TransCode;
            logRequest.TransRef = log.TransRef;
            logRequest.ProviderCode = log.ProviderCode;
            logRequest.ProductCode = log.ProductCode;
            logRequest.TransDate = log.RequestDate;
            logRequest.TransIndex = log.TransIndex;
        }

        if (serviceCode.StartsWith("PIN"))
        {
            var log = await CardRequestLogAsync(transRef);
            if (log == null)
                return null;
            logRequest.TransCode = log.TransCode;
            logRequest.TransRef = log.TransRef;
            logRequest.ProviderCode = log.ProviderCode;
            logRequest.ProductCode = log.ProductCode;
            logRequest.TransDate = log.RequestDate;
            logRequest.TransIndex = log.TransIndex;
        }

        return logRequest;
    }

    public bool ValidConnector(string currentProvider, string providerCheck)
    {
        providerCheck = providerCheck.Split('-')[0];
        switch (currentProvider)
        {
            case ProviderConst.VTT when providerCheck == ProviderConst.VTT:
            case ProviderConst.VTT2 when providerCheck == ProviderConst.VTT2:
            case ProviderConst.ZOTA when providerCheck == ProviderConst.ZOTA:
            case ProviderConst.VTT_TEST when providerCheck == ProviderConst.VTT_TEST:
            case ProviderConst.ZOTA_TEST when providerCheck == ProviderConst.ZOTA_TEST:
            case ProviderConst.OCTA when providerCheck == ProviderConst.OCTA:
            case ProviderConst.OCTA_TEST when providerCheck == ProviderConst.OCTA_TEST:
            case ProviderConst.IOMEDIA when providerCheck == ProviderConst.IOMEDIA:
            case ProviderConst.IOMEDIA_TEST when providerCheck == ProviderConst.IOMEDIA_TEST:
            case ProviderConst.FAKE when providerCheck == ProviderConst.FAKE:
            case ProviderConst.IMEDIA when providerCheck == ProviderConst.IMEDIA:
            case ProviderConst.IMEDIA_TEST when providerCheck == ProviderConst.IMEDIA_TEST:
            case ProviderConst.CARD when providerCheck == ProviderConst.CARD:
            case ProviderConst.MTC when providerCheck == ProviderConst.MTC:
            case ProviderConst.NHATTRAN when providerCheck == ProviderConst.NHATTRAN:
            case ProviderConst.APPOTA when providerCheck == ProviderConst.APPOTA:
            case ProviderConst.VIMO when providerCheck == ProviderConst.VIMO:
            case ProviderConst.CG2022 when providerCheck == ProviderConst.CG2022:
            case ProviderConst.SHT when providerCheck == ProviderConst.SHT:
            case ProviderConst.WPAY when providerCheck == ProviderConst.WPAY:
            case ProviderConst.PAYOO when providerCheck == ProviderConst.PAYOO:
            case ProviderConst.MOBIFONE when providerCheck == ProviderConst.MOBIFONE:
            case ProviderConst.IMEDIA2 when providerCheck == ProviderConst.IMEDIA2:
            case ProviderConst.PAYTECH when providerCheck == ProviderConst.PAYTECH:
            case ProviderConst.ESALE when providerCheck == ProviderConst.ESALE:
            case ProviderConst.HLS when providerCheck == ProviderConst.HLS:
            case ProviderConst.VDS when providerCheck == ProviderConst.VDS:
            case ProviderConst.PAYPOO when providerCheck == ProviderConst.PAYPOO:
            case ProviderConst.VMG when providerCheck == ProviderConst.VMG:
            case ProviderConst.VMG2 when providerCheck == ProviderConst.VMG2:
            case ProviderConst.WHYPAY when providerCheck == ProviderConst.WHYPAY:
            case ProviderConst.IRIS when providerCheck == ProviderConst.IRIS:
            // case ProviderConst.IRIS_PINCODE when providerCheck == ProviderConst.IRIS_PINCODE:
            case ProviderConst.ADVANCE when providerCheck == ProviderConst.ADVANCE:
            case ProviderConst.VTC365 when providerCheck == ProviderConst.VTC365:
            case ProviderConst.GATE when providerCheck == ProviderConst.GATE:
            case ProviderConst.SHOPEEPAY when providerCheck == ProviderConst.SHOPEEPAY:
            case ProviderConst.VINNET when providerCheck == ProviderConst.VINNET:
            case ProviderConst.FINVIET when providerCheck == ProviderConst.FINVIET:
            case ProviderConst.VNPTPAY when providerCheck == ProviderConst.VNPTPAY:
            case ProviderConst.VINATTI when providerCheck == ProviderConst.VINATTI:
                return true;
            default:
                return false;
        }
    }


    public async Task<bool> ProviderSalePriceInfoCreateAsync(List<ProviderSalePriceDto> dtos)
    {
        var list = dtos.ConvertTo<List<ProviderSalePrice>>();
        try
        {
            foreach (var item in list)
            {
                var checkDto = await _transRepository.GetOneAsync<ProviderSalePrice>(c =>
                    c.ProviderCode == item.ProviderCode
                    && c.ProviderType == item.ProviderType && c.CardValue == item.CardValue &&
                    c.TopupType == item.TopupType);
                if (checkDto != null)
                {
                    checkDto.CardPrice = item.CardPrice;
                    await _transRepository.UpdateOneAsync(item);
                }
                else
                {
                    if (item.Id != Guid.Empty) item.Id = Guid.NewGuid();
                    item.AddedAtUtc = DateTime.UtcNow;
                    await _transRepository.AddOneAsync(item);
                }
            }

            return true;
        }
        catch (Exception e)
        {
            _logger.Log(LogLevel.Error, "Insert providerSalePriceInfo error: " + e.Message);
            return false;
        }
    }

    public async Task<List<ProviderSalePriceDto>> ProviderSalePriceGetAsync(string providerCode, string providerType,
        string topupType)
    {
        try
        {
            // var key = $"PayGate_ProviderInfo:Items:{providerCode}";
            // var providerInfo = await _cacheManager.GetCacheObject<ProviderInfoDto>(key);
            //if (providerInfo != null) return providerInfo;
            var item = await _transRepository.GetAllAsync<ProviderSalePrice>(p =>
                p.ProviderCode == providerCode && p.ProviderType == providerType && p.TopupType == topupType);
            if (item != null) return item?.ConvertTo<List<ProviderSalePriceDto>>();
            //await _cacheManager.SetCacheObject(key, providerInfo, TimeSpan.FromDays(300));
            //return providerInfo;
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<ProviderSalePriceDto> ProviderSalePriceGetAsync(string providerCode, string providerType,
        string topupType, decimal value)
    {
        try
        {
            var item = await _transRepository.GetOneAsync<ProviderSalePrice>(p => p.ProviderCode == providerCode
                && p.ProviderType == providerType && p.TopupType == topupType && p.CardValue == value);
            if (item != null) return item?.ConvertTo<ProviderSalePriceDto>();

            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public int[] ConvertArrayCode(string extraInfo)
    {
        if (string.IsNullOrEmpty(extraInfo))

            return new int[0];
        var data = (from x in extraInfo.Split(',', ';', '|').ToList()
            select Convert.ToInt32(x)).ToArray();
        return data;
    }

    public async Task SendTelegram(MessageResponseBase result,
        SendWarningDto logRequest)
    {
        if (logRequest.ProviderCode != ProviderConst.CARD)
            try
            {
                if (logRequest.Status == 2)
                    switch (logRequest.Type)
                    {
                        case SendWarningDto.Topup:
                        case SendWarningDto.VBILL:
                            await _bus.Publish<SendBotMessage>(new
                            {
                                MessageType = BotMessageType.Wraning,
                                BotType = BotType.Sale,
                                Module = "TopupGate",
                                Title = "Cảnh báo GD chưa có kết quả",
                                Message =
                                    $"Mã NCC: {logRequest.TransCode}\n" +
                                    $"NCC: {logRequest.ProviderCode}\n" +
                                    $"Mã GD: {logRequest.TransRef}\n" +
                                    $"Mã đối tác: {logRequest.ReferenceCode}\n" +
                                    $"TG: {DateTime.Now:dd/MM/yyyy HH:mm:ss}\n" +
                                    $"Đại lý: {logRequest.PartnerCode}\n" +
                                    $"Sản phẩm {logRequest.ProductCode}\n" +
                                    $"Tài khoản thụ hưởng: {logRequest.ReceiverInfo}\n" +
                                    $"Số tiền: {logRequest.TransAmount.ToFormat("đ")}",
                                TimeStamp = DateTime.Now,
                                CorrelationId = Guid.NewGuid()
                            });
                            break;
                        case SendWarningDto.PinCode:
                            await _bus.Publish<SendBotMessage>(new
                            {
                                MessageType = BotMessageType.Wraning,
                                BotType = BotType.Sale,
                                Module = "TopupGate",
                                Title = "Cảnh báo GD chưa có kết quả",
                                Message =
                                    $"Mã NCC: {result.TransCodeProvider}\n" +
                                    $"NCC: {logRequest.ProviderCode}\n" +
                                    $"Mã GD: {logRequest.TransRef}\n" +
                                    $"Mã đối tác: {logRequest.ReferenceCode}\n" +
                                    $"TG: {DateTime.Now:dd/MM/yyyy HH:mm:ss}\n" +
                                    $"Đại lý: {logRequest.PartnerCode}\n" +
                                    $"Sản phẩm {logRequest.ProductCode}\n" +
                                    $"Số tiền: {logRequest.TransAmount.ToFormat("đ")}"
                                //TimeStamp = DateTime.Now,
                                //CorrelationId = Guid.NewGuid()
                            });
                            break;
                    }
            }
            catch (Exception e)
            {
                _logger.LogInformation($"SendTelegram : {e}");
            }

        if (!string.IsNullOrEmpty(logRequest.SendProviderFailed))
        {
            if (logRequest.SendProviderFailed == ProviderConst.CARD)
                try
                {
                    await _bus.Publish<SendBotMessage>(new
                    {
                        MessageType = BotMessageType.Wraning,
                        BotType = BotType.CardMapping,
                        Module = "TopupGate",
                        Title = logRequest.ReceiverInfo,
                        Message = logRequest.Content,
                        TimeStamp = DateTime.Now,
                        CorrelationId = Guid.NewGuid()
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError($"SendTelegram : {ex}");
                }

            else if (logRequest.Status == 1 && logRequest.SendProviderFailed != ProviderConst.CARD)
                try
                {
                    await _bus.Publish<SendBotMessage>(new
                    {
                        MessageType = BotMessageType.Error,
                        BotType = BotType.Dev,
                        Module = "TopupGate",
                        Title = $"Cảnh báo giao dịch của kênh {logRequest.ProviderCode} đang đang bi sự cố",
                        Message =
                            $"GD: {logRequest.TransCode}\n" +
                            $"SĐT: {logRequest.ReceiverInfo}\n" +
                            $"Số tiền {logRequest.TransAmount.ToFormat("đ")}\n" +
                            $"Nội dung báo : {logRequest.Content}",
                        TimeStamp = DateTime.Now,
                        CorrelationId = Guid.NewGuid()
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogInformation($"SendTelegram : {ex}");
                }
        }
    }
}