using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HLS.Paygate.Gw.Model.Commands;
using HLS.Paygate.Gw.Model.Events.Stock;
using HLS.Paygate.Stock.Contracts.Dtos;
using HLS.Paygate.Stock.Contracts.Events;
using HLS.Paygate.Stock.Domains.Grains;
using MassTransit;
using MassTransit.Definition;
using MongoDB.Bson;
using NLog;
using Orleans;

namespace HLS.Paygate.TopupGw.Components.Consumers
{
    public class StockCommandConsumer : IConsumer<StockInventoryCommand>, IConsumer<StockExchangeCommand>,
        IConsumer<StockSaleCommand>,
        IConsumer<StockImportCommand>,
        IConsumer<StockAllocateCommand>,
        IConsumer<StockUnAllocateCommand>
    {
        // private readonly ICardRequestService _cardRequestService;
        private readonly IClusterClient _clusterClient;
        private readonly Logger _logger = LogManager.GetLogger("CardStockCommandConsumer");

        // private IHazelcastInstance _hazelcastClient;

        public StockCommandConsumer(IClusterClient clusterClient)
        {
            _clusterClient = clusterClient;
            // _cardRequestService = cardRequestService;
            // _hazelcastClient = hazelcastClient;
        }

        public async Task Consume(ConsumeContext<StockExchangeCommand> context)
        {
            _logger.LogInformation("StockExchangeCommand recevied: " + context.Message.ToJson());
            var srcStock = _clusterClient.GetGrain<IStockGrain>(string.Join("|", context.Message.SrcStockCode.Trim(),
                context.Message.ProductCode));
            await context.Publish<CardStockCommandReceived>(new
            {
                Id = context.Message.CorrelationId,
                Timestamp = DateTime.Now,
                CardStockCommand = context.Message
            });
            var inventory = await srcStock.CheckAvailableInventory();

            if (inventory < context.Message.Amount)
            {
                await context.RespondAsync<CardStockCommandRejected>(new
                {
                    Id = context.Message.CorrelationId,
                    Timestamp = DateTime.Now,
                    Reason = "Inventory is not enough"
                });
            }
            else
            {
                var cards = await srcStock.ExportCard(context.Message.Amount, context.Message.CorrelationId,
                    context.Message.BatchCode);

                if (cards == null)
                {
                    await context.RespondAsync<CardStockCommandRejected>(new
                    {
                        Id = context.Message.CorrelationId,
                        Timestamp = DateTime.Now,
                        Reason = "Inventory is not enough"
                    });
                }
                else
                {
                    _logger.LogInformation($"ExportCard success: {cards.Count}");
                    var id = Guid.NewGuid();
                    await context.Publish<CardStockCommandReceived>(new
                    {
                        Id = id,
                        Timestamp = DateTime.Now,
                        CardStockCommand = context.Message
                    });
                    _logger.LogInformation("Processing import cards");
                    var result = await _clusterClient
                        .GetGrain<IStockGrain>(context.Message.DesStockCode + "|" + context.Message.ProductCode)
                        .ImportCard(cards, id);
                    if (result)
                    {
                        await context.RespondAsync<CardStockCommandSubmitted<string>>(new
                        {
                            context.Message.CorrelationId,
                            Timestamp = DateTime.Now
                        });
                        _logger.LogInformation($"ImportCard success: {cards.Count}");
                    }
                    else
                    {
                        await context.RespondAsync<CardStockCommandRejected>(new
                        {
                            Id = context.Message.CorrelationId,
                            Timestamp = DateTime.Now,
                            Reason = "Can not inventory to des stock: " + context.Message.DesStockCode
                        });
                        _logger.LogInformation("ImportCard fail " + context.Message.DesStockCode);
                    }
                }
            }

            await context.Publish<CardStockCommandDone>(new
            {
                Id = context.Message.CorrelationId,
                Timestamp = DateTime.Now
            });
        }

        public async Task Consume(ConsumeContext<StockImportCommand> context)
        {
            var id = Guid.NewGuid();
            await context.Publish<CardStockCommandReceived>(new
            {
                Id = id,
                Timestamp = DateTime.Now,
                CardStockCommand = context.Message
            });
            //Chỗ này dùng client mới. cho những kho chưa được tạo
            var result = await _clusterClient
                .GetGrain<IStockGrain>(context.Message.StockCode + "|" + context.Message.ProductCode)
                .ImportInitCard(context.Message.CardValue, context.Message.Amount , id);
            if (result.Item1)
            {
                await context.RespondAsync<CardStockCommandSubmitted<int>>(new
                {
                    Id = context.Message.CorrelationId,
                    Timestamp = DateTime.Now,
                    Payload = result.Item2
                });
                _logger.LogInformation("INIT_INVENTORY success");
            }
            else
            {
                await context.RespondAsync<CardStockCommandRejected>(new
                {
                    Id = context.Message.CorrelationId,
                    Timestamp = DateTime.Now,
                    Reason = "Can not init inventory"
                });
                _logger.LogInformation("INIT_INVENTORY fail");
            }

            await context.Publish<CardStockCommandDone>(new
            {
                Id = context.Message.CorrelationId,
                Timestamp = DateTime.Now
            });
        }

        public async Task Consume(ConsumeContext<StockInventoryCommand> context)
        {
            var stock = _clusterClient.GetGrain<IStockGrain>(string.Join("|", context.Message.StockCode.Trim(),
                context.Message.ProductCode));
            var inventory = await stock.CheckAvailableInventory();

            await context.RespondAsync<CardStockCommandSubmitted<int>>(new
            {
                Id = context.Message.CorrelationId,
                Timestamp = DateTime.Now,
                Payload = inventory
            });
        }

        public async Task Consume(ConsumeContext<StockSaleCommand> context)
        {
            _logger.LogInformation("StockExchangeCommand recevied: " + context.Message.ToJson());
            var stock = _clusterClient.GetGrain<IStockGrain>(string.Join("|", context.Message.StockCode.Trim(),
                context.Message.ProductCode));
            await context.Publish<CardStockCommandReceived>(new
            {
                Id = context.Message.CorrelationId,
                Timestamp = DateTime.Now,
                CardStockCommand = context.Message
            });
            var cards = await stock.Sale(context.Message.Amount, context.Message.CorrelationId);

            if (cards == null)
            {
                await context.RespondAsync<CardStockCommandRejected>(new
                {
                    Id = context.Message.CorrelationId,
                    Timestamp = DateTime.Now,
                    Reason = "Inventory is not enough"
                });
                _logger.LogInformation("SALE Inventory is not enough");
            }
            else
            {
                await context.RespondAsync<CardStockCommandSubmitted<List<CardDto>>>(new
                {
                    Id = context.Message.CorrelationId,
                    Timestamp = DateTime.Now,
                    Payload = cards
                });
                _logger.LogInformation($"SALE Inventory is success: {cards.Count}");
            }

            await context.Publish<CardStockCommandDone>(new
            {
                Id = context.Message.CorrelationId,
                Timestamp = DateTime.Now
            });
        }

        public async Task Consume(ConsumeContext<StockAllocateCommand> context)
        {
            _logger.LogInformation("Stock Allocate recevied: " + context.Message.ToJson());
            var stock = _clusterClient.GetGrain<IStockGrain>(string.Join("|", context.Message.StockCode.Trim(),
                context.Message.ProductCode));
            
            await stock.Allocate(context.Message.Amount, context.Message.CorrelationId);
            
            await context.RespondAsync<StockAllocated>(new
            {
                context.Message.AllocationId,
                context.Message.TransCode
            });
        }

        public async Task Consume(ConsumeContext<StockUnAllocateCommand> context)
        {
            _logger.LogInformation("Stock UnAllocate recevied: " + context.Message.ToJson());
            var stock = _clusterClient.GetGrain<IStockGrain>(string.Join("|", context.Message.StockCode.Trim(),
                context.Message.ProductCode));

            await stock.UnAllocate(context.Message.Amount, context.Message.CorrelationId);
        }
    }

    public class CardStockCommandConsumerDefinition :
        ConsumerDefinition<StockCommandConsumer>
    {
        public CardStockCommandConsumerDefinition()
        {
            ConcurrentMessageLimit = 10;
            EndpointName = "stock";
        }
    }
}
