using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GreenPipes;
using HLS.Paygate.Gw.Model.Commands.TopupGw;
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
using Paygate.Discovery.Requests.Stocks;
using ServiceStack;

namespace HLS.Paygate.TopupGw.Components.Consumers
{
    public class CardBatchConsumer : IConsumer<CardBatchCommand>
    {
        private readonly ILogger<CardBatchConsumer> _logger;
        private readonly IServiceGateway _gateway;
        private readonly ITopupGatewayService _topupGatewayService;

        private IGatewayConnector _gatewayConnector;
        public CardBatchConsumer(ITopupGatewayService topupGatewayService, ILogger<CardBatchConsumer> logger)
        {
            _topupGatewayService = topupGatewayService;
            _gateway = HostContext.AppHost.GetServiceGateway();
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<CardBatchCommand> context)
        {
            _logger.LogInformation($"{context.Message.TransRef} Received CardBatchCommand: " + context.Message.ToJson());

            var transRef = context.Message.TransRef;
            if (string.IsNullOrEmpty(transRef))
                throw new ArgumentNullException(nameof(transRef));

            var providerCode = context.Message.ProviderCode;
            if (string.IsNullOrEmpty(providerCode))
                throw new ArgumentNullException(nameof(providerCode));
            var productCode = context.Message.ProductCode;
            if (string.IsNullOrEmpty(productCode))
                throw new ArgumentNullException(nameof(productCode));

            if (context.Message.AutoImportToStock)
                await context.RespondAsync(new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.Success,
                    ResponseMessage = $"Your command submit Ok: {transRef}"
                });

            var transRequest = new CardRequestLogDto
            {
                Quantity = context.Message.Quantity,
                ProductCode = productCode,
                Status = TransRequestStatus.Init,
                RequestDate = DateTime.Now,
                TransCode = context.Message.TransCodeProvider,
                TransRef = transRef,
                TransIndex = "C" + DateTime.Now.ToString("yyMMddHHmmssffff"),
                TransAmount = context.Message.Amount,
                Vendor = string.IsNullOrEmpty(context.Message.Vendor)
                    ? context.Message.ProductCode.Split('_')[0]
                    : context.Message.Vendor.Split('_')[0],
                ProviderCode = providerCode,
                CategoryCode = context.Message.CategoryCde,
                PartnerCode = context.Message.PartnerCode,
                ReferenceCode = context.Message.ReferenceCode,
                ServiceCode = context.Message.ServiceCode,

            };

            if (string.IsNullOrEmpty(transRequest.TransCode))
                transRequest.TransCode =  DateTime.Now.ToString("yyMMddHHmmssffff");

            transRequest = await _topupGatewayService.CardRequestLogCreateAsync(transRequest);

            if (transRequest != null)
            {
                // var cardsFk = FakeCardItemsImport(context.Message.Quantity, context.Message.Amount);
                //  await context.Send<StockCardsImportCommand>(new
                //  {
                //      BatchCode = transRef,
                //      ProductCode = productCode,
                //      CardItems = cardsFk.ConvertTo<List<CardItemsImport>>()
                //  });
                // //  return;

                _gatewayConnector = HostContext.Container.ResolveNamed<IGatewayConnector>(providerCode.Split('-')[0]);
                _logger.LogInformation($"GatewayConnector {_gatewayConnector.ToJson()} CardRequest:{transRequest.TransRef}-{transRequest.TransCode}-{transRequest.ProviderCode}");
                var result = await _gatewayConnector.CardGetByBatchAsync(transRequest);

                _logger.LogInformation($"{transRequest.TransRef}|{transRequest.TransCode} Get card result: " + result.ToJson());

                if (!context.Message.AutoImportToStock)
                {
                    await context.RespondAsync(result);
                }
                else
                {
                    if (result.ResponseCode == ResponseCodeConst.Success)
                    {
                        var cards = result.Payload.ToString().FromJson<List<CardRequestResponseDto>>();
                        _logger.LogInformation($"{transRequest.TransRef}|{transRequest.TransCode} Get card reponse : {result.ResponseCode}|{result.ResponseMessage}");
                        var listCardToImport = new List<CardItemsImport>();

                        foreach (var cardDto in cards)
                            listCardToImport.Add(new CardItemsImport
                            {
                                Serial = cardDto.Serial,
                                CardCode = cardDto.CardCode,
                                CardValue = decimal.Parse(cardDto.CardValue),
                                ExpiredDate = cardDto.ExpireDate
                            });

                        var rs = _gateway.SendAsync(new StockCardImportListRequest
                        {
                            BatchCode = transRef,
                            ProductCode = productCode,
                            CardItems = listCardToImport
                        });
                        _logger.LogInformation($"{transRequest.TransRef}|{transRequest.TransCode} StockCardImportListRequestreponse : {rs.ToJson()}");
                        // await context.Send<StockCardsImportCommand>(new
                        // {
                        //     BatchCode = transRef,
                        //     ProductCode = productCode,
                        //     CardItems = listCardToImport
                        // });
                    }
                    else
                    {

                        var objSend = transRequest.ConvertTo<SendWarningDto>();
                        objSend.Type = SendWarningDto.PinCode;
                        await _topupGatewayService.SendTelegram(result, objSend);
                    }
                }
            }
            else
            {
                _logger.LogInformation("Error create transRequest with: " + transRef);

                if (!context.Message.AutoImportToStock)
                    await context.RespondAsync(new MessageResponseBase
                    {
                        ResponseCode = ResponseCodeConst.Error,
                        ResponseMessage = "Fail to create request"
                    });
            }
        }

        private List<object> FakeCardItemsImport(int quantity, decimal cardValue)
        {
            var cards = new List<object>();
            var i = 0;
            var dateLog = DateTime.Now.ToString("yyMMddHHmmssffff");
            while (i < quantity)
            {
                var obj = new
                {
                    CardCode = $"{dateLog}_{i}",
                    Serial = $"{dateLog}_{i}",
                    ExpiredDate = DateTime.Now.AddYears(2),
                    CardValue = cardValue
                };
                cards.Add(obj);
                i++;
            }

            return cards;
        }
    }

    public class CardBatchConsumerDefinition :
        ConsumerDefinition<CardBatchConsumer>
    {
        private readonly IServiceProvider _serviceProvider;

        public CardBatchConsumerDefinition(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            ConcurrentMessageLimit = 30;
        }

        protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpointConfigurator,
            IConsumerConfigurator<CardBatchConsumer> consumerConfigurator)
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
