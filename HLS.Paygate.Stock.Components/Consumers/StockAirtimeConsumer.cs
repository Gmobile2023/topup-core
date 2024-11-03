using System;
using System.Threading.Tasks;
using HLS.Paygate.Gw.Model.Commands.Stock;
using HLS.Paygate.Gw.Model.Events.Stock;
using HLS.Paygate.Shared;
using HLS.Paygate.Stock.Contracts.Dtos;
using HLS.Paygate.Stock.Domains.BusinessServices;
using HLS.Paygate.Stock.Domains.Grains;
using MassTransit;
using Orleans;

namespace HLS.Paygate.Stock.Components.Consumers;

public class StockAirtimeConsumer : IConsumer<StockAirtimeInventoryCommand>, IConsumer<StockAirtimeImported>,
    IConsumer<StockAirtimeExported>
{
    private readonly ICardService _cardService;
    private readonly IClusterClient _clusterClient;

    public StockAirtimeConsumer(IClusterClient clusterClient, ICardService cardService)
    {
        _clusterClient = clusterClient;
        _cardService = cardService;
    }

    public async Task Consume(ConsumeContext<StockAirtimeExported> context)
    {
        var stockCode = context.Message.StockCode;
        if (string.IsNullOrEmpty(stockCode))
            throw new ArgumentNullException(nameof(stockCode));
        var providerCode = context.Message.ProviderCode;
        if (string.IsNullOrEmpty(providerCode))
            throw new ArgumentNullException(nameof(providerCode));
        var amount = context.Message.Amount;
        if (amount <= 0)
            throw new ArgumentNullException(nameof(amount));

        var stock = _clusterClient.GetGrain<IStockGrain>(string.Join("|", context.Message.StockCode,
            context.Message.ProviderCode));

        if (stock != null)
        {
            await stock.ExportAirtime(context.Message.Amount, context.Message.CorrelationId);

            var stockTrans = new StockTransDto
            {
                SrcStockCode = stockCode,
                ProviderCode = providerCode,
                Quantity = amount,
                CardValue = 1,
                CreatedDate = DateTime.Now,
                StockTransType = "EXPORT",
                TransRef = context.Message.TransRef
            };

            await _cardService.StockTransInsertAsync(stockTrans);
        }
    }

    public async Task Consume(ConsumeContext<StockAirtimeImported> context)
    {
        var stockCode = context.Message.StockCode;
        if (string.IsNullOrEmpty(stockCode))
            throw new ArgumentNullException(nameof(stockCode));
        var providerCode = context.Message.ProviderCode;
        if (string.IsNullOrEmpty(providerCode))
            throw new ArgumentNullException(nameof(providerCode));
        var amount = context.Message.Amount;
        if (amount <= 0)
            throw new ArgumentNullException(nameof(amount));

        var stock = _clusterClient.GetGrain<IStockGrain>(string.Join("|", stockCode,
            providerCode));

        if (stock != null)
        {
            await stock.ImportAirtime(amount, context.Message.CorrelationId);

            var stockTrans = new StockTransDto
            {
                DesStockCode = stockCode,
                ProviderCode = providerCode,
                Quantity = amount,
                CardValue = 1,
                CreatedDate = DateTime.Now,
                StockTransType = "IMPORT",
                TransRef = context.Message.TransRef
            };

            await _cardService.StockTransInsertAsync(stockTrans);
        }
    }

    public async Task Consume(ConsumeContext<StockAirtimeInventoryCommand> context)
    {
        var stock = _clusterClient.GetGrain<IStockGrain>(string.Join("|", context.Message.StockCode,
            context.Message.ProviderCode));

        if (stock != null)
        {
            var inventory = await stock.CheckAvailableInventory();

            await context.RespondAsync<MessageResponseBase>(new
            {
                ResponseCode = ResponseCodeConst.Success,
                ResponseMessage = "Get inventory success",
                Payload = inventory
            });
        }
        else
        {
            await context.RespondAsync<MessageResponseBase>(new
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Get inventory fail"
            });
        }
    }
}

public class StockAirtimeConsumerDefinition :
    ConsumerDefinition<StockAirtimeConsumer>
{
    public StockAirtimeConsumerDefinition()
    {
        ConcurrentMessageLimit = 10;
        // EndpointName = "stock";
    }
}