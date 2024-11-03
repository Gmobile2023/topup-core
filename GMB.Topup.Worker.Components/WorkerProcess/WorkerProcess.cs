using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GMB.Topup.Gw.Domain.Services;
using GMB.Topup.Worker.Components.Connectors;
using GMB.Topup.Worker.Components.TaskQueues;
using GMB.Topup.Shared;
using GMB.Topup.Shared.AbpConnector;
using GMB.Topup.Shared.CacheManager;
using GMB.Topup.Shared.ConfigDtos;
using GMB.Topup.Shared.Helpers;
using GMB.Topup.Shared.UniqueIdGenerator;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using GMB.Topup.Contracts.Commands.Commons;
using GMB.Topup.Contracts.Requests.Commons;
using GMB.Topup.Discovery.Requests.Tool;
using GMB.Topup.Discovery.Requests.TopupGateways;
using ServiceStack;
using ServiceStack.Caching;

namespace GMB.Topup.Worker.Components.WorkerProcess;

public partial class WorkerProcess : IWorkerProcess
{
    private readonly IBus _bus;
    private readonly CheckLimitTransaction _checkLimit;
    private readonly ExternalServiceConnector _externalServiceConnector;

    //private readonly IServiceGateway _gateway; gunner
    private readonly ILimitTransAccountService _limitTransAccountService;
    private readonly ILogger<WorkerProcess> _logger;
    private readonly ISaleService _saleService;
    private readonly TelcoConnector _telcoConnector;
    private readonly WorkerConfig _workerConfig;
    private readonly GrpcClientHepper _grpcClient;
    private readonly ISystemService _systemService;
    private ICacheClientAsync _cacheClient;
    private readonly ICommonService _commonService;

    public WorkerProcess(ILogger<WorkerProcess> logger, ExternalServiceConnector externalServiceConnector,
        ISaleService saleService,
        ILimitTransAccountService limitTransAccountService,
        CheckLimitTransaction checkLimit,
        IBus bus, IConfiguration configuration, TelcoConnector telcoConnector, IBackgroundTaskQueue queue,
        GrpcClientHepper grpcClient, ISystemService systemService, ICacheClient cacheClient,
        ICommonService commonService)
    {
        _logger = logger;
        _externalServiceConnector = externalServiceConnector;
        _saleService = saleService;
        _limitTransAccountService = limitTransAccountService;
        _checkLimit = checkLimit;
        _bus = bus;
        _telcoConnector = telcoConnector;
        _queue = queue;
        _grpcClient = grpcClient;
        _systemService = systemService;
        _commonService = commonService;
        _cacheClient = cacheClient.AsAsync();
        //configuration.GetSection("WorkerConfig").Bind(_workerConfig);
        _workerConfig = _commonService.GetWorkerConfigAsync().Result;
    }

    private async Task SendTeleMessage(SendTeleTrasactionRequest request)
    {
        try
        {
            await _bus.Publish<SendBotMessage>(new
            {
                MessageType = request.BotMessageType ?? BotMessageType.Wraning,
                BotType = request.BotType ?? BotType.Dev,
                Module = "Worker",
                request.Title,
                request.Message,
                TimeStamp = DateTime.Now,
                CorrelationId = Guid.NewGuid()
            });
        }
        catch (Exception ex)
        {
            _logger.LogInformation($"SendTeleMessage : {ex}");
        }
    }

    private bool IsSlowTrans(List<ServiceConfiguration> configurations)
    {
        var checkExít = configurations.FirstOrDefault(x => x.IsSlowTrans);
        if (checkExít == null)
            return false;
        var checkLast = configurations.LastOrDefault();
        if (checkLast == null)
            return false;
        return !checkLast.IsSlowTrans;
    }

    private async Task<NewMessageResponseBase<string>> ValidateTelco(string transCode, string phoneNumber,
        string vendor)
    {
        try
        {
            _logger.LogInformation($"ValidateTelco request: {transCode}-{phoneNumber}-{vendor}");
            if (!ValidationHelper.IsPhone(phoneNumber))
            {
                return new NewMessageResponseBase<string>
                {
                    ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_PhoneNotValid,
                        "Số điện thoại không hợp lệ")
                };
            }

            NewMessageResponseBase<string> check;
            if (_workerConfig.IsEnableCheckMobileSystem)
            {
                check = await _grpcClient.GetClientCluster(GrpcServiceName.MobileInfo, _workerConfig.TimeoutCheckMobile)
                    .SendAsync(
                        new MobileInfoRequest()
                        {
                            MsIsdn = phoneNumber,
                            TransCode = transCode,
                            ProviderCode = "VIETTEL2",
                            Telco = vendor
                        });
                var provider = !string.IsNullOrEmpty(check.Results) && check.Results.Contains("SYSTEM")
                    ? "SYSTEM"
                    : "NCC";
                _logger.LogInformation(
                    $"Provider {provider} ValidateTelcoSystem return: {check.ToJson()}-{transCode}-{phoneNumber}-{vendor}");
            }
            else
            {
                check = await _grpcClient
                    .GetClientCluster(GrpcServiceName.TopupGateway, _workerConfig.TimeoutCheckMobile).SendAsync(
                        new GateCheckMsIsdnRequest()
                        {
                            MsIsdn = phoneNumber,
                            TransCode = transCode,
                            ProviderCode = "VIETTEL2",
                            Telco = vendor
                        });
                //đổi về cùng mã lỗi
                _logger.LogInformation($"ValidateTelco return: {check.ToJson()}-{transCode}-{phoneNumber}-{vendor}");
                if (check.ResponseStatus.ErrorCode == ResponseCodeConst.Success)
                {
                    check.ResponseStatus.ErrorCode = "00"; // chỗ này đổi tạm để nó đồng nhất mã lỗi với NGate
                }
            }

            if (check.ResponseStatus.ErrorCode == "9999" || check.ResponseStatus.ErrorCode == "99")
                return new NewMessageResponseBase<string>
                {
                    ResponseStatus = new ResponseStatusApi("9999")
                };
            if (check.ResponseStatus.ErrorCode != "00" || string.IsNullOrEmpty(check.Results))
                return new NewMessageResponseBase<string>
                {
                    ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_PhoneNotValid,
                        "Số điện thoại không hợp lệ hoặc không thuộc nhà mạng yêu cầu")
                };
            if (!check.Results.Contains("TT") && !check.Results.Contains("TS"))
                return new NewMessageResponseBase<string>
                {
                    ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_PhoneNotValid,
                        "Số điện thoại không hợp lệ hoặc không thuộc nhà mạng yêu cầu")
                };

            var telco = check.Results.Split("|")[0];
            var telcoType = check.Results.Split("|")[1];
            if (string.IsNullOrEmpty(telco) || string.IsNullOrEmpty(telcoType))
                return new NewMessageResponseBase<string>
                {
                    ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_PhoneNotValid,
                        "Số điện thoại không hợp lệ hoặc không thuộc nhà mạng yêu cầu")
                };
            var convertTelco = TelcoHepper.ConvertTelco(telco);
            var checkTelco = string.Equals(convertTelco, vendor, StringComparison.CurrentCultureIgnoreCase);
            if (!checkTelco)
                return new NewMessageResponseBase<string>
                {
                    ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_NotValidStatus,
                        "Giao dịch không thành công. Vui lòng kiểm tra thông tin của thuê bao")
                };

            return new NewMessageResponseBase<string>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Success,
                    "Success"),
                Results = telcoType
            };
        }
        catch (Exception e)
        {
            _logger.LogError($"ValidateTelco error:{e}-{transCode}-{phoneNumber}-{vendor}");
            return new NewMessageResponseBase<string>
            {
                ResponseStatus = new ResponseStatusApi("9999", "Error")
            };
        }
    }
}