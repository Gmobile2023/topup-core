using System;
using System.Linq;
using System.Threading.Tasks;
using Topup.Contracts.Commands.Backend;
using Topup.Contracts.Commands.Commons;
using Topup.Contracts.Requests.Commons;
using Topup.Shared;
using Topup.Shared.Helpers;
using Topup.Shared.UniqueIdGenerator;


using Topup.TopupGw.Domains.BusinessServices;
using MassTransit;
using Microsoft.Extensions.Logging;



using Topup.Discovery.Requests.TopupGateways;
using Topup.TopupGw.Contacts.Dtos;
using Topup.TopupGw.Contacts.Enums;
using ServiceStack;
using Topup.TopupGw.Components.Connectors;

namespace Topup.TopupGw.Components.TopupGwProcess;

public partial class TopupGwProcess : ITopupGwProcess
{
    private readonly IBus _bus;
    private readonly ILogger<TopupGwProcess> _logger;
    private readonly ITopupGatewayService _topupGatewayService;
    private readonly ITransCodeGenerator _transCodeGenerator;
    private IGatewayConnector _gatewayConnector;

    public TopupGwProcess(ILogger<TopupGwProcess> logger, ITopupGatewayService topupGatewayService, IBus bus,
        ITransCodeGenerator transCodeGenerator)
    {
        _topupGatewayService = topupGatewayService;
        _bus = bus;
        _transCodeGenerator = transCodeGenerator;
        _logger = logger;
    }


    public async Task<NewMessageResponseBase<ResponseProvider>> TopupRequest(GateTopupRequest request)
    {
        try
        {
            var response = new NewMessageResponseBase<ResponseProvider>();
            _logger.LogInformation("TopupRequest: " + request.ToJson());
            var amount = request.Amount;
            if (amount <= 0)
            {
                response.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_00,
                    "Số tiền không hợp lệ");
                return response;
            }

            var receiverInfo = request.ReceiverInfo;
            if (string.IsNullOrEmpty(receiverInfo))
            {
                response.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_00,
                    "Tài khoản nạp không tồn tại");
                return response;
            }

            var transRef = request.TransRef;
            if (string.IsNullOrEmpty(transRef))
            {
                response.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_00,
                    "Mã giao dịch đối tác không tồn tại");
                return response;
            }

            var providerCode = request.ProviderCode;
            if (string.IsNullOrEmpty(providerCode))
            {
                response.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_00,
                    "Provider not found");
                return response;
            }

            var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(providerCode);

            if (providerInfo == null)
            {
                _logger.LogInformation("providerInfo is null");
                return response;
            }

            var transRequest = new TopupRequestLogDto
            {
                Id = Guid.NewGuid(),
                ReceiverInfo = request.ReceiverInfo,
                Status = TransRequestStatus.Init,
                RequestDate = DateTime.Now,
                TransIndex = "I" + DateTime.Now.ToString("yyMMddHHmmssffff"),
                TransAmount = (int)amount,
                TransRef = transRef,
                TransCode = request.TransCodeProvider,
                ReferenceCode = request.ReferenceCode,
                PartnerCode = request.PartnerCode,
                ProviderCode = providerCode,
                ServiceCode = request.ServiceCode,
                Vendor = string.IsNullOrEmpty(request.Vendor)
                    ? request.ProductCode.Split('_')[0]
                    : request.Vendor,
                ProductCode = request.ProductCode,
                CategoryCode = request.CategoryCode,
                ProviderMaxWaitingTimeout = request.ProviderMaxWaitingTimeout,
                ProviderSetTransactionTimeout = request.ProviderSetTransactionTimeout,
                IsEnableResponseWhenJustReceived = request.IsEnableResponseWhenJustReceived,
                StatusResponseWhenJustReceived = request.StatusResponseWhenJustReceived,
                WaitingTimeResponseWhenJustReceived = request.WaitingTimeResponseWhenJustReceived
            };
            transRequest = await _topupGatewayService.TopupRequestLogCreateAsync(transRequest);
            if (transRequest != null)
            {
                _gatewayConnector =
                    HostContext.Container
                        .ResolveNamed<IGatewayConnector>(providerCode.Split('-')[0]);
                if (_gatewayConnector != null)
                {
                    var startTime = DateTime.Now;
                    var result = await _gatewayConnector.TopupAsync(transRequest, providerInfo);
                    var endTime = DateTime.Now;
                    var processedTime = endTime.Subtract(startTime).TotalSeconds;
                    if (providerInfo.ProcessTimeAlarm > 0 && processedTime > providerInfo.ProcessTimeAlarm)
                    {
                        await AlarmProcessedTime(result, transRequest.ConvertTo<SendWarningDto>(), providerInfo,Math.Round(processedTime));
                    }

                    _logger.LogInformation(
                        "{TransRequestTransCode}|{TransRequestTransRef}|{TransRequestProviderCode} Connector response : {Json}",
                        transRequest.TransCode, transRequest.TransRef, transRequest.ProviderCode, result.ToJson());
                    response.ResponseStatus = new ResponseStatusApi(result.ResponseCode, result.ResponseMessage);
                    response.Results = new ResponseProvider
                    {
                        Code = result.ProviderResponseCode,
                        Message = result.ProviderResponseMessage,
                        ProviderCode = result.ProviderCode,
                        ProviderResponseTransCode = result.ProviderResponseTransCode,
                        ReceiverType = result.ReceiverType
                    };
                    //Xử lý đóng kênh tự động
                    if (providerInfo.TotalTransError > 0)
                        await CheckAutoClose(response, providerInfo);
                    //Cảnh báo nếu gd lỗi
                    if (providerInfo.IsAlarm && result.ResponseCode != ResponseCodeConst.Success)
                    {
                        await AlarmProvider(result, transRequest.ConvertTo<SendWarningDto>(), providerInfo);
                    }

                    return response;
                }

                _logger.LogError($"Can not create connector: {request.TransRef}-{request.TransCodeProvider}");
                response.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error, "Fail to create request");
                return response;
            }

            _logger.LogInformation(
                $"Error create transRequest with: {request.TransRef}-{request.TransCodeProvider}");
            response.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error, "Fail to create request");
            return response;
        }
        catch (Exception e)
        {
            _logger.LogError(
                $"{request.TransRef}-{request.TransCodeProvider}-{request.ProviderCode}-TopupRequestError: " + e);
            return new NewMessageResponseBase<ResponseProvider>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_WaitForResult,
                    "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ. Xin vui lòng thử lại sau")
            };
        }
    }

    private async Task CheckAutoClose(NewMessageResponseBase<ResponseProvider> response,
        ProviderInfoDto providerInfo)
    {
        try
        {
            switch (response.ResponseStatus.ErrorCode)
            {
                case ResponseCodeConst.Success:
                    await SetAutoClose(providerInfo, true);
                    break;
                case ResponseCodeConst.ResponseCode_WaitForResult:
                case ResponseCodeConst.ResponseCode_TimeOut:
                case ResponseCodeConst.ResponseCode_InProcessing:
                    await SetAutoClose(providerInfo, false);
                    break;
                default:
                {
                    if (providerInfo.IsAutoCloseFail)
                    {
                        if (string.IsNullOrEmpty(providerInfo.IgnoreCode))
                            await SetAutoClose(providerInfo, false);
                        else if (!providerInfo.IgnoreCode.Contains(response.ResponseStatus.ErrorCode))
                            await SetAutoClose(providerInfo, false);
                    }

                    break;
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError("CheckAutoClose error: {Error}", e.Message);
        }
    }

    private async Task SetAutoClose(ProviderInfoDto providerInfoDto, bool success)
    {
        try
        {
            var count = await _transCodeGenerator.AutoCloseIndex(providerInfoDto.ProviderCode, success);

            if (providerInfoDto.TotalTransError > 0 && count > providerInfoDto.TotalTransError)
            {
                _logger.LogInformation($"SetAutoClose:{providerInfoDto.ProviderCode}");
                //Lock provider
                await _bus.Publish<LockProviderCommand>(new
                {
                    CorrelationId = Guid.NewGuid(),
                    providerInfoDto.ProviderCode,
                    providerInfoDto.TimeClose
                });
                //ResetAuto
                await _transCodeGenerator.ResetAutoCloseIndex(providerInfoDto.ProviderCode);
                await _bus.Publish<SendBotMessage>(new
                {
                    MessageType = BotMessageType.Wraning,
                    BotType = BotType.Channel,
                    Module = "TopupGw",
                    Title =
                        $"Kênh:{providerInfoDto.ProviderCode} đóng tự động",
                    Message =
                        $"NCC {providerInfoDto.ProviderCode} sẽ đóng tự động.\nSố lượng GD không thành công liên tiếp:{count}\nThời gian đóng:{providerInfoDto.TimeClose} phút",
                    TimeStamp = DateTime.Now,
                    CorrelationId = Guid.NewGuid()
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("AutoCloseCheck error: {Error}", ex.Message);
        }
    }

    private async Task AlarmProvider(MessageResponseBase response, SendWarningDto data, ProviderInfoDto providerInfoDto)
    {
        try
        {
            var isSend = true;
            if (string.IsNullOrEmpty(response.ProviderResponseCode))
                response.ProviderResponseCode =
                    ResponseCodeConst.ResponseCode_GMB_CODE; //neu k co ma loi ncc. lay ma loi NT
            if (!string.IsNullOrEmpty(providerInfoDto.ErrorCodeNotAlarm) &&
                !string.IsNullOrEmpty(response.ProviderResponseCode))
            {
                var check = providerInfoDto.ErrorCodeNotAlarm.Split(',', '|', ';')
                    .FirstOrDefault(c => c == response.ProviderResponseCode);
                if (check is not null)
                    isSend = false;
            }

            if (!string.IsNullOrEmpty(providerInfoDto.MessageNotAlarm) &&
                !string.IsNullOrEmpty(response.ProviderResponseMessage))
            {
                var keys = providerInfoDto.MessageNotAlarm.Split(',', '|', ';');
                var existsMessage = keys.Any(value => response.ProviderResponseMessage.Contains(value));
                if (existsMessage)
                    isSend = false;
            }

            if (isSend)
            {
                _logger.LogInformation($"{data.TransCode}-{data.TransRef}-AlarmProvider process:{response.ToJson()}");
                var title = response.ResponseCode is ResponseCodeConst.ResponseCode_WaitForResult
                    or ResponseCodeConst.ResponseCode_TimeOut or ResponseCodeConst.ResponseCode_RequestReceived
                    or ResponseCodeConst.ResponseCode_InProcessing
                    ? $"Cảnh báo GD kênh {data.ProviderCode} chưa có KQ"
                    : $"Cảnh báo GD kênh {data.ProviderCode} bị lỗi";

                await _bus.Publish<SendBotMessageToGroup>(new
                {
                    MessageType = BotMessageType.Error,
                    BotType = BotType.Private,
                    ChatId = !string.IsNullOrEmpty(providerInfoDto.AlarmTeleChatId)
                        ? providerInfoDto.AlarmTeleChatId
                        : "-908841190",
                    Module = "TopupGate",
                    Title = title,
                    Message =
                        $"Mã NCC: {data.TransCode}\n" +
                        $"NCC: {data.ProviderCode}\n" +
                        $"Mã GD: {data.TransRef}\n" +
                        $"Mã đối tác: {data.ReferenceCode}\n" +
                        $"Đại lý: {data.PartnerCode}\n" +
                        $"Sản phẩm {data.ProductCode}\n" +
                        $"Tài khoản thụ hưởng: {data.ReceiverInfo}\n" +
                        $"Số tiền: {data.TransAmount.ToFormat("đ")}\n" +
                        $"Mã lỗi NT: {response.ResponseCode}\n" +
                        $"Thông báo lỗi NT: {response.ResponseMessage}\n" +
                        $"Mã lỗi NCC: {response.ProviderResponseCode}\n" +
                        $"Thông báo lỗi NCC: {response.ProviderResponseMessage}\n" +
                        $"Exception : {response.Exception}",
                    TimeStamp = DateTime.Now,
                    CorrelationId = Guid.NewGuid()
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"{data.TransCode}-{data.TransRef}-AlarmProvider error:{ex}");
        }
    }

    private async Task AlarmProcessedTime(MessageResponseBase response, SendWarningDto data,
        ProviderInfoDto providerInfoDto, double timeProcessed)
    {
        try
        {
            _logger.LogInformation($"{data.TransCode}-{data.TransRef}-AlarmProcessedTime:{response.ToJson()}");
            await _bus.Publish<SendBotMessageToGroup>(new
            {
                MessageType = BotMessageType.Wraning,
                BotType = BotType.Private,
                ChatId = !string.IsNullOrEmpty(providerInfoDto.AlarmTeleChatId)
                    ? providerInfoDto.AlarmTeleChatId
                    : "-908841190",
                Module = "TopupGate",
                Title = $"Cảnh báo giao dịch NCC {providerInfoDto.ProviderCode} xử lý giao dịch chậm. TG Xử lý {timeProcessed}s",
                Message =
                    $"Mã NCC: {data.TransCode}\n" +
                    $"NCC: {data.ProviderCode}\n" +
                    $"Mã GD: {data.TransRef}\n" +
                    $"Mã đối tác: {data.ReferenceCode}\n" +
                    $"Đại lý: {data.PartnerCode}\n" +
                    $"Sản phẩm {data.ProductCode}\n" +
                    $"Tài khoản thụ hưởng: {data.ReceiverInfo}\n" +
                    $"Số tiền: {data.TransAmount.ToFormat("đ")}\n" +
                    $"Time xử lý: {timeProcessed}s",
                TimeStamp = DateTime.Now,
                CorrelationId = Guid.NewGuid()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"{data.TransCode}-{data.TransRef}-AlarmProcessedTime error:{ex}");
        }
    }
}