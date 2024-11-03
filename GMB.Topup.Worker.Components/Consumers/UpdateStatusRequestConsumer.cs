using System;
using System.Threading.Tasks;
using GMB.Topup.Gw.Domain.Services;
using GMB.Topup.Gw.Model.Commands;
using GMB.Topup.Gw.Model.Dtos;
using GMB.Topup.Gw.Model.Events;
using GMB.Topup.Worker.Components.WorkerProcess;
using GMB.Topup.Shared;
using GMB.Topup.Shared.ConfigDtos;
using GMB.Topup.Shared.Helpers;
using MassTransit;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using GMB.Topup.Contracts.Commands.Commons;
using GMB.Topup.Contracts.Requests.Commons;
using GMB.Topup.Discovery.Requests.TopupGateways;
using GMB.Topup.Discovery.Requests.Workers;

namespace GMB.Topup.Worker.Components.Consumers;

public class UpdateStatusRequestConsumer : IConsumer<CallBackTransCommand>
{
    private readonly IBus _bus;
    private readonly ILogger<UpdateStatusRequestConsumer> _logger;
    private readonly ISaleService _saleService;
    private readonly IWorkerProcess _workerProcess;
    private readonly GrpcClientHepper _grpcClient;


    public UpdateStatusRequestConsumer(ISaleService saleService, ILogger<UpdateStatusRequestConsumer> logger, IBus bus, GrpcClientHepper grpcClient, IWorkerProcess workerProcess)
    {
        _saleService = saleService;
        _logger = logger;
        _bus = bus;
        _workerProcess = workerProcess;
        _grpcClient = grpcClient;
    }

    public async Task Consume(ConsumeContext<CallBackTransCommand> context)
    {
        var callBackMessage = context.Message;
        _logger.LogInformation("UpdateStatusRequestConsumer request: " + callBackMessage.ToJson());
        await ProcessCallBackTrans(callBackMessage);
    }

    private async Task ProcessCallBackTrans(CallBackTransCommand callBackMessage)
    {
        var saleUpdate = await _saleService.SaleRequestGetAsync(callBackMessage.TransCode);
        if (saleUpdate == null)
            return;

        if (saleUpdate.Status == SaleRequestStatus.WaitForResult || saleUpdate.Status == SaleRequestStatus.Paid || saleUpdate.Status == SaleRequestStatus.ProcessTimeout)
        {
            switch (callBackMessage.Status)
            {
                case 1:
                    _logger.LogInformation($"Callback success:{callBackMessage.TransCode}");
                    saleUpdate.Status = SaleRequestStatus.Success;
                    await _saleService.SaleRequestUpdateStatusAsync(callBackMessage.TransCode,
                        string.Empty, SaleRequestStatus.Success);
                    await _bus.Publish(new ReportTransStatusMessage
                    {
                        TransCode = callBackMessage.TransCode,
                        ProviderCode = callBackMessage.ProviderCode,
                        PayTransRef = saleUpdate.ProviderTransCode,
                        Status = 1
                    });
                    break;
                case 3:
                    if (!string.IsNullOrEmpty(callBackMessage.AccountCode))
                    {
                        await _saleService.SaleRequestUpdateStatusAsync(callBackMessage.TransCode,
                            string.Empty, SaleRequestStatus.InProcessing);
                        var reponse = await TopupTransWorker(new SaleGateRequestDto()
                        {
                            FirstProvider = callBackMessage.AccountCode,
                            ServiceCode = saleUpdate.ServiceCode,
                            ProductCode = saleUpdate.ProductCode,
                            TransCode = saleUpdate.TransCode,
                            Mobile = saleUpdate.ReceiverInfo,
                            CategoryCode = saleUpdate.CategoryCode,
                            CreatedDate = DateTime.Now,
                        }, Convert.ToInt32(saleUpdate.Amount));

                        if (reponse.ResponseCode == ResponseCodeConst.Success
                            || reponse.ResponseCode == ResponseCodeConst.ResponseCode_TimeOut
                            || reponse.ResponseCode == ResponseCodeConst.ResponseCode_InProcessing
                            || reponse.ResponseCode == ResponseCodeConst.ResponseCode_WaitForResult)
                        {
                            await _saleService.SaleRequestUpdateStatusAsync(callBackMessage.TransCode,
                            string.Empty, SaleRequestStatus.Success);
                            await _bus.Publish(new ReportTransStatusMessage
                            {
                                TransCode = callBackMessage.TransCode,
                                ProviderCode = callBackMessage.ProviderCode,
                                PayTransRef = saleUpdate.ProviderTransCode,
                                Status = 1
                            });                            ;
                            return;
                        }
                    }

                    _logger.LogInformation(
                        $"CallBack Refund Request: {saleUpdate.TransCode}-{saleUpdate.TransRef}-{saleUpdate.Provider}");
                    await _bus.Publish<TransactionRefundCommand>(new
                    {
                        saleUpdate.TransCode,
                        CorrelationId = Guid.NewGuid()
                    });
                    break;
                default:
                    _logger.LogWarning($"Callback invalid SaleRequest Status:{callBackMessage.ToJson()}");
                    break;
            }
        }
        else if (callBackMessage.ProviderCode == ProviderConst.GATE && saleUpdate != null)
        {
            var saleGateUpdate = await _saleService.SaleGateRequestGetAsync(saleUpdate.TransCode);
            if (saleGateUpdate != null)
            {
                if (saleGateUpdate.Status == SaleRequestStatus.WaitForResult)
                {
                    if (saleUpdate.Status == SaleRequestStatus.Success && callBackMessage.ProviderCode == saleGateUpdate.Provider)
                    {
                        if (callBackMessage.Status == 1)
                        {
                            saleGateUpdate.Status = SaleRequestStatus.Success;
                            saleGateUpdate.EndDate = DateTime.Now;

                            if (callBackMessage.Amount < saleGateUpdate.TransAmount)
                            {
                                decimal transAmount = 0;
                                if (saleGateUpdate.Type.StartsWith("1"))
                                    transAmount = saleGateUpdate.TransAmount - callBackMessage.Amount;
                                else if (saleGateUpdate.Type.StartsWith("2"))
                                    transAmount = saleGateUpdate.TransAmount;

                                var reponse = await TopupTransWorker(saleGateUpdate, Convert.ToInt32(transAmount));
                                if (reponse.ResponseCode == ResponseCodeConst.Success)
                                    saleGateUpdate.TopupAmount = saleGateUpdate.TopupAmount + transAmount;

                            }
                            else saleGateUpdate.TopupAmount = saleGateUpdate.TopupAmount + callBackMessage.Amount;
                            saleGateUpdate.TopupProvider = saleUpdate.Provider;
                            await _saleService.SaleGateRequestUpdateAsync(saleGateUpdate);
                        }
                        else if (callBackMessage.Status == 3)
                        {
                            saleGateUpdate.Status = SaleRequestStatus.InProcessing;
                            await _saleService.SaleGateRequestUpdateAsync(saleGateUpdate);
                            decimal transAmount = saleGateUpdate.TransAmount;
                            var reponse = await TopupTransWorker(saleGateUpdate, Convert.ToInt32(transAmount));
                            if (reponse.ResponseCode == ResponseCodeConst.Success)
                            {
                                saleGateUpdate.TopupProvider = saleGateUpdate.FirstProvider;
                                saleGateUpdate.TopupAmount = saleGateUpdate.TopupAmount + transAmount;
                                saleGateUpdate.Status = SaleRequestStatus.Success;
                            }
                            else
                            {
                                if (reponse.ResponseCode == ResponseCodeConst.ResponseCode_TimeOut
                                 || reponse.ResponseCode == ResponseCodeConst.ResponseCode_WaitForResult)
                                    saleGateUpdate.Status = SaleRequestStatus.ProcessTimeout;
                                else saleGateUpdate.Status = SaleRequestStatus.Failed;
                            }

                            saleGateUpdate.EndDate = DateTime.Now;
                            await _saleService.SaleGateRequestUpdateAsync(saleGateUpdate);
                        }

                    }
                }
            }
        }
    }


    private async Task<MessageResponseBase> TopupTransWorker(SaleGateRequestDto saleGateUpdate, int transAmount)
    {
        var responseMessage = new MessageResponseBase();
        try
        {
            var publicKey = saleGateUpdate.FirstProvider;
            string productCode = "";
            var gateProviderInfo = await _grpcClient.GetClientCluster(GrpcServiceName.TopupGateway).SendAsync(new GateProviderInfoRequest()
            {
                ProviderCode = publicKey
            });

            if (saleGateUpdate.TransAmount == transAmount)
                productCode = saleGateUpdate.ProductCode;
            else
            {
                var code = transAmount / 1000;
                productCode = saleGateUpdate.CategoryCode + "_" + code.ToString();
            }

            if (gateProviderInfo.ResponseStatus.ErrorCode == ResponseCodeConst.Success)
            {
                var infoAccount = gateProviderInfo.Results;
                var requestDto = new WorkerTopupRequest
                {
                    Amount = transAmount,
                    Channel = Channel.API,
                    AgentType = AgentType.AgentApi,
                    AccountType = SystemAccountType.MasterAgent,
                    CategoryCode = saleGateUpdate.CategoryCode,
                    ProductCode = productCode,
                    PartnerCode = infoAccount.ApiUser,
                    ReceiverInfo = saleGateUpdate.Mobile,
                    RequestIp = string.Empty,
                    ServiceCode = saleGateUpdate.ServiceCode,
                    StaffAccount = infoAccount.ApiUser,
                    StaffUser = infoAccount.ApiUser,
                    TransCode = saleGateUpdate.TransCode,
                    RequestDate = DateTime.Now,
                    IsCheckReceiverType = false,
                    IsNoneDiscount = false,
                    DefaultReceiverType = "",
                    IsCheckAllowTopupReceiverType = false
                };

                var reponseTopup = await _workerProcess.TopupRequest(requestDto);

                _logger.LogInformation($"{saleGateUpdate.TransCode} - {saleGateUpdate.FirstProvider} TopupTransWorker: reponse: {reponseTopup.ToJson()}");

                if (!string.IsNullOrEmpty(infoAccount.AlarmTeleChatId) && infoAccount.IsAlarm)
                {
                    try
                    {
                        BotMessageType type = reponseTopup.ResponseStatus.ErrorCode == ResponseCodeConst.Success
                            ? BotMessageType.Message : reponseTopup.ResponseStatus.ErrorCode == ResponseCodeConst.Error
                            ? BotMessageType.Error : BotMessageType.Wraning;

                        bool isSend = true;
                        var sKeys = infoAccount.PublicKey.Split('|');
                        if (sKeys.Length >= 3)
                        {
                            var sKey = sKeys[2].Split('-');
                            if (type == BotMessageType.Message && sKey[0] == "0")
                                isSend = false;
                            else if (type == BotMessageType.Wraning && sKey.Length >= 2 && sKey[1] == "0")
                                isSend = false;
                            else if (type == BotMessageType.Error && sKey.Length >= 3 && sKey[2] == "0")
                                isSend = false;
                        }

                        if (isSend)
                        {
                            await _bus.Publish<SendBotMessageToGroup>(new
                            {
                                MessageType = type,
                                BotType = BotType.Private,
                                ChatId = infoAccount.AlarmTeleChatId,
                                Module = "TopupGate",
                                Title = "Nạp Bù GD",
                                Message =
                                    $"Mã GD: {requestDto.TransCode}\n" +
                                    $"Tài khoản nạp: {requestDto.PartnerCode}\n" +
                                    $"Sản phẩm {requestDto.ProductCode}\n" +
                                    $"Số thụ hưởng: {requestDto.ReceiverInfo}\n" +
                                    $"Số tiền nạp: {requestDto.Amount.ToFormat("đ")}\n" +
                                    $"Hình thức nap: Nạp bù tiền\n" +
                                    $"Trạng thái: {reponseTopup.ResponseStatus.ErrorCode}\n" +
                                    $"Nội dung: {reponseTopup.ResponseStatus.Message}",
                                TimeStamp = DateTime.Now,
                                CorrelationId = Guid.NewGuid()
                            }); ;
                        }
                    }
                    catch (Exception alarmTeleex)
                    {
                        _logger.LogError($"{saleGateUpdate.TransCode} - {saleGateUpdate.FirstProvider} TopupTransWorker_Work_BotMessage Exception: {alarmTeleex}");
                    }
                }

                var responseStatus = new MessageResponseBase
                {
                    TransCode = requestDto.TransCode,
                    ResponseCode = reponseTopup.ResponseStatus.ErrorCode,
                    ResponseMessage = reponseTopup.ResponseStatus.Message
                };

                return responseStatus;
            }
            else
            {
                _logger.LogInformation($"GET: {publicKey} TopupTransWorker_Worker_Profile: {gateProviderInfo.ToJson()}");
            }

        }
        catch (Exception ex)
        {
            _logger.LogError($"{saleGateUpdate.TransCode} - {saleGateUpdate.FirstProvider} TopupTransWorker_Work Exception: {ex}");
        }

        return responseMessage;
    }

}

public class UpdateStatusConsumerDefinition :
    ConsumerDefinition<UpdateStatusRequestConsumer>
{
    private readonly IServiceProvider _serviceProvider;

    public UpdateStatusConsumerDefinition(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        ConcurrentMessageLimit = 30;
    }

    protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<UpdateStatusRequestConsumer> consumerConfigurator)
    {
        endpointConfigurator.UseServiceScope(_serviceProvider);
    }
}