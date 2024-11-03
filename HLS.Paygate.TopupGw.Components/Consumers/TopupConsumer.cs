using System;
using System.Threading.Tasks;
using HLS.Paygate.Gw.Model.Commands.TopupGw;
using HLS.Paygate.Gw.Model.Events.TopupGw;
using HLS.Paygate.Shared;
using HLS.Paygate.TopupGw.Components.Connectors;
using HLS.Paygate.TopupGw.Contacts.Dtos;
using HLS.Paygate.TopupGw.Contacts.Enums;
using HLS.Paygate.TopupGw.Domains.BusinessServices;
using MassTransit;
using Microsoft.Extensions.Logging;
using ServiceStack;

namespace HLS.Paygate.TopupGw.Components.Consumers;

public class TopupConsumer : IConsumer<TopupCommand>
{
    private readonly ILogger<TopupConsumer> _logger; // = LogManager.GetLogger("TopupConsumer");

    private readonly ITopupGatewayService _topupGatewayService;
    private IGatewayConnector _gatewayConnector;

    public TopupConsumer(ITopupGatewayService topupGatewayService,
        ILogger<TopupConsumer> logger)
    {
        _topupGatewayService = topupGatewayService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<TopupCommand> context)
    {
        _logger.LogInformation($"{context.Message.TransRef} Received topupCommand: " + context.Message.ToJson());
        var amount = context.Message.Amount;
        if (amount <= 0)
            throw new ArgumentNullException(nameof(amount));

        var receiverInfo = context.Message.ReceiverInfo;
        if (string.IsNullOrEmpty(receiverInfo))
            throw new ArgumentNullException(nameof(receiverInfo));

        var transRef = context.Message.TransRef;
        if (string.IsNullOrEmpty(transRef))
            throw new ArgumentNullException(nameof(transRef));

        var providerCode = context.Message.ProviderCode;
        if (string.IsNullOrEmpty(providerCode))
            throw new ArgumentNullException(nameof(providerCode));

        var providerInfo = await _topupGatewayService.ProviderInfoCacheGetAsync(providerCode);

        if (providerInfo == null)
        {
            _logger.LogInformation("providerInfo is null");
            throw new ArgumentNullException(nameof(providerCode));
        }

        var transRequest = new TopupRequestLogDto
        {
            Id = context.Message.CorrelationId,
            ReceiverInfo = context.Message.ReceiverInfo,
            Status = TransRequestStatus.Init,
            RequestDate = context.Message.RequestDate,
            TransIndex = "I" + DateTime.Now.ToString("yyMMddHHmmssffff"),
            TransAmount = (int) amount,
            TransRef = transRef,
            TransCode = context.Message.TransCodeProvider,
            ReferenceCode = context.Message.ReferenceCode,
            PartnerCode = context.Message.PartnerCode,
            ProviderCode = providerCode,
            ServiceCode = context.Message.ServiceCode,
            Vendor = string.IsNullOrEmpty(context.Message.Vendor)
                ? context.Message.ProductCode.Split('_')[0]
                : context.Message.Vendor,
            ProductCode = context.Message.ProductCode,
            CategoryCode = context.Message.CategoryCode
        };

        transRequest = await _topupGatewayService.TopupRequestLogCreateAsync(transRequest);

        if (transRequest != null)
        {
            _gatewayConnector = HostContext.Container.ResolveNamed<IGatewayConnector>(providerCode.Split('-')[0]);

            if (_gatewayConnector != null)
            {
                await context.Publish<TopupSentToProvider>(new
                {
                    context.Message.CorrelationId
                });
                //_logger.LogInformation($"GatewayConnector {_gatewayConnector.ToJson()} TopupRequest:{transRequest.TransRef}-{transRequest.TransCode}-{transRequest.ProviderCode}");
                var result = await _gatewayConnector.TopupAsync(transRequest, providerInfo);
                //_logger.LogInformation($"{transRequest.TransCode}|{transRequest.TransRef}|{transRequest.TransAmount} topupCommand reponse : { result.ToJson()}");
                result.ProviderCode = providerCode;
                if (result is {ExtraInfo: "STOP"})
                {
                    await context.RespondAsync(result);
                    return;
                }

                var objSend = transRequest.ConvertTo<SendWarningDto>();
                objSend.Type = SendWarningDto.Topup;
                await _topupGatewayService.SendTelegram(result, objSend);
                await context.RespondAsync(result);
            }
            else
            {
                _logger.LogInformation("Can not create connector: " + transRef);

                await context.RespondAsync(new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error,
                    ResponseMessage = "Fail to create request",
                    ExtraInfo = transRequest.TransCode
                });
            }
        }
        else
        {
            _logger.LogInformation("Error create transRequest with: " + transRef);

            await context.RespondAsync(new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Fail to create request",
                ExtraInfo = transRequest.TransCode
            });
        }
    }
}

public class TopupConsumerDefinition :
    ConsumerDefinition<TopupConsumer>
{
    private readonly IServiceProvider _serviceProvider;

    public TopupConsumerDefinition(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        ConcurrentMessageLimit = 256;
    }

    protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<TopupConsumer> consumerConfigurator)
    {
        endpointConfigurator.UseMessageRetry(r =>
        {
            r.Ignore<InvalidOperationException>();
            r.None();
        });
        // endpointConfigurator.UseServiceScope(_serviceProvider);
        endpointConfigurator.UseInMemoryOutbox();
        endpointConfigurator.DiscardFaultedMessages();
        // consumerConfigurator.Message<CardSaleRequestCommand>(m => m.UseFilter(new ContainerScopedFilter()));
    }
}