using System.Threading.Tasks;
using Topup.Common.Domain.Services;
using Topup.Common.Model.Dtos;
using Topup.Common.Model.Dtos.RequestDto;
using MassTransit;
using Microsoft.Extensions.Logging;
using Topup.Contracts.Requests.Commons;
using Topup.Shared;
using ServiceStack;
using Topup.Discovery.Requests.Commons;

namespace Topup.Common.Interface.Services;

public partial class CommonService : Service
{
    private readonly IAuditLogService _auditLog;
    private readonly IBotMessageService _bot;
    private readonly IBusControl _bus;
    private readonly ICmsService _cmsService;
    private readonly ICommonAppService _commonService;
    private readonly ILogger<CommonService> _logger;
    private readonly INotificationSevice _notification;


    public CommonService(
        ILogger<CommonService> logger,
        IBotMessageService bot,
        INotificationSevice notification,
        IBusControl bus, IAuditLogService auditLog, ICommonAppService commonService, ICmsService cmsService)
    {
        _logger = logger;
        _bot = bot;
        _notification = notification;
        _bus = bus;
        _auditLog = auditLog;
        _commonService = commonService;
        _cmsService = cmsService;
    }

    public async Task<object> GetAsync(GetSavePayBillRequest request)
    {
        _logger.LogInformation($"GetSavePayBillRequest:{request.ToJson()}");
        return await _commonService.GetSavePayBill(request);
    }

    public async Task<object> GetAsync(GetTotalWaitingBillRequest request)
    {
        _logger.LogInformation($"GetTotalWaitingBillRequest:{request.ToJson()}");
        return await _commonService.GetTotalWaitingBill(request);
    }

    public async Task<object> DeleteAsync(RemoveSavePayBillRequest request)
    {
        _logger.LogInformation($"RemoveSavePayBillRequest:{request.ToJson()}");
        var rs = await _commonService.RemoveSavePayBill(request);
        _logger.LogInformation($"RemoveSavePayBillRequest return:{rs}");
        return new ResponseMessageApi<bool>
        {
            Success = rs
        };
    }

    public async Task<object> AnyAsync(CommonSendMessageTeleRequest request)
    {
        return await _bot.SendAlarmMessage(request.ConvertTo<SendAlarmMessageInput>());
    }

    public async Task<object> AnyAsync(CommonSendMessageTeleToGroupRequest request)
    {
        var input = request.ConvertTo<SendAlarmMessageInput>();
        if (!string.IsNullOrEmpty(request.ChatId))
        {
            input.ChatId = int.Parse(request.ChatId);
        }

        return await _bot.SendAlarmMessage(input);
    }

    public async Task<object> GetAsync(PingRouteRequest request)
    {
        return await Task.FromResult("OK");
    }

    public async Task<object> PostAsync(HealthCheckNotifiRequest request)
    {
        await _bot.SendAlarmMessage(new SendAlarmMessageInput
        {
            Message = request.message,
            Module = "Common",
            MessageType = request.message.ToLower().Contains("is back to life")
                ? BotMessageType.Message
                : BotMessageType.Error,
            BotType = BotType.Dev,
            Title = "HealthCheck Alarm"
        });
        return await Task.FromResult("OK");
    }

    public async Task<object> GetAsync(GetAlarmBalanceRequest request)
    {
        _logger.LogInformation($"GetAlarmBalanceRequest:{request.ToJson()}");
        var item = await _commonService.AlarmBalanceGetAsync(request.AccountCode, request.CurrencyCode);
        return item;
    }

    public async Task<object> GetAsync(GetAllAlarmBalanceRequest request)
    {
        _logger.LogInformation($"GetAllAlarmBalanceRequest:{request.ToJson()}");
        var item = await _commonService.GetListAlarmBalanceGetAsync(request);
        return item;
    }

    public async Task<object> PostAsync(AddAlarmBalanceRequest request)
    {
        _logger.LogInformation($"AddAlarmBalanceRequest:{request.ToJson()}");
        var item = await _commonService.AlarmBalanceGetAsync(request.AccountCode, request.CurrencyCode);
        
        var add = false;
        if (item == null)
        {
            add = await _commonService.AlarmBalanceCreateAsync(request.ConvertTo<AlarmBalanceConfigDto>());
        }
        
        return new NewMessageResponseBase<object>
        {
            ResponseStatus = new ResponseStatusApi(add == true ? ResponseCodeConst.Success : ResponseCodeConst.Error,
                add == true ? "Success" : "Error")
        };
    }

    public async Task<object> PutAsync(UpdateAlarmBalanceRequest request)
    {
        _logger.LogInformation($"UpdateAlarmBalanceRequest:{request.ToJson()}");
        var item = await _commonService.AlarmBalanceGetAsync(request.AccountCode, request.CurrencyCode);
        if (item == null)
            return new NewMessageResponseBase<object>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error, "Error")
            };
        var updateItem = item.ConvertTo<AlarmBalanceConfigDto>();
        updateItem.IsRun = request.IsRun;
        updateItem.Channel = request.Channel;
        updateItem.TeleChatId = request.TeleChatId;
        updateItem.MinBalance = request.MinBalance;
        updateItem.CurrencyCode = request.CurrencyCode;
        updateItem.AccountName = request.AccountName;
        var update = await _commonService.AlarmBalanceUpdateAsync(updateItem);
        return new NewMessageResponseBase<object>
        {
            ResponseStatus = new ResponseStatusApi(update == true ? ResponseCodeConst.Success : ResponseCodeConst.Error,
                update == true ? "Success" : "Error")
        };
    }
}