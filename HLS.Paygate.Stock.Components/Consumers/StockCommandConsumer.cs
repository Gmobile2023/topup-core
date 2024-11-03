using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HLS.Paygate.Gw.Model.Commands.Stock;
using HLS.Paygate.Gw.Model.Events.Stock;
using HLS.Paygate.Shared.Dtos;
using HLS.Paygate.Stock.Contracts.Events;
using HLS.Paygate.Stock.Domains.BusinessServices;
using HLS.Paygate.Stock.Domains.Grains;
using MassTransit;
using MassTransit.Definition;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using Orleans;
using ServiceStack; // using Hazelcast.Core;

namespace HLS.Paygate.Stock.Components.Consumers
{
    public class StockCommandConsumer : IConsumer<StockCardInventoryCommand>,
        IConsumer<StockCardExchangeCommand>,
        IConsumer<StockCardSaleCommand>,
        IConsumer<StockCardImportCommand>,
        IConsumer<StockAllocateCommand>,
        IConsumer<StockUnAllocateCommand>
    {
        private readonly IClusterClient _clusterClient;
        private readonly ILogger<StockCommandConsumer> _logger;

        public StockCommandConsumer(IClusterClient clusterClient, ILogger<StockCommandConsumer> logger)
        {
            _clusterClient = clusterClient;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<StockAllocateCommand> context)
        {
            _logger.LogInformation("StockAllocateCommand recevied: " + BsonExtensionMethods.ToJson(context.Message));
            var stock = _clusterClient.GetGrain<IStockGrain>(string.Join("|", context.Message.StockCode.Trim(),
                context.Message.ProductCode));

            await stock.Allocate(context.Message.Amount, context.Message.CorrelationId);

            await context.RespondAsync<StockAllocated>(new
            {
                context.Message.AllocationId,
                context.Message.TransCode
            });
        }

        public async Task Consume(ConsumeContext<StockCardExchangeCommand> context)
        {
            _logger.LogInformation("StockExchangeCommand recevied: " + BsonExtensionMethods.ToJson(context.Message));
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
                _logger.LogInformation(
                    $"StockExchangeCommand AvailableInventory: inventory:{inventory}; quantity:{context.Message.Amount};");
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
                    _logger.LogInformation($"StockExchangeCommand ExportCard: inventory:{inventory}; quantity:{context.Message.Amount};");
                    await context.RespondAsync<CardStockCommandRejected>(new
                    {
                        Id = context.Message.CorrelationId,
                        Timestamp = DateTime.Now,
                        Reason = "Card is not enough"
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

        public async Task Consume(ConsumeContext<StockCardImportCommand> context)
        {
            _logger.LogInformation("StockImportCommand recevied: " + BsonExtensionMethods.ToJson(context.Message));
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
                .ImportInitCard(context.Message.CardValue, context.Message.Amount, id);
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

        public async Task Consume(ConsumeContext<StockCardInventoryCommand> context)
        {
            _logger.LogInformation("StockInventoryCommand recevied: " + BsonExtensionMethods.ToJson(context.Message));
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

        public async Task Consume(ConsumeContext<StockCardSaleCommand> context)
        {
            _logger.LogInformation("StockSaleCommand recevied: " + BsonExtensionMethods.ToJson(context.Message));
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
                await context.RespondAsync<CardStockCommandSubmitted<List<CardRequestResponseDto>>>(new
                {
                    Id = context.Message.CorrelationId,
                    Timestamp = DateTime.Now,
                    Payload = cards.ConvertTo<List<CardRequestResponseDto>>()
                });
                _logger.LogInformation($"SALE Inventory is success: {cards.Count}");
            }

            await context.Publish<CardStockCommandDone>(new
            {
                Id = context.Message.CorrelationId,
                Timestamp = DateTime.Now
            });
        }

        public async Task Consume(ConsumeContext<StockUnAllocateCommand> context)
        {
            _logger.LogInformation("StockUnAllocateCommand recevied: " + BsonExtensionMethods.ToJson(context.Message));
            var stock = _clusterClient.GetGrain<IStockGrain>(string.Join("|", context.Message.StockCode.Trim(),
                context.Message.ProductCode));

            await stock.UnAllocate(null, context.Message.Amount, context.Message.CorrelationId);
        }
    }

    public class CardStockCommandConsumerDefinition :
        ConsumerDefinition<StockCommandConsumer>
    {
        public CardStockCommandConsumerDefinition()
        {
            ConcurrentMessageLimit = 10;
            // EndpointName = "stock";
        }
    }
}
