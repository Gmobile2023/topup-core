using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HLS.Paygate.Gw.Model.Events;
using HLS.Paygate.Shared;
using HLS.Paygate.Shared.Contracts.Events.Report;
using HLS.Paygate.Stock.Contracts.Dtos;
using HLS.Paygate.Stock.Contracts.Enums;
using HLS.Paygate.Stock.Contracts.Events;
using HLS.Paygate.Stock.Domains.BusinessServices;
using HLS.Paygate.Stock.Domains.Grains;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using Orleans;
using Orleans.Providers;
using Paygate.Contracts.Commands.Commons;

// [assembly: GenerateSerializer(typeof(StockDto))]

namespace HLS.Paygate.StockGrains;

[StorageProvider(ProviderName = "stock-grains-storage")]
public class StockGrain : Grain<StockDto>, IStockGrain
{
    // private readonly ICardService _cardService;
    private readonly IBus _bus;
    private readonly ICardStockService _cardStockService;

    //private readonly Logger _logger = LogManager.GetLogger("StockGrain");
    private readonly ILogger<StockGrain> _logger;
    private readonly int _minimumInventoryLimit;

    public StockGrain(ICardStockService cardStockService, IBus bus, // ICardService cardService,
        IConfiguration configuration, ILogger<StockGrain> logger)
    {
        _cardStockService = cardStockService;
        _bus = bus;
        _logger = logger;
        // _cardService = cardService;
        _minimumInventoryLimit = int.Parse(configuration["StockConfig:MinimumInventoryLimit"]);
    }

    async Task<List<CardDto>> IStockGrain.ExportCard(int quantity, Guid correlationId, string batchCode)
    {
        try
        {
            // await InitState();

            if (State.Status == 1)
            {
                // if (State.Inventory < State.MinimumInventoryLimit)
                //     return null;
                if (State.Inventory < quantity)
                    return null;

                var list = await _cardStockService.CardExportForExchangeAsync(quantity, CardStatus.Active,
                    State.KeyCode,
                    State.ItemValue,
                    State.StockCode, batchCode);

                if (list == null)
                    return null;

                await _cardStockService.StockUpdateInventoryAsync(State.StockCode, State.KeyCode, -quantity);
                State.Inventory -= quantity;
                await WriteStateAsync();

                //var proCode = State.KeyCode.Split("_");
                var providerItems = (from x in list
                    group x by new { x.BatchCode, x.ProviderCode }
                    into g
                    select new ProviderCardStockItem
                    {
                        ProviderCode = g.Key.ProviderCode,
                        BatchCode = g.Key.BatchCode,
                        Quantity = g.Count()
                    }).ToList();
                await StockInventoryUpdate(new ReportCardStockMessage
                {
                    Id = correlationId,
                    Inventory = State.Inventory,
                    Vendor = State.VendorCode,
                    ProductCode = State.KeyCode,
                    StockType = State.StockType,
                    Serial = list.Select(x => x.Serial).ToJson(),
                    CardValue = State.ItemValue,
                    StockCode = State.StockCode,
                    Decrease = quantity,
                    InventoryType = CardTransType.Exchange,
                    ProviderItem = providerItems,
                });
                return list;
            }

            return null;
        }
        catch (Exception e)
        {
            _logger.LogError("ExportCard error: " + e);
            return null;
        }
    }

    async Task<bool> IStockGrain.ImportCard(List<CardDto> cards, Guid correlationId)
    {
        try
        {
            _logger.LogInformation($"{cards[0].BatchCode} ImportCard process");
            // await InitState();

            if (State.Status == 1)
            {
                var rs = await _cardStockService.CardImportFromExchangeAsync(cards, CardStatus.Active,
                    State.StockCode);
                if (rs == false)
                {
                    _logger.LogInformation($"{cards[0].BatchCode} ImportCard fail");
                    return false;
                }

                _logger.LogInformation($"{cards[0].BatchCode} ImportCard success");
                State.Inventory += cards.Count;
                await _cardStockService.StockUpdateInventoryAsync(State.StockCode, State.KeyCode, cards.Count);
                await WriteStateAsync();
                _logger.LogInformation($"{cards[0].BatchCode} ImportCard update inventory success");
                var providerItems = (from x in cards
                    group x by new { x.BatchCode, x.ProviderCode }
                    into g
                    select new ProviderCardStockItem
                    {
                        ProviderCode = g.Key.ProviderCode,
                        BatchCode = g.Key.BatchCode,
                        Quantity = g.Count()
                    }).ToList();

                await StockInventoryUpdate(new ReportCardStockMessage
                {
                    Id = correlationId,
                    Inventory = State.Inventory,
                    Vendor = State.VendorCode,
                    Serial = cards.Select(x => x.Serial).ToJson(),
                    CardValue = State.ItemValue,
                    StockCode = State.StockCode,
                    Increase = cards.Count,
                    InventoryType = CardTransType.Exchange,
                    ProductCode = State.KeyCode,
                    StockType = State.StockType,
                    ProviderItem = providerItems
                });
                return true;
            }

            return false;
        }
        catch (Exception e)
        {
            _logger.LogError("ImportCard error: " + e);
            return false;
        }
    }

    public async Task ImportAirtime(int amount, Guid correlationId)
    {
        if (State.Status == 1)
        {
            var result = await _cardStockService.StockUpdateInventoryAsync(State.StockCode, State.KeyCode, amount);
            if (result)
            {
                State.Inventory += amount;
                await WriteStateAsync();
            }
        }
    }

    public async Task ExportAirtime(int amount, Guid correlationId)
    {
        if (State.Status == 1)
        {
            var result = await _cardStockService.StockUpdateInventoryAsync(State.StockCode, State.KeyCode, -amount);
            if (result)
            {
                State.Inventory -= amount;
                await WriteStateAsync();
            }
        }
    }

    public async Task<bool> UpdateInventory(int inventory)
    {
        var result = await _cardStockService.StockSetInventoryAsync(State.StockCode, State.KeyCode, inventory);
        if (!result) return false;
        State.Inventory = inventory;
        await WriteStateAsync();
        return true;

    }

    public async Task<Tuple<bool, int>> ImportInitCard(int itemValue, int quantity, Guid correlationId)
    {
        try
        {
            //Hàm này tăng tồn kho khi thêm mới thẻ
            // await InitState();
            if (State.Status == 1)
            {
                var result =
                    await _cardStockService.StockUpdateInventoryAsync(State.StockCode, State.KeyCode, quantity);
                if (result)
                {
                    State.ItemValue = itemValue;
                    State.Inventory += quantity;
                    await WriteStateAsync();

                    return Tuple.Create(true, State.Inventory);
                }
            }

            return Tuple.Create(false, 0);
        }
        catch (Exception e)
        {
            _logger.LogError("ImportInitCard error: " + e);
        }

        return Tuple.Create(false, 0);
    }

    public async Task<int> CheckAvailableInventory()
    {
        _logger.LogInformation($"CheckAvailableInventory: {State.Inventory.ToJson()}");
        return await Task.FromResult(State.Inventory);
    }

    async Task<List<CardDto>> IStockGrain.Sale(int amount, Guid correlationId, string transCode = null)
    {
        var lstCard = new List<CardDto>();
        try
        {
            if (State.Status == 1)
            {
                if (State.Inventory < amount)
                    return null;

                var list = await _cardStockService.CardExportForSaleAsync(amount, State.KeyCode,
                    State.ItemValue,
                    State.StockCode, transCode);

                if (list == null)
                    return null;

                lstCard = list;

                _logger.LogInformation(
                    $"{transCode} CardExportForSaleAsync return: {list.Select(x => x.Serial).ToJson()}");

                var updateInventory =
                    await _cardStockService.StockUpdateInventoryAsync(State.StockCode, State.KeyCode, -amount);

                if (updateInventory)
                {
                    State.Inventory -= amount;
                    await WriteStateAsync();

                    try
                    {
                        var providerItems = (from x in lstCard
                            group x by new { x.BatchCode, x.ProviderCode }
                            into g
                            select new ProviderCardStockItem
                            {
                                ProviderCode = g.Key.ProviderCode,
                                BatchCode = g.Key.BatchCode,
                                Quantity = g.Count()
                            }).ToList();

                        await StockInventoryUpdate(new ReportCardStockMessage
                        {
                            Id = correlationId,
                            ProviderItem = providerItems,
                            Inventory = State.Inventory,
                            Vendor = State.VendorCode,
                            StockType = State.StockType,
                            ProductCode = State.KeyCode,
                            Serial = list.Select(x => x.Serial).ToJson(),
                            CardValue = State.ItemValue,
                            StockCode = State.StockCode,
                            Decrease = amount,
                            InventoryType = CardTransType.Sale
                        });
                        var min = State.MinimumInventoryLimit > 0
                            ? State.MinimumInventoryLimit
                            : _minimumInventoryLimit;
                        if (State.Inventory < min)
                            try
                            {
                                await _bus.Publish<StockInventoryNotificationCommand>(new
                                {
                                    State.Inventory,
                                    State.VendorCode,
                                    CardValue = (int)State.ItemValue,
                                    State.StockCode,
                                    NotifiType = CardStockNotificationType.MinimumInventoryLimit,
                                    ProductCode = State.KeyCode,
                                    TimeStamp = DateTime.Now,
                                    CorrelationId = Guid.NewGuid()
                                });
                            }
                            catch (Exception e)
                            {
                                _logger.LogError($"{transCode} StockInventoryNotificationCommand error: {e}");
                            }
                    }
                    catch (Exception e)
                    {
                        _logger.LogError($"{transCode} Event error: {e}");
                    }

                    return list;
                }

                throw new Exception($"{transCode} Fail to update inventory");
            }

            return null;
        }
        catch (Exception e)
        {
            var lstSerical = lstCard?.Select(x => x.Serial).ToJson();
            _logger.LogError(
                $"{transCode} ExportCard for sale error - Serial: {lstSerical} - Product:{State.KeyCode} - Stock:{State.StockCode} - exeption:{e}");

            await _bus.Publish<CardExportRollbackEvent>(new
            {
                State.StockCode,
                ProductCode = State.KeyCode,
                Cards = lstCard,
                ErrorDetail = e.Message
            });

            return null;
        }
    }

    public async Task Allocate(int quantity, Guid correlationId)
    {
        if (State.Status == 1)
        {
            var result = await _cardStockService.StockUpdateInventoryAsync(State.StockCode, State.KeyCode, -quantity);
            if (result)
            {
                State.Inventory -= quantity;
                await WriteStateAsync();
            }
        }
    }

    public async Task UnAllocate(List<ProviderCardStockItem> providerItem, int quantity, Guid correlationId)
    {
        try
        {
            //Hàm này tăng tồn kho khi thêm mới thẻ
            // await InitState();
            if (State.Status == 1)
            {
                var result =
                    await _cardStockService.StockUpdateInventoryAsync(State.StockCode, State.KeyCode, quantity);
                if (result)
                {
                    State.Inventory += quantity;
                    await WriteStateAsync();
                    // pub sau khi import ton kho
                    await StockInventoryUpdate(new ReportCardStockMessage
                    {
                        Id = correlationId,
                        Inventory = State.Inventory,
                        Vendor = State.VendorCode,
                        ProductCode = State.KeyCode,
                        StockType = State.StockType,
                        Serial = "",
                        CardValue = State.ItemValue,
                        StockCode = State.StockCode,
                        Increase = quantity,
                        InventoryType = CardTransType.Inventory,
                        ProviderItem = providerItem
                    }).ConfigureAwait(false);
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError("ImportInitCard error: " + e);
        }
    }

    async Task IStockGrain.InitStock(decimal cardValue)
    {
        try
        {
            // await InitState();
            if (State.Status == 1)
            {
                State.ItemValue = cardValue;
                await WriteStateAsync();
            }
        }
        catch (Exception e)
        {
            _logger.LogError("InitStock error: " + e);
        }
    }

    private string ServiceGet(string prodCode)
    {
        var s = prodCode.Contains("PINCODE") || prodCode.Contains("PIN_CODE") || prodCode.Contains("CODE")
            ? "PIN_CODE"
            : prodCode.Contains("PINGAME") || prodCode.Contains("PIN_GAME") || prodCode.Contains("GAME")
                ? "PIN_GAME"
                : prodCode.Contains("PINDATA") || prodCode.Contains("PIN_DATA") || prodCode.Contains("DATA")
                    ? "PIN_DATA"
                    : "";
        return s;
    }

    private string CategoryGet(string prodCode)
    {
        var c = "";
        var pCode = prodCode.Split("_").ToList();
        if (!pCode.Any() || pCode.Count == 1)
        {
            c = prodCode;
        }
        else
        {
            pCode.RemoveAt(pCode.Count - 1);
            c = string.Join("_", pCode);
        }

        return c;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("On activate: " + this.GetPrimaryKeyString());
        var keys = this.GetPrimaryKeyString().Split('|');
        var stockCode = keys[0];
        var keyCode = keys[1];
        var stock = await _cardStockService.StockGetAsync(stockCode, keyCode);

        if (stock != null)
        {
            State = stock;
        }
        else
        {
            State.Status = 1;
            State.StockCode = stockCode;
            State.KeyCode = keyCode;

            // PINCODE => keyCode = vendor_cardvalue;
            // AIRTIME => keyCode = providerCode
            var proCode = keyCode.Split('_');
            var lastProCode = proCode[^1];
            var isCardValue = int.TryParse(lastProCode, out var cardValue);
            if (isCardValue)
            {
                State.StockType = "PINCODE";
                if (State.ItemValue == 0)
                    State.ItemValue = cardValue * 1000;
                // add info
                State.ServiceCode = ServiceGet(keyCode);
                State.CategoryCode = CategoryGet(keyCode);
            }
            else
            {
                State.StockType = "AIRTIME";
            }

            await _cardStockService.StockInsertAsync(State);
        }

        await WriteStateAsync();
        await base.OnActivateAsync(cancellationToken);
    }

    private async Task StockInventoryUpdate(ReportCardStockMessage message)
    {
        try
        {
            //_logger.LogInformation($"NotifyInventoryUpdate request: {message.ToJson()}");

            await _bus.Publish<ReportCardStockMessage>(new
            {
                message.ProviderItem,
                message.StockCode,
                message.Id,
                message.Inventory,
                message.Serial,
                message.Vendor,
                message.CardValue,
                message.ProductCode,
                message.CategoryCode,
                message.StockType,
                message.InventoryType,
                message.Increase,
                message.Decrease,
                CreatedDate = DateTime.Now,
                QuantityAfter = message.Inventory,
                QuantityBefore = message.Increase > 0
                    ? message.Inventory - message.Increase
                    : message.Inventory + message.Decrease
            });
        }
        catch (Exception e)
        {
            _logger.LogError("NotifyInventoryUpdate error" + e);
        }
    }
}