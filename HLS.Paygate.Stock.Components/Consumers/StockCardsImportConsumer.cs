using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using HLS.Paygate.Gw.Model.Commands;
using HLS.Paygate.Gw.Model.Commands.Stock;
using HLS.Paygate.Gw.Model.Events;
using HLS.Paygate.Shared;
using HLS.Paygate.Stock.Contracts.Dtos;
using HLS.Paygate.Stock.Domains.BusinessServices;
using HLS.Paygate.Stock.Domains.Grains;
using MassTransit;
using MassTransit.Definition;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using NLog;
using Orleans;
using ServiceStack; // using Hazelcast.Core;

namespace HLS.Paygate.Stock.Components.Consumers
{
    public class StockCardsImportConsumer : IConsumer<StockCardsImportCommand>
    {
        private readonly ICardService _cardService;
        private readonly IClusterClient _clusterClient;

        //private readonly ILogger<StockCardsImportConsumer> _logger; 
        private readonly Logger _logger = LogManager.GetLogger("CardStockCommandConsumer");

        // private IHazelcastInstance _hazelcastClient;

        public StockCardsImportConsumer(IClusterClient clusterClient, ICardService cardService) //, ILogger<StockCardsImportConsumer> logger)
        {
            _clusterClient = clusterClient;
            _cardService = cardService;
            //_logger = logger;
        }


        /// <summary>
        /// Import list card cùng mệnh giá, cùng productCode
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task Consume(ConsumeContext<StockCardsImportCommand> context)
        {
            _logger.Info("StockCardsImportCommand: " + BsonExtensionMethods.ToJson(context.Message));
            var cards = context.Message.CardItems.Select(x =>
            {
                var isDate = DateTime.TryParseExact(x.ExpiredDate, "dd/MM/yyyy", null, DateTimeStyles.None, out var expiredDate);
                return new CardSimpleDto()
                {
                    ProductCode = context.Message.ProductCode,
                    ExpiredDate = isDate ? expiredDate : DateTime.Now.AddYears(3),
                    CardValue = x.CardValue,
                    CardCode = x.CardCode,
                    Serial = x.Serial
                };
            }).ToList();
            var result = await _cardService.CardsInsertAsync(context.Message.BatchCode, cards);
            if (result.ResponseCode == "01")
            {
                var stock = _clusterClient.GetGrain<IStockGrain>(string.Join("|", StockCodeConst.STOCK_TEMP,
                    context.Message.ProductCode));

                var providerItems =  result.Payload.ConvertTo<List<ProviderCardStockItem>>();

                await stock.UnAllocate(providerItems, context.Message.CardItems.Count, context.Message.CorrelationId);
                _logger.Info($"StockCardsImportCommand success: {cards.Count}");
            }
            else
            {
                _logger.Error("StockCardsImportCommand fail import by BatchCode: " + context.Message.BatchCode + ", count: " + cards.Count());
            }
            await context.RespondAsync(result);
        }
    }

    public class StockCardsImportConsumerDefinition :
        ConsumerDefinition<StockCardsImportConsumer>
    {
        public StockCardsImportConsumerDefinition()
        {
            ConcurrentMessageLimit = 10;
            // EndpointName = "stock";
        }
    }
}