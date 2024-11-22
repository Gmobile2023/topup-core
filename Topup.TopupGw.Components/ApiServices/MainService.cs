using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Topup.Shared;
using Topup.Shared.Dtos;
using Topup.Shared.Utils;


using Topup.TopupGw.Domains.BusinessServices;
using MassTransit;
using Microsoft.Extensions.Logging;
using Topup.Discovery.Requests.TopupGateways;
using Topup.TopupGw.Contacts.ApiRequests;
using Topup.TopupGw.Contacts.Dtos;
using ServiceStack;
using Topup.TopupGw.Components.TopupGwProcess;
using static Topup.TopupGw.Components.Connectors.IRIS.IrisConnector;

namespace Topup.TopupGw.Components.ApiServices;

public partial class MainService : Service
{
    private readonly IBusControl _bus;

    // private readonly GatewayConnectorFactory _connectorFactory;
    private readonly ILogger<MainService> _logger; // = LogManager.GetLogger("MainService");
    private readonly ITopupGwProcess _topupGatewayProcess;
    private readonly ITopupGatewayService _topupGatewayService;

    public MainService(ITopupGatewayService topupGatewayService,
        ILogger<MainService> logger, IBusControl bus, ITopupGwProcess topupGatewayProcess)
    {
        _topupGatewayService = topupGatewayService;
        // _connectorFactory = connectorFactory;
        _logger = logger;
        _bus = bus;
        _topupGatewayProcess = topupGatewayProcess;
    }

    public async Task<object> PostAsync(ProviderInfoRequest providerInfoRequest)
    {
        if (string.IsNullOrEmpty(providerInfoRequest.ProviderCode))
            throw new HttpError(HttpStatusCode.BadRequest);
        var providerInfo = providerInfoRequest.ConvertTo<ProviderInfoDto>();
        providerInfo = await _topupGatewayService.ProviderInfoCreateAsync(providerInfo);

        if (providerInfo != null)
            return new NewMessageResponseBase<ProviderInfoDto>
            {
                Results = providerInfo,
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Success, "Success")
            };

        return new NewMessageResponseBase<ProviderInfoDto>
        {
            ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error, "Không thành công")
        };
    }

    public async Task<object> PatchAsync(ProviderInfoUpdateRequest providerInfoRequest)
    {
        if (string.IsNullOrEmpty(providerInfoRequest.ProviderCode))
            throw new HttpError(HttpStatusCode.Forbidden);

        var providerInfo = providerInfoRequest.ConvertTo<ProviderInfoDto>();
        var result = await _topupGatewayService.ProviderInfoEditAsync(providerInfo);

        if (result)
            return new NewMessageResponseBase<ProviderInfoDto>
            {
                Results = providerInfo,
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Success, "Success")
            };

        return new NewMessageResponseBase<ProviderInfoDto>
        {
            ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error, "Không thành công")
        };
    }

    public async Task<object> GetAsync(ProviderInfoGet providerInfoRequest)
    {
        if (string.IsNullOrEmpty(providerInfoRequest.ProviderCode))
            throw new HttpError(HttpStatusCode.Forbidden);

        var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(providerInfoRequest.ProviderCode);

        if (providerInfo != null)
            return new NewMessageResponseBase<ProviderInfoDto>
            {
                Results = providerInfo,
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Success, "Success")
            };

        return new NewMessageResponseBase<ProviderInfoDto>
        {
            ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error, "NCC không tồn tại")
        };
    }

    public async Task<object> GetAsync(GateProviderInfoRequest request)
    {
        var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(request.ProviderCode);

        if (providerInfo != null)
            return new NewMessageResponseBase<ProviderTopupInfoDto>
            {
                Results = providerInfo.ConvertTo<ProviderTopupInfoDto>(),
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Success, "Success")
            };

        return new NewMessageResponseBase<ProviderTopupInfoDto>
        {
            ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error, "Không tồn tại")
        };
    }

    public async Task<object> PostAsync(CreateProviderReponse providerRequest)
    {
        if (string.IsNullOrEmpty(providerRequest.Provider))
            throw new HttpError(HttpStatusCode.Forbidden);
        var request = providerRequest.ConvertTo<ProviderReponseDto>();
        var rs = await _topupGatewayService.ProviderResponseCreateAsync(request);
        if (rs)
            return new NewMessageResponseBase<string>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Success, "Thành công")
            };
        return new NewMessageResponseBase<string>
        {
            ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error, "Không thành công")
        };
    }

    public async Task<object> PutAsync(UpdateProviderReponse providerRequest)
    {
        if (string.IsNullOrEmpty(providerRequest.Provider))
            throw new HttpError(HttpStatusCode.Forbidden);
        var request = providerRequest.ConvertTo<ProviderReponseDto>();
        var rs = await _topupGatewayService.ProviderResponseUpdateAsync(request);
        if (rs)
            return new NewMessageResponseBase<string>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Success, "Thành công")
            };
        return new NewMessageResponseBase<string>
        {
            ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error, "Không thành công")
        };
    }

    public async Task<object> DeleteAsync(DeleteProviderReponse providerRequest)
    {
        if (string.IsNullOrEmpty(providerRequest.Provider))
            throw new HttpError(HttpStatusCode.Forbidden);
        var request = providerRequest.ConvertTo<ProviderReponseDto>();
        var rs = await _topupGatewayService.ProviderResponseDeleteAsync(request);
        if (rs)
            return new NewMessageResponseBase<string>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Success, "Thành công")
            };
        return new NewMessageResponseBase<string>
        {
            ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error, "Không thành công")
        };
    }

    public async Task<MessagePagedResponseBase> GetAsync(GetListProviderResponse providerResponse)
    {
        return await _topupGatewayService.GetListResponseMessageAsync(providerResponse);
    }

    public async Task<NewMessageResponseBase<object>> PostAsync(ImportListProviderResponse request)
    {
        var rs = await _topupGatewayService.ImportListProviderResponseAsync(request);
        if (rs)
            return new NewMessageResponseBase<object>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Success, "Thành công")
            };
        return new NewMessageResponseBase<object>
        {
            ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error, "Không thành công")
        };
    }


    public async Task<object> GetAsync(GetProviderReponse quest)
    {
        if (string.IsNullOrEmpty(quest.Provider))
            throw new HttpError(HttpStatusCode.Forbidden);
        var msg = await _topupGatewayService.GetResponseMassageCacheAsync(quest.Provider, quest.Code, null);
        return new NewMessageResponseBase<ProviderReponseDto>
        {
            ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Success, "Thành công"),
            Results = msg.ConvertTo<ProviderReponseDto>()
        };
    }

    //Gunner=> Hàm này xem lại phải để ra Gateway để callback, check giao dịch gốc

    public async Task<object> PostAsync(CallBackRequest request)
    {
        _logger.LogInformation("CallBackRequest {Request}", request.ToJson());
        var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(ProviderConst.CARD);

        if (providerInfo == null)
        {
            _logger.LogInformation("providerInfo is null");
            return new ResponseCallBack
            {
                TransCode = request.TransCode,
                ResponseCode = 2
            };
        }

        var signature = (request.TransCode + providerInfo.Username + providerInfo.Password).EncryptMd5()
            .ToUpper();
        if (request.Signature.ToUpper() != signature)
            return new ResponseCallBack
            {
                TransCode = request.TransCode,
                ResponseCode = 2
            };

        await _bus.Publish(new TopupCallBackMessage
        {
            Amount = request.TotalTopupAmount,
            ProviderCode = providerInfo.ProviderCode,
            Status = request.ResponseCode,
            TransCode = request.TransCode,
            CorrelationId = Guid.NewGuid(),
            Timestamp = DateTime.Now
        });

        return new ResponseCallBack
        {
            TransCode = request.TransCode,
            ResponseCode = 1
        };
    }

    public async Task<object> GetAsync(CallBackZotaRequest request)
    {
        _logger.LogInformation("CallBackZotaRequest {Request}", request.ToJson());
        var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(ProviderConst.ZOTA);

        if (providerInfo == null)
        {
            _logger.LogInformation("providerInfo is null");
            return new ResponseCallBack
            {
                TransCode = request.request_id,
                ResponseCode = 2
            };
        }

        await _bus.Publish(new TopupCallBackMessage
        {
            Amount = request.txn_amount,
            ProviderCode = providerInfo.ProviderCode,
            Status = request.txn_status.ToUpper() == "CLOSED" ? 1 : request.txn_status.ToUpper() == "FAILED" ? 2 : 0,
            TransCode = request.request_id,
            CorrelationId = Guid.NewGuid(),
            Timestamp = DateTime.Now
        });

        return new ResponseCallBack
        {
            TransCode = request.request_id,
            ResponseCode = 1
        };
    }

    public async Task<object> PostAsync(CallBackCG2022Request request)
    {
        _logger.LogInformation("CallBackCG2022Request {Request}", request.ToJson());
        await _bus.Publish(new TopupCallBackMessage
        {
            Amount = request.ActualAmount,
            ProviderCode = ProviderConst.CG2022,
            Status = request.ResponseCode switch
            {
                ResponseCodeConst.Error => 1,
                ResponseCodeConst.Success => 2,
                _ => 0
            },
            TransCode = request.TxnId,
            CorrelationId = Guid.NewGuid(),
            Timestamp = DateTime.Now
        });

        return new ResponseCallBack
        {
            TransCode = request.TxnId,
            ResponseCode = 1
        };
    }

    public async Task<object> PostAsync(CallBackCG2022V2Request request)
    {
        _logger.LogInformation("CallBackCG2022V2Request {Request}", request.ToJson());
        await _bus.Publish(new TopupCallBackMessage
        {
            Amount = request.ActualAmount,
            ProviderCode = request.ProviderCode,
            Status = request.ResponseCode switch
            {
                ResponseCodeConst.Error => 1,
                ResponseCodeConst.Success => 2,
                _ => 0
            },
            TransCode = request.TxnId,
            CorrelationId = Guid.NewGuid(),
            Timestamp = DateTime.Now
        });

        return new ResponseCallBack
        {
            TransCode = request.TxnId,
            ResponseCode = 1
        };
    }

    /// <summary>
    /// CallBack cảu kênh : GATE
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public async Task<object> PostAsync(CallBackCardGateRequest request)
    {
        _logger.LogInformation("CallBackCardGateRequest {Request}", request.ToJson());
        var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(ProviderConst.GATE);
        if (providerInfo == null)
        {
            _logger.LogInformation("providerInfo is null");
            return new ResponseCallBackCardGate
            {
                Status = "1",
                Message = "Thành công"
            }.ToJson();
        }

        int status = Convert.ToInt32(request.status);
        if (status == 1 || status == 0)
        {
            return new ResponseCallBackCardGate
            {
                Status = "1",
                Message = "Thành công"
            }.ToJson();
        }

        await _bus.Publish(new TopupCallBackMessage
        {
            Amount = status == 1 ? request.amount : request.amoutran,
            ProviderCode = providerInfo.ProviderCode,
            Status = new[] { 1, 2 }.Contains(Convert.ToInt32(request.status)) ? 1 : 2,
            TransCode = request.refcode,
            CorrelationId = Guid.NewGuid(),
            Timestamp = DateTime.Now
        });

        return new ResponseCallBackCardGate
        {
            Status = "1",
            Message = "Thành công"
        }.ToJson();
    }

    /// <summary>
    /// CallBack của kênh : Advance
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public async Task<object> PostAsync(CallBackAdvanceRequest request)
    {
        _logger.LogInformation("CallBackAdvanceRequest {Request}", request.ToJson());
        var reponseData = new ResponseCallBackAdvance
        {
            Status = 0,
            RequestId = request.request_id,
            TransId = request.trans_id,
            Message = "Thất bại"
        };

        if (!new[] { 0, 1 }.Contains(request.status))
            return reponseData;

        var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(ProviderConst.ADVANCE);
        if (providerInfo == null)
        {
            _logger.LogInformation($"{ProviderConst.ADVANCE} providerInfo is null");
            return reponseData;
        }

        string plainText =
            $"{request.trans_id}{request.amount}{request.phone}{request.status}{providerInfo.ApiPassword.Md5()}";
        string signature = plainText.Md5();

        if (request.sign.ToUpper() != signature.ToUpper())
            return reponseData;

        await _bus.Publish(new TopupCallBackMessage
        {
            Amount = 0,
            ProviderCode = providerInfo.ProviderCode,
            Status = request.status == 1 ? 1 : 2,
            TransCode = request.request_id,
            CorrelationId = Guid.NewGuid(),
            Timestamp = DateTime.Now
        });

        reponseData.Status = 1;
        reponseData.Message = "Thành công";
        return reponseData;
    }

    public async Task<object> GetAsync(PingRouteRequest request)
    {
        return await Task.FromResult("OK");
    }

    public async Task<object> GetAsync(ProviderSalePriceInfoGet request)
    {
        if (string.IsNullOrEmpty(request.ProviderCode))
            throw new HttpError(HttpStatusCode.Forbidden);

        var providerInfo =
            await _topupGatewayService.ProviderSalePriceGetAsync(request.ProviderCode, request.ProviderType,
                request.TopupType);

        if (providerInfo != null)
            return new NewMessageResponseBase<List<ProviderSalePriceDto>>
            {
                Results = providerInfo,
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Success, "Success")
            };

        return new NewMessageResponseBase<ProviderInfoDto>
        {
            ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error, "NCC không tồn tại")
        };
    }
}