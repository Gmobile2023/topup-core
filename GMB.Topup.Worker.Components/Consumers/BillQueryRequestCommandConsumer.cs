using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GreenPipes;
using HLS.Paygate.Gw.Model.Commands;
using HLS.Paygate.Gw.Model.Commands.TopupGw;
using HLS.Paygate.Shared;
using HLS.Paygate.Shared.AbpConnector;
using HLS.Paygate.Worker.Components.Connectors;
using MassTransit;
using MassTransit.ConsumeConfigurators;
using MassTransit.Definition;
using Microsoft.Extensions.Logging;
using ServiceStack;

namespace HLS.Paygate.Worker.Components.Consumers
{
    public class BillQueryRequestCommandConsumer : IConsumer<BillQueryRequestCommand>
    {
        private readonly ILogger<BillQueryRequestCommandConsumer> _logger;
        private readonly ExternalServiceConnector _externalServiceConnector;
        private readonly IRequestClient<BillQueryCommand> _billQueryClient;

        public BillQueryRequestCommandConsumer(ILogger<BillQueryRequestCommandConsumer> logger, ExternalServiceConnector externalServiceConnector, IRequestClient<BillQueryCommand> billQueryClient)
        {
            _logger = logger;
            _externalServiceConnector = externalServiceConnector;
            _billQueryClient = billQueryClient;
        }

        public async Task Consume(ConsumeContext<BillQueryRequestCommand> context)
        {
            try
            {
                _logger.LogInformation("Query request is comming: " + context.Message.ToJson());
                var serviceConfiguration = await _externalServiceConnector.ServiceConfigurationAsync(
                    "",
                    context.Message.ServiceCode,
                    context.Message.CategoryCode, context.Message.ProductCode);

                if (serviceConfiguration != null && serviceConfiguration.Count > 0)
                {
                    var serviceConfig = serviceConfiguration.OrderBy(c => c.Priority).First();
                    _logger.LogInformation("Query request is forwarding by provider: " + serviceConfig.ProviderCode);
                    var queryResult = await _billQueryClient.GetResponse<MessageResponseBase>(new
                    {
                        context.Message.ServiceCode,
                        context.Message.CategoryCode,
                        serviceConfig.ProviderCode,
                        ReceiverInfo = context.Message.QueryInputInfo,
                        TransRef = DateTime.Now.ToString("yyMMddHHmmssffff"),
                        context.Message.IsInvoice,
                        context.Message.CorrelationId,
                        Vendor = context.Message.ProductCode.Split('_')[0],
                        context.Message.ProductCode
                    }, CancellationToken.None, RequestTimeout.After(m: 5));

                    _logger.LogInformation("queryResult is: " + queryResult.Message.ToJson());
                    if (queryResult.Message.ResponseCode == ResponseCodeConst.Success)
                    {
                        await context.RespondAsync<MessageResponseBase>(new
                        {
                            queryResult.Message.ResponseCode,
                            queryResult.Message.ResponseMessage,
                            queryResult.Message.Payload
                        });
                    }
                    else
                    {
                        await context.RespondAsync<MessageResponseBase>(new
                        {
                            queryResult.Message.ResponseCode,
                            queryResult.Message.ResponseMessage
                        });
                    }
                }
                else
                {
                    await context.RespondAsync<MessageResponseBase>(new
                    {
                        ResponseCode = "00",
                        ResponseMessage = "Dịch vụ chưa được thiết lập. Vui lòng liên hệ CSKH để được hỗ trợ"
                    });
                }
            }
            catch (Exception e)
            {
                _logger.LogError($"BillQueryRequestCommand_Error:{e}");
                await context.RespondAsync<MessageResponseBase>(new
                {
                    ResponseCode = "00",
                    ResponseMessage = "Không thể truy vấn thông tin. Vui lòng thử lại sau"
                });
            }
        }
    }

    public class BillQueryRequestCommandConsumerDefinition :
        ConsumerDefinition<BillQueryRequestCommandConsumer>
    {
        public BillQueryRequestCommandConsumerDefinition()
        {
            ConcurrentMessageLimit = 256;

        }

        protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpointConfigurator,
            IConsumerConfigurator<BillQueryRequestCommandConsumer> consumerConfigurator)
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
