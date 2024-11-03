using System;
using System.Threading.Tasks;
using GMB.Topup.Gw.Model.Commands.TopupGw;
using GMB.Topup.TopupGw.Components.Connectors;
using GMB.Topup.Shared;
using GMB.Topup.Shared.Dtos;
using GMB.Topup.TopupGw.Contacts.Dtos;
using MassTransit;
using Microsoft.Extensions.Logging;
using ServiceStack;

namespace GMB.Topup.TopupGw.Components.Consumers;

public class BillQueryConsumer : IConsumer<BillQueryCommand>
{
    private readonly ILogger<BillQueryConsumer> _logger;

    // private readonly GatewayConnectorFactory _connectorFactory;
    private IGatewayConnector _gatewayConnector;

    public BillQueryConsumer(ILogger<BillQueryConsumer> logger)
    {
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<BillQueryCommand> context)
    {
        try
        {
            _logger.LogInformation($"{context.Message.TransRef} Bill query: " + context.Message.ToJson());

            var providerCode = context.Message.ProviderCode;
            if (string.IsNullOrEmpty(providerCode))
                throw new ArgumentNullException(nameof(providerCode));

            _gatewayConnector = HostContext.Container.ResolveNamed<IGatewayConnector>(providerCode.Split('-')[0]);
            _logger.LogInformation($"GatewayConnector {_gatewayConnector.ToJson()} billQuery:{providerCode}");
            var result = await _gatewayConnector.QueryAsync(new PayBillRequestLogDto
            {
                TransCode = DateTime.Now.ToString("yyMMddHHmmssffff"),
                ReceiverInfo = context.Message.ReceiverInfo,
                IsInvoice = context.Message.IsInvoice,
                Vendor = context.Message.Vendor,
                ProviderCode = context.Message.ProviderCode,
                ProductCode = context.Message.ProductCode
            });

            _logger.LogInformation("Bill query: " + context.Message.ToJson());

            //Parse kiểu Viettel
            if (context.Message.ProductCode.StartsWith("VTE") || context.Message.ProductCode.StartsWith("VMS") &&
                providerCode == ProviderConst.MTC)
                result.Results = new InvoiceResultDto
                {
                    Amount = result.Results.Amount
                };

            await context.RespondAsync(result);
        }
        catch (Exception)
        {
            await context.RespondAsync(new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.ResponseCode_BillException,
                ResponseMessage = "Không thể truy vấn thông tin. Vui lòng thử lại sau"
            });
        }
    }
}

public class BillQueryConsumerDefinition :
    ConsumerDefinition<BillQueryConsumer>
{
    private readonly IServiceProvider _serviceProvider;

    public BillQueryConsumerDefinition(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        ConcurrentMessageLimit = 256;
    }

    protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<BillQueryConsumer> consumerConfigurator)
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