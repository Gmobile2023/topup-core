using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GreenPipes;
using HLS.Paygate.Gw.Model.Commands.TopupGw;
using HLS.Paygate.Gw.Model.Events;
using HLS.Paygate.Shared;
using HLS.Paygate.Shared.Dtos;
using HLS.Paygate.TopupGw.Components.Connectors;
using HLS.Paygate.TopupGw.Contacts.Dtos;
using HLS.Paygate.TopupGw.Contacts.Enums;
using HLS.Paygate.TopupGw.Domains.BusinessServices;
using MassTransit;
using MassTransit.ConsumeConfigurators;
using MassTransit.Definition;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using ServiceStack;

namespace HLS.Paygate.TopupGw.Components.Consumers
{
    public class PayBillConsumer : IConsumer<PayBillCommand>
    {
        private readonly ILogger<PayBillConsumer> _logger; // = LogManager.GetLogger("PayBillConsumer");

        private readonly ITopupGatewayService _topupGatewayService;
        private IGatewayConnector _gatewayConnector;


        public PayBillConsumer(ITopupGatewayService topupGatewayService, ILogger<PayBillConsumer> logger)
        {
            _topupGatewayService = topupGatewayService;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<PayBillCommand> context)
        {
            _logger.LogInformation("Received payBillCommand: " + context.Message.ToJson());
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

            var transRequest = new PayBillRequestLogDto
            {
                ReceiverInfo = context.Message.ReceiverInfo,
                Status = TransRequestStatus.Init,
                RequestDate = context.Message.RequestDate,
                TransCode = context.Message.TransCodeProvider,
                TransIndex = "V" + DateTime.Now.ToString("yyMMddHHmmssffff"),
                TransAmount = amount,
                TransRef = transRef,
                ProviderCode = providerCode,
                ProductCode = context.Message.ProductCode,
                Vendor = context.Message.Vendor,
                PartnerCode = context.Message.PartnerCode,
                ReferenceCode = context.Message.ReferenceCode,
                ServiceCode = context.Message.ServiceCode,
                CategoryCode = context.Message.CategoryCde,
            };



            transRequest = await _topupGatewayService.PayBillRequestLogCreateAsync(transRequest);

            if (transRequest != null)
            {
                _gatewayConnector = HostContext.Container.ResolveNamed<IGatewayConnector>(providerCode.Split('-')[0]);
                _logger.LogInformation($"GatewayConnector {_gatewayConnector.ToJson()} PayBillRequest:{transRequest.TransRef}-{transRequest.TransCode}-{transRequest.ProviderCode}");
                var result = await _gatewayConnector.PayBillAsync(transRequest);
                _logger.LogInformation($"{transRequest.TransCode}|{transRequest.TransRef} payBillCommand reponse : { result.ToJson()}");
                result.ProviderCode = providerCode;


                var objSend = transRequest.ConvertTo<SendWarningDto>();
                objSend.Type = SendWarningDto.VBILL;
                await _topupGatewayService.SendTelegram(result, objSend);
                await context.RespondAsync(result);
            }
            else
            {
                _logger.LogInformation("Error create transRequest with: " + transRef);

                await context.RespondAsync(new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error,
                    ResponseMessage = "Fail to create request",
                    ExtraInfo = transRequest.TransCode,
                });
            }
        }

        private async Task<MessageResponseBase> PayBillProviderPriority(List<ProviderConfig> providerCodes,
           PayBillRequestLogDto transRequest)
        {
            MessageResponseBase result = new MessageResponseBase();
            foreach (var item in providerCodes.OrderBy(c => c.Priority))
            {
                try
                {
                    var config = item;
                    var providerCode = config.ProviderCode;
                    transRequest.TransIndex = "V" + DateTime.Now.ToString("yyMMddHHmmssffff");
                    transRequest.TransCode = string.IsNullOrEmpty(config.TransCodeConfig) ? transRequest.TransRef : config.TransCodeConfig + "_" + transRequest.TransRef;
                    transRequest.ProviderCode = providerCode;
                    _gatewayConnector = HostContext.Container.ResolveNamed<IGatewayConnector>(providerCode.Split('-')[0]);
                    if (_gatewayConnector != null)
                    {
                        _logger.LogInformation($"GatewayConnector {_gatewayConnector.ToJson()} PayBillRequest:{transRequest.TransRef}-{transRequest.TransCode}-{transRequest.ProviderCode}");
                        transRequest.RequestDate = DateTime.Now;
                        transRequest = await _topupGatewayService.PayBillRequestLogCreateAsync(transRequest);
                        result = await _gatewayConnector.PayBillAsync(transRequest);
                        _logger.LogInformation($"{transRequest.TransCode}|{transRequest.TransRef}|{transRequest.TransAmount} payBillCommand reponse : { result.ToJson()}");
                        result.ProviderCode = providerCode;
                        if (result.ResponseCode == ResponseCodeConst.Error)
                            continue;
                        else
                        {
                            var objSend = transRequest.ConvertTo<SendWarningDto>();
                            objSend.Type = SendWarningDto.VBILL;
                            await _topupGatewayService.SendTelegram(result, objSend);
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInformation($"PayBillProviderPriority Exception: {ex.Message}|{ex.StackTrace}|{ex.InnerException}");
                    break;
                }
            }

            return result;
        }
    }

    public class PayBillConsumerDefinition :
        ConsumerDefinition<PayBillConsumer>
    {
        private readonly IServiceProvider _serviceProvider;

        public PayBillConsumerDefinition(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            ConcurrentMessageLimit = 256;
        }

        protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpointConfigurator,
            IConsumerConfigurator<PayBillConsumer> consumerConfigurator)
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
}
