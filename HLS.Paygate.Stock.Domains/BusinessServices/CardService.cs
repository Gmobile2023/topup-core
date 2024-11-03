using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using HLS.Paygate.Shared;
using HLS.Paygate.Shared.Contracts.Events.Report;
using HLS.Paygate.Shared.Helpers;
using HLS.Paygate.Shared.Utils;
using HLS.Paygate.Stock.Contracts.ApiRequests;
using HLS.Paygate.Stock.Contracts.Dtos;
using HLS.Paygate.Stock.Contracts.Enums;
using HLS.Paygate.Stock.Domains.Entities;
using HLS.Paygate.Stock.Domains.Repositories;
using Microsoft.Extensions.Logging;
using ServiceStack;
using ServiceStack.MiniProfiler;

namespace HLS.Paygate.Stock.Domains.BusinessServices;

public class CardService : BusinessServiceBase, ICardService
{
    private readonly ICardMongoRepository _cardMongoRepository;
    private readonly IDateTimeHelper _dateTimeHelper;

    private readonly ILogger<CardService> _logger;

    public CardService(ICardMongoRepository cardMongoRepository,
        IDateTimeHelper dateTimeHelper, ILogger<CardService> logger)
    {
        _cardMongoRepository = cardMongoRepository;
        _dateTimeHelper = dateTimeHelper;
        _logger = logger;
    }

    public async Task<bool> CardUpdateStatus(Guid id, CardStatus cardStatus)
    {
        var card = await _cardMongoRepository.GetByIdAsync<Card, Guid>(id);
        card.Status = cardStatus;
        try
        {
            await _cardMongoRepository.UpdateOneAsync(card);
            return true;
        }
        catch (Exception e)
        {
            _logger.LogError("Update card error: " + e.Message);
            return false;
        }
    }

    public async Task<bool> CardUpdateAsync(CardUpdateRequest cardUpdateRequest)
    {
        var card = await _cardMongoRepository.GetByIdAsync<Card, Guid>(cardUpdateRequest.Id);

        if (!string.IsNullOrEmpty(cardUpdateRequest.Serial) && card.Serial != cardUpdateRequest.Serial)
            card.Serial = cardUpdateRequest.Serial;
        var encryptedCode = cardUpdateRequest.CardCode.EncryptTripDes();

        if (!string.IsNullOrEmpty(cardUpdateRequest.CardCode) && card.CardCode != encryptedCode)
            card.CardCode = encryptedCode;
        if (cardUpdateRequest.ExpiredDate != DateTime.MinValue && cardUpdateRequest.ExpiredDate != card.ExpiredDate)
            card.ExpiredDate = cardUpdateRequest.ExpiredDate;

        try
        {
            await _cardMongoRepository.UpdateOneAsync(card);
            return true;
        }
        catch (Exception e)
        {
            _logger.LogError("Card update error: " + e.Message);

            if (e.Message.Contains("dup key"))
                throw new PaygateException("109", "Card is already exist");

            throw new PaygateException("00");
        }
    }

    public async Task<CardDto> CardGetAsync(Guid id, string serial, string productCode)
    {
        Card card = null;
        if (id != Guid.Empty) card = await _cardMongoRepository.GetByIdAsync<Card>(id);

        if (!string.IsNullOrEmpty(serial))
            card = await _cardMongoRepository.GetOneAsync<Card>(p =>
                p.ProductCode == productCode && p.Serial == serial);

        return AppendInfo(card);
    }

    public async Task<CardDto> CardGetWithVendorAsync(Guid id, string serial, string vendor)
    {
        Card card = null;
        if (id != Guid.Empty) card = await _cardMongoRepository.GetByIdAsync<Card>(id);

        if (!string.IsNullOrEmpty(serial))
            card = await _cardMongoRepository.GetOneAsync<Card>(p =>
                p.ProductCode.StartsWith(vendor) && p.Serial == serial);

        return AppendInfo(card);
    }

    public async Task<CardDto> CardGetByCodeAsync(string code, string productCode)
    {
        var encrypted = code.EncryptTripDes();
        var card = await _cardMongoRepository.GetOneAsync<Card>(p =>
            p.ProductCode == productCode && p.CardCode == encrypted);

        return card?.ConvertTo<CardDto>();
    }


    public async Task<MessagePagedResponseBase> CardGetListAsync(CardGetList cardGetList)
    {
        try
        {
            Expression<Func<Card, bool>> query = p => true;

            if (!string.IsNullOrEmpty(cardGetList.Filter))
            {
                Expression<Func<Card, bool>> newQuery = p => p.Serial.Contains(cardGetList.Filter)
                                                             || p.BatchCode.Contains(cardGetList.Filter)
                                                             || p.ProviderCode.Contains(cardGetList.Filter)
                                                             || p.ServiceCode.Contains(cardGetList.Filter)
                                                             || p.CategoryCode.Contains(cardGetList.Filter)
                                                             || p.ProductCode.Contains(cardGetList.Filter)
                                                             || p.StockCode.Contains(cardGetList.Filter);
                query = query.And(newQuery);
            }

            if (!string.IsNullOrEmpty(cardGetList.StockCode))
            {
                Expression<Func<Card, bool>> newQuery = p => p.StockCode == cardGetList.StockCode;
                query = query.And(newQuery);
            }

            if (!string.IsNullOrEmpty(cardGetList.ProviderCode))
            {
                Expression<Func<Card, bool>> newQuery = p => p.ProviderCode.Contains(cardGetList.ProviderCode);
                query = query.And(newQuery);
            }

            if (!string.IsNullOrEmpty(cardGetList.ServiceCode))
            {
                Expression<Func<Card, bool>> newQuery = p => p.ServiceCode.Contains(cardGetList.ServiceCode);
                query = query.And(newQuery);
            }

            if (!string.IsNullOrEmpty(cardGetList.CategoryCode))
            {
                Expression<Func<Card, bool>> newQuery = p => p.CategoryCode.Contains(cardGetList.CategoryCode);
                query = query.And(newQuery);
            }

            if (!string.IsNullOrEmpty(cardGetList.ProductCode))
            {
                Expression<Func<Card, bool>> newQuery = p => p.ProductCode.Contains(cardGetList.ProductCode);
                query = query.And(newQuery);
            }

            if (cardGetList.Status != CardStatus.Undefined)
            {
                Expression<Func<Card, bool>> newQuery = p => p.Status == cardGetList.Status;
                query = query.And(newQuery);
            }

            if (!string.IsNullOrEmpty(cardGetList.Serial))
            {
                Expression<Func<Card, bool>> newQuery = p =>
                    p.Serial.ToLower().Contains(cardGetList.Serial.ToLower());
                query = query.And(newQuery);
            }

            if (!string.IsNullOrEmpty(cardGetList.CardCode))
            {
                Expression<Func<Card, bool>> newQuery = p =>
                    p.CardCode.ToLower().Contains(cardGetList.CardCode.ToLower());
                query = query.And(newQuery);
            }

            if (!string.IsNullOrEmpty(cardGetList.BatchCode))
            {
                Expression<Func<Card, bool>> newQuery = p =>
                    p.BatchCode.ToLower().Contains(cardGetList.BatchCode.ToLower());
                query = query.And(newQuery);
            }

            if (cardGetList.FromExpiredDate.HasValue)
            {
                Expression<Func<Card, bool>> newQuery = p =>
                    p.ExpiredDate >= cardGetList.FromExpiredDate.Value.ToUniversalTime();
                query = query.And(newQuery);
            }

            if (cardGetList.ToExpiredDate.HasValue)
            {
                var date = cardGetList.ToExpiredDate.Value.ToUniversalTime().AddHours(23).AddMinutes(59)
                    .AddSeconds(59);
                Expression<Func<Card, bool>> newQuery = p => p.ExpiredDate <= date;
                query = query.And(newQuery);
            }

            if (cardGetList.FromImportDate.HasValue)
            {
                Expression<Func<Card, bool>> newQuery = p =>
                    p.AddedAtUtc >= cardGetList.FromImportDate.Value.ToUniversalTime();
                query = query.And(newQuery);
            }

            if (cardGetList.ToImportDate.HasValue)
            {
                var date = cardGetList.ToImportDate.Value.ToUniversalTime().AddHours(23).AddMinutes(59)
                    .AddSeconds(59);
                Expression<Func<Card, bool>> newQuery = p => p.AddedAtUtc <= date;
                query = query.And(newQuery);
            }

            if (cardGetList.FromExportedDate.HasValue)
            {
                Expression<Func<Card, bool>> newQuery = p =>
                    p.ExportedDate >= cardGetList.FromExportedDate.Value.ToUniversalTime();
                query = query.And(newQuery);
            }

            if (cardGetList.ToExportedDate.HasValue)
            {
                var date = cardGetList.ToExportedDate.Value.ToUniversalTime().AddHours(23).AddMinutes(59)
                    .AddSeconds(59);
                Expression<Func<Card, bool>> newQuery = p => p.ExportedDate <= date;
                query = query.And(newQuery);
            }

            if (cardGetList.FormCardValue != null && cardGetList.FormCardValue.Value > 0)
            {
                Expression<Func<Card, bool>> newQuery = p => p.CardValue >= cardGetList.FormCardValue.Value;
                query = query.And(newQuery);
            }

            if (cardGetList.ToCardValue != null && cardGetList.ToCardValue.Value > 0)
            {
                Expression<Func<Card, bool>> newQuery = p => p.CardValue <= cardGetList.ToCardValue.Value;
                query = query.And(newQuery);
            }

            if (cardGetList.SearchType == SearchType.Export)
            {
                cardGetList.Offset = 0;
                cardGetList.Limit = int.MaxValue;
            }

            var total = await _cardMongoRepository.CountAsync(query);
            var cardList = await _cardMongoRepository.GetSortedPaginatedAsync<Card, Guid>(query,
                s => s.AddedAtUtc, false,
                cardGetList.Offset, cardGetList.Limit);

            var data = new List<CardDto>();
            if (cardList.Count > 0)
                // var batchCodes = cardList.Select(x => x.BatchCode).ToList();
                // var batchs =
                //     await _cardMongoRepository.GetAllAsync<StockBatch>(p => batchCodes.Contains(p.BatchCode));
                //return cardList.ConvertTo<List<CardDto>>();
                data = cardList.Select(x =>
                {
                    var card = AppendInfo(x);
                    card.CardCode = "... encrypted ...";
                    return card;
                }).ToList();

            return new MessagePagedResponseBase
            {
                ResponseCode = "01",
                ResponseMessage = "Thành công",
                Total = (int)total,
                Payload = data
            };
        }
        catch (Exception ex)
        {
            _logger.LogError("CardGetListAsync error: " + ex.Message);
            return new MessagePagedResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Thất bại",
                Total = 0
            };
        }
    }

    public async Task<StockBatchDto> StockBatchInsertAsync(StockBatchDto stockBatchDto)
    {
        try
        {
            var cardBatch = await _cardMongoRepository.GetOneAsync<StockBatch>(p =>
                p.BatchCode == stockBatchDto.BatchCode);
            if (cardBatch != null) return cardBatch.ConvertTo<StockBatchDto>();

            cardBatch = stockBatchDto.ConvertTo<StockBatch>();

            if (stockBatchDto.Status == StockBatchStatus.Undefined) cardBatch.Status = StockBatchStatus.Active;

            cardBatch.CreatedDate = DateTime.Now;

            if (stockBatchDto.StockBatchItems == null)
                cardBatch.StockBatchItems = new List<StockBatchItem>();
            else
                cardBatch.StockBatchItems = cardBatch.StockBatchItems;

            try
            {
                await _cardMongoRepository.AddOneAsync(cardBatch);
                return stockBatchDto;
            }
            catch (Exception ex)
            {
                _logger.LogError("CardBatch insert error: " + ex.Message);
                return null;
            }
        }
        catch (Exception exx)
        {
            _logger.LogError("CardBatch_Exception: " + exx.Message);
            return null;
        }

    }

    public async Task<MessageResponseBase> StockBatchUpdateAsync(StockBatchDto stockBatchDto)
    {
        var cardBatch = await _cardMongoRepository.GetByIdAsync<StockBatch>(stockBatchDto.Id) ??
                        await _cardMongoRepository.GetOneAsync<StockBatch>(p =>
                            p.BatchCode == stockBatchDto.BatchCode);
        var isInUse = await _cardMongoRepository.AnyAsync<Card>(p => p.BatchCode == cardBatch.BatchCode);
        if (null == cardBatch)
            return new CardResponseMesssage
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Lô không tồn tại"
            };
        if (isInUse
           ) // && (cardBatch.BatchType != cardBatchDto.BatchType || cardBatch.Vendor != cardBatchDto.Vendor))
            return new CardResponseMesssage
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Không thể cập nhật loại lô hoặc nhà mạng cho lô đã có thẻ"
            };

        cardBatch.Status = stockBatchDto.Status;
        // cardBatch.BatchType = cardBatchDto.BatchType;
        cardBatch.Description = stockBatchDto.Description;
        // cardBatch.Vendor = cardBatchDto.Vendor;
        cardBatch.Status = stockBatchDto.Status;
        cardBatch.ModifiedDate = DateTime.Now;

        try
        {
            await _cardMongoRepository.UpdateOneAsync(cardBatch);
            return new CardResponseMesssage
            {
                ResponseCode = "01",
                ResponseMessage = "Thành công"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError("CardBatch update error: " + ex.Message);
            return new CardResponseMesssage
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Error"
            };
        }
    }

    public async Task<MessageResponseBase> StockBatchItemsUpdateAsync(string batchCode, string productCode,
        decimal cardValue, int quantity)
    {
        _logger.LogInformation($"StockBatchItemsUpdateAsync: {batchCode},{productCode},{cardValue},{quantity} ");
        try
        {
            var cardBatch = await _cardMongoRepository.GetOneAsync<StockBatch>(p => p.BatchCode == batchCode);
            if (null == cardBatch)
                return new CardResponseMesssage
                {
                    ResponseCode = ResponseCodeConst.Error,
                    ResponseMessage = $"Batch {batchCode} does not exist"
                };

            if (cardBatch.StockBatchItems == null)
                cardBatch.StockBatchItems = new List<StockBatchItem>();
            var bathItem = cardBatch.StockBatchItems.FirstOrDefault(x => x.ProductCode == productCode);

            if (bathItem != null)
            {
                var amount = quantity * cardValue - quantity * cardValue * ((decimal)bathItem.Discount / 100);
                bathItem.QuantityImport += quantity;
                bathItem.Amount += Math.Round(amount);

                var reason = await _cardMongoRepository.CardBatchUpdateItemAsync(batchCode, bathItem);
                // var reason = await _cardMongoRepository.CardBatchUpdateItemTransAsync(batchCode, bathItem);
                _logger.LogInformation("CardBatchUpdateItemTransAsync: " + reason);
                return new CardResponseMesssage
                {
                    ResponseCode = reason ? "01" : "00",
                    ResponseMessage = reason ? "Thành công" : "Lỗi"
                };
            }

            return new CardResponseMesssage
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = $"BathItem in {batchCode} does not exist"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError("CardBatch update error: " + ex.Message);
            return new CardResponseMesssage
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Error"
            };
        }
    }

    public async Task<MessageResponseBase> StockBatchDeleteAsync(Guid id)
    {
        var cardBatch = await _cardMongoRepository.GetByIdAsync<StockBatch>(id);

        var isInUse = _cardMongoRepository.Any<Card>(p => p.BatchCode == cardBatch.BatchCode);

        var returnMessage = new MessageResponseBase
        {
            ResponseCode = ResponseCodeConst.Error,
            ResponseMessage = "Không thành công!"
        };

        if (isInUse)
        {
            returnMessage.ResponseMessage = "Lô thẻ không thể xóa vì đang được sử dụng";
            returnMessage.ResponseCode = "201";
        }
        else
        {
            try
            {
                await _cardMongoRepository.DeleteOneAsync(cardBatch);
                returnMessage.ResponseCode = "01";
                returnMessage.ResponseMessage = "Thành công!";
            }
            catch (Exception ex)
            {
                _logger.LogError("CardBatch update error: " + ex.Message);
                returnMessage.ResponseCode = "202";
                returnMessage.ResponseMessage = "Lỗi xóa Lô thẻ.";
            }
        }

        return returnMessage;
    }


    public async Task<MessageResponseBase> StockBatchGuidDeleteAsync(Guid id)
    {
        var cardBatch = await _cardMongoRepository.GetByIdAsync<StockBatch>(id);

        var isInUse = _cardMongoRepository.Any<Card>(p => p.BatchCode == cardBatch.BatchCode);

        var returnMessage = new MessageResponseBase
        {
            ResponseCode = ResponseCodeConst.Error,
            ResponseMessage = "Không thành công!"
        };
        try
        {
            await _cardMongoRepository.DeleteOneAsync(cardBatch);
            returnMessage.ResponseCode = "01";
            returnMessage.ResponseMessage = "Thành công!";
        }
        catch (Exception ex)
        {
            _logger.LogError("CardBatch update error: " + ex.Message);
            returnMessage.ResponseCode = "202";
            returnMessage.ResponseMessage = "Lỗi xóa Lô thẻ.";
        }

        return returnMessage;
    }

    public async Task<StockBatchDto> StockBatchGetAsync(Guid id, string batchCode)
    {
        if (!string.IsNullOrEmpty(batchCode))
            return (await _cardMongoRepository.GetOneAsync<StockBatch>(p => p.BatchCode == batchCode))
                .ConvertTo<StockBatchDto>();

        var obj = await _cardMongoRepository.GetByIdAsync<StockBatch>(id);
        obj.CreatedDate = _dateTimeHelper.ConvertToUserTime(obj.CreatedDate, DateTimeKind.Utc);
        return obj.ConvertTo<StockBatchDto>();
    }


    public async Task<MessagePagedResponseBase> StockBatchListGetAsync(StockBatchGetListRequest request)
    {
        Expression<Func<StockBatch, bool>> query = p => p.ImportType != "AIRTIME";
        if (!string.IsNullOrEmpty(request.Filter))
        {
            Expression<Func<StockBatch, bool>> newQuery = p => p.BatchCode.Contains(request.Filter) ||
                                                               //p.Vendor.Contains(request.Filter) ||
                                                               p.Provider.Contains(request.Filter);
            query = query.And(newQuery);
        }

        if (!string.IsNullOrEmpty(request.BatchCode))
        {
            Expression<Func<StockBatch, bool>> newQuery = p => p.BatchCode.Contains(request.BatchCode);
            query = query.And(newQuery);
        }

        if (request.Status != StockBatchStatus.Undefined)
        {
            Expression<Func<StockBatch, bool>> newQuery = p => p.Status == request.Status;
            query = query.And(newQuery);
        }

        // if (!string.IsNullOrEmpty(request.Vendor))
        // {
        //     Expression<Func<StockBatch, bool>> newQuery = p => p.Vendor == (request.Vendor);
        //     query = query.And(newQuery);
        // }

        if (!string.IsNullOrEmpty(request.Provider))
        {
            Expression<Func<StockBatch, bool>> newQuery = p => p.Provider == request.Provider;
            query = query.And(newQuery);
        }

        if (request.FromDate.HasValue)
        {
            Expression<Func<StockBatch, bool>> newQuery = p =>
                p.CreatedDate >= request.FromDate.Value.ToUniversalTime();
            query = query.And(newQuery);
        }

        if (request.ToDate.HasValue)
        {
            Expression<Func<StockBatch, bool>> newQuery = p =>
                p.CreatedDate <= request.ToDate.Value.ToUniversalTime();
            query = query.And(newQuery);
        }

        if (!string.IsNullOrEmpty(request.ImportType))
        {
            Expression<Func<StockBatch, bool>> newQuery = p => p.ImportType == request.ImportType;
            query = query.And(newQuery);
        }

        var total = await _cardMongoRepository.CountAsync(query);
        if (request.SearchType == SearchType.Export)
        {
            request.Offset = 0;
            request.Limit = int.MaxValue;
        }

        var cardBatches = await _cardMongoRepository.GetSortedPaginatedAsync<StockBatch, Guid>(query,
            p => p.CreatedDate, false,
            request.Offset, request.Limit);

        foreach (var item in cardBatches)
            item.CreatedDate = _dateTimeHelper.ConvertToUserTime(item.CreatedDate, DateTimeKind.Utc);

        return new MessagePagedResponseBase
        {
            ResponseCode = "01",
            ResponseMessage = "Thành công",
            Total = (int)total,
            Payload = cardBatches.OrderByDescending(x => x.CreatedDate).ThenBy(x => x.BatchCode)
                .ConvertTo<List<StockBatchDto>>()
        };
    }

    public async Task<StockTransDto> StockTransInsertAsync(StockTransDto dto)
    {
        var order =
            await _cardMongoRepository.GetOneAsync<StockTrans>(p => p.StockTransCode == dto.StockTransCode);
        if (order != null) return order.ConvertTo<StockTransDto>();

        order = dto.ConvertTo<StockTrans>();
        if (order.Status == StockTransStatus.Undefined) order.Status = StockTransStatus.Active;

        order.CreatedDate = DateTime.Now;
        order.StockTransItems = new List<StockTransItem>();
        try
        {
            await _cardMongoRepository.AddOneAsync(order);
            return order.ConvertTo<StockTransDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError("CardTransferInsertAsync error: " + ex.Message);
            return null;
        }
    }

    public async Task<StockTransDto> StockTransUpdateAsync(StockTransDto dto)
    {
        var order =
            await _cardMongoRepository.GetOneAsync<StockTrans>(p => p.StockTransCode == dto.StockTransCode);
        if (order != null) return order.ConvertTo<StockTransDto>();

        order.ModifiedDate = DateTime.Now;
        try
        {
            await _cardMongoRepository.UpdateOneAsync(order);
            return order.ConvertTo<StockTransDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError("CardTransferInsertAsync error: " + ex.Message);
            return null;
        }
    }

    public async Task<long> GetStockItemAvailable(string stockCode, string productCode, string batchCode)
    {
        Expression<Func<Card, bool>> query = p => true && p.Status == CardStatus.Active && p.StockCode == stockCode;
        if (!string.IsNullOrEmpty(productCode))
        {
            Expression<Func<Card, bool>> newQuery = p =>
                p.ProductCode == productCode;
            query = query.And(newQuery);
        }

        if (!string.IsNullOrEmpty(batchCode))
        {
            Expression<Func<Card, bool>> newQuery = p =>
                p.BatchCode == batchCode;
            query = query.And(newQuery);
        }

        return await _cardMongoRepository.CountAsync(query);
    }


    /// <summary>
    ///     CardsInsertAsync
    ///     Insert 1 list cùng mệnh giá cùng productCode
    /// </summary>
    /// <param name="batchCode"></param>
    /// <param name="cardItems"></param>
    /// <returns></returns>
    public async Task<MessageResponseBase> CardsInsertAsync(string batchCode, List<CardSimpleDto> cardItems)
    {
        _logger.LogInformation($"CardsInsertAsync batchCode: {batchCode} cardItems_rows: {cardItems.Count()}");

        var batch = await _cardMongoRepository.GetOneAsync<StockBatch>(x =>
            x.BatchCode == batchCode);
        if (batch == null || batch.Status != StockBatchStatus.Active)
        {
            _logger.LogError($"CardsInsertAsync invalid CardBatch: {batchCode}");
            return MessageResponseBase.Error($"BatchCode {batchCode} không tồn tại, vui lòng kiểm tra lại");
        }

        if (cardItems == null || !cardItems.Any())
        {
            _logger.LogError("CardsInsertAsync invalid CardItems");
            return MessageResponseBase.Error("Danh sách thẻ không có dữ liệu, vui lòng kiểm tra lại");
        }

        var cardFirst = cardItems.FirstOrDefault();
        if (cardFirst == null)
        {
            _logger.LogError("CardsInsertAsync invalid CardFirst");
            return MessageResponseBase.Error("Danh sách thẻ không phù hợp, vui lòng kiểm tra lại");
        }

        var cards = cardItems.Select(x =>
        {
            var batchItem = batch.StockBatchItems.FirstOrDefault(e => e.ProductCode == x.ProductCode);
            var serviceCode = batchItem != null ? batchItem.ServiceCode : "";
            if (string.IsNullOrEmpty(serviceCode))
                serviceCode = x.ProductCode.Split("_")[1];
            var categoryCode = batchItem != null ? batchItem.CategoryCode : "";
            if (string.IsNullOrEmpty(categoryCode))
                categoryCode = x.ProductCode.Split("_")[0];
            return new Card
            {
                BatchCode = batchCode,
                ProviderCode = batch.Provider,
                ServiceCode = serviceCode,
                CategoryCode = categoryCode,
                CardCode = x.CardCode.EncryptTripDes(),
                Serial = x.Serial,
                ProductCode = x.ProductCode,
                CardValue = x.CardValue,
                ImportedDate = DateTime.Now,
                ExpiredDate = x.ExpiredDate,
                StockCode = StockCodeConst.STOCK_TEMP, //Mặc định update thẻ cho vào kho tạm
                Status = CardStatus.Active
            };
        }).ToList();
        //var cards = cardItems.ConvertTo<Card>();
        // Parallel.ForEach(cards, card =>
        // {
        //     card.BatchCode = batchCode;
        //     card.ProviderCode = batch.Provider;
        //     card.ServiceCode = batch.StockBatchItems.FirstOrDefault(x=>x.ProductCode == card.ProductCode)?.ServiceCode;
        //     card.CategoryCode = batch.StockBatchItems.FirstOrDefault(x=>x.ProductCode == card.ProductCode)?.CategoryCode;
        //     card.CardCode = card.CardCode.EncryptTripDes();
        //     card.ImportedDate = DateTime.Now;
        //     card.Status = CardStatus.Active;
        //     card.StockCode = StockCodeConst.STOCK_TEMP; //Mặc định update thẻ cho vào kho tạm
        // });

        try
        {
            var s = await _cardMongoRepository.CardInsertListTransAsync(cards);
            if (s)
            {
                var qty = cards.Count();
                await StockBatchItemsUpdateAsync(batch.BatchCode, cardFirst.ProductCode, cardFirst.CardValue, qty);
                // return MessageResponseBase.Error(
                //     $"Trùng mã thẻ không thể thêm mới danh sách thẻ, vui lòng kiểm tra lại");

                var providerItems = (from x in cards
                                     group x by new { x.BatchCode, x.ProviderCode } into g
                                     select new ProviderCardStockItem
                                     {
                                         ProviderCode = g.Key.ProviderCode,
                                         BatchCode = g.Key.BatchCode,
                                         Quantity = g.Count()
                                     }).ToList();

                return MessageResponseBase.Success(providerItems);
            }

            _logger.LogError("CardsInsertAsync error: " + s);
            return MessageResponseBase.Error(
                "Trùng mã thẻ không thể thêm mới danh sách thẻ, vui lòng kiểm tra lại");
        }
        catch (Exception e)
        {
            _logger.LogError("CardsInsertAsync error: " + e);
            return MessageResponseBase.Error($"Lỗi thêm mới cards: {e.Message}");
            ;
        }
    }

    public async Task<ResponseMesssageObject<string>> GetCardBatchSaleProviderRequest(DateTime date, string provider)
    {
        try
        {
            var fromDate = date.Date.ToUniversalTime();
            var toDate = date.Date.AddDays(1).ToUniversalTime();

            Expression<Func<StockTransRequest, bool>> query = p =>
                p.CreatedDate >= fromDate
                && p.CreatedDate < toDate
                && p.Status == StockBatchStatus.Active;

            if (!string.IsNullOrEmpty(provider))
            {
                Expression<Func<StockTransRequest, bool>> newQuery = p =>
                    p.Provider == provider;
                query = query.And(newQuery);
            }

            var result = _cardMongoRepository.GetAll(query);

            var reponse = new ResponseMesssageObject<string>
            {
                ResponseCode = "01",
                Total = result.Count(),
                Payload = result.ToJson(),
            };
            return await Task.FromResult(reponse);
        }
        catch (Exception e)
        {
            _logger.LogError($"GetCardBatchSaleProviderRequest error: {e}");
            return new ResponseMesssageObject<string>
            {
                ResponseCode = ResponseCodeConst.Error,
                Total = 0,
                Payload = ""
            };
        }
    }

    #region transfer

    public async Task<List<StockTransferItemInfo>> GetCardQuantityAvailableInStock(string stockCode,
        string batchCode,
        string categoryCode, string productCode)
    {
        return await _cardMongoRepository.GetCardQuantityAvailableInStock(stockCode, batchCode, categoryCode,
            productCode);
    }

    #endregion

    private CardDto AppendInfo(Card? card)
    {
        if (card != null)
        {
            var cardDto = card.ConvertTo<CardDto>();
            cardDto.ExpiredDate = _dateTimeHelper.ConvertToUserTime(card.ExpiredDate, DateTimeKind.Utc);
            cardDto.ImportedDate = _dateTimeHelper.ConvertToUserTime(
                card.ImportedDate == DateTime.MinValue ? card.AddedAtUtc : card.ImportedDate, DateTimeKind.Utc);
            if (card.ExportedDate != null && card.ExportedDate != DateTime.MinValue)
                cardDto.ExportedDate = _dateTimeHelper.ConvertToUserTime(card.ExportedDate.Value, DateTimeKind.Utc);
            else
                cardDto.ExportedDate = null;
            return cardDto;
        }

        return null;
    }


    #region viettel api

    //
    // private string _publicViettel =
    //     "MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQCW0VomTHsZ4VoNCWI4L74ief91bNKeBtbngsAO33DKnM6YY645KhJsw4rYaNllGTpO9iF7vqPVxcQ4dokXvlylo+niE7oUVxPJ1htQs+pt5fcDFZl0QMR3oVUAETmJcBJ368O1hKMSsssf2klBMJJpg8fg49IofEHjm5qkGPqkCQIDAQAB";
    //
    // private string _publicPartner =
    //     "MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQC3oVNGoF3MwVdEELdVpMNZTJoCccZrNOJ5MqsUwiyIaTHWDpVQhImCpDRXMGv1b9FQF0Bvg3WCNIwrKtzFdVZFGsDybpZuybBA8vJQcMLzpAewa9NahuQCHZRMX/npLu8GgqcDc2V7aiBrX1kzw620XTyUYxj7dXs3h6eAk0P1cQIDAQAB";
    //
    // private string _privKeyPartner = "MIICdgIBADANBgkqhkiG9w0BAQEFAASCAmAwggJcAgEAAoGBALehU0agXczBV0QQt1Wk\n" +
    //                                  "w1lMmgJxxms04nkyqxTCLIhpMdYOlVCEiYKkNFcwa/Vv0VAXQG+DdYI0jCsq3MV1VkUa\n" +
    //                                  "wPJulm7JsEDy8lBwwvOkB7Br01qG5AIdlExf+eku7waCpwNzZXtqIGtfWTPDrbRdPJRj\n" +
    //                                  "GPt1ezeHp4CTQ/VxAgMBAAECgYA/7zlxY7CE8+QQXMmYVg917gfJRhfRh846aHvMdHbQ\n" +
    //                                  "399sKhOuvxapl8ZpfQB5qf70pcPXj6vAM8+B0CCh12K7gQ6wZbCNfxA4IilE7JHY+2Lg\n" +
    //                                  "ASc3lWt7LY99m1e11El4I1OY0rY18az2mtxUB/54nvQdi1YMddv39q+z8OLkAQJBAO7f\n" +
    //                                  "n0WNP8hlD0PScuI7CG0sYrfWTrNhmulMBaoL5fEJPz83gx6pfpcWkboC3PSDm/lftLH+\n" +
    //                                  "zQlT4+kX+iPnW6ECQQDEy77FTxDloV3cbzIPykB3aeJVu7366/gfUT0Ng7sraoqxdEGt\n" +
    //                                  "8BrNql8ckIjMZhx1OMxmojpQ3RoxLVPaKMfRAkEApUEpc7mLRby8ecQu3FnQs46AYQQv\n" +
    //                                  "ACRnQjzoskJ2+nDWQ4rI+D50KFxhxpjSeYpPLo9Kd9V5zZku1ARVdd9J4QJAI1oqig1b\n" +
    //                                  "DrU/REMhbh66F/mIdDhGt5W+O/n/Crd4XyNDiP9GcTWpyvppHZuFR5qsUA6FAYbxDOe7\n" +
    //                                  "NcxbvNwIkQJARfZiwCZbv10khyqcyN8wgtv3JNyEMp2GWGzwSCDuA1w0IXiEnbbVj1mN\n" +
    //                                  "vv9bVDc248wusWdL5DmjsltqohI9Vw==";
    //
    // private string _password = "changemeplease123a@";
    // private string _username = "partnerchain";
    //
    //
    //
    // public async Task<string> CHECK_CP()
    // {
    //     try
    //     {
    //         using (var rsaPartner = RSA.Create())
    //         {
    //             rsaPartner.ImportPkcs8PrivateKey(System.Convert.FromBase64String(_privKeyPartner), out _);
    //             using (var rsaViettel = RSA.Create())
    //             {
    //                 rsaViettel.ImportSubjectPublicKeyInfo(System.Convert.FromBase64String(_publicViettel), out _);
    //                 var data = (new
    //                 {
    //                     order_id = Guid.NewGuid(),
    //                     username = _username,
    //                     password = rsaViettel.Encrypt(Encoding.ASCII.GetBytes(_password)),
    //                     service_code = "CP",
    //                 }).ToJson();
    //
    //                 var sig = rsaPartner.SignData(
    //                     Encoding.ASCII.GetBytes(data),
    //                     HashAlgorithmName.SHA1,
    //                     RSASignaturePadding.Pkcs1);
    //                 var signature = System.Convert.ToBase64String(sig);
    //
    //                 var request = new ViettelPayRequest()
    //                 {
    //                     Cmd = "CHECK_CP",
    //                     Data = data,
    //                     Signature = signature,
    //                 };
    //
    //                 return CallWebService(request);
    //             }
    //         }
    //     }
    //     catch (Exception e)
    //     {
    //         return null;
    //     }
    // }
    //
    // private static XmlDocument CreateSoapEnvelope(ViettelPayRequest request)
    // {
    //     XmlDocument soapEnvelopeDocument = new XmlDocument();
    //     soapEnvelopeDocument.LoadXml(
    //         $@"<?xml version=""1.0"" encoding=""utf-8""?>
    //         <soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/""
    //             xmlns:par=""http://partnerapi.bankplus.viettel.com"" >
    //             <soapenv:Header/>
    //             <soapenv:Body>
    //             <par:process>
    //                  <cmd>{request.Cmd}</cmd>
    //                  <data>{request.Data}</data>
    //                  <signature>{request.Signature}</signature>
    //             </par:process>
    //             </soapenv:Body>
    //         </soapenv:Envelope>");
    //     return soapEnvelopeDocument;
    // }
    //
    // private string CallWebService(ViettelPayRequest request)
    // {
    //     try
    //     {
    //         var url = "https://api1.viettelpay.vn:8441/PartnerService/PartnerAPI?wsdl";
    //         var body = CreateSoapEnvelope(request);
    //         HttpWebRequest webRequest = (HttpWebRequest) WebRequest.Create(url);
    //         webRequest.Headers.Add("SOAPAction", "process");
    //         webRequest.ContentType = "text/xml;charset=\"utf-8\"";
    //         webRequest.Accept = "text/xml";
    //
    //         webRequest.Method = "POST";
    //         using (Stream stream = webRequest.GetRequestStream())
    //         {
    //             body.Save(stream);
    //         }
    //
    //         string result;
    //         using (WebResponse response = webRequest.GetResponse())
    //         {
    //             result = response.GetResponseStatus().ToString();
    //             // using (StreamReader rd = new StreamReader(response.GetResponseStream()))
    //             // {
    //             //     result = rd.ReadToEnd();
    //             // }
    //         }
    //
    //         return result;
    //     }
    //     catch (Exception e)
    //     {
    //         _logger.LogError("CallWebService Exception:" + e);
    //         return null;
    //     }
    // }
    //
    //
    // [DataContract]
    // public class ViettelPayRequest
    // {
    //     [DataMember(Name = "cmd")] public string Cmd { get; set; }
    //     [DataMember(Name = "data")] public string Data { get; set; }
    //     [DataMember(Name = "signature")] public string Signature { get; set; }
    // }
    //
    // [DataContract]
    // public class ViettelPayResponse
    // {
    //     [DataMember(Name = "data")] public string Data { get; set; }
    //     [DataMember(Name = "signature")] public string signature { get; set; }
    // }

    #endregion

    #region update info

    private string getService(string prodCode)
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

    private string getCategory(string prodCode)
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

    private int getItemAmount(string prodCode)
    {
        try
        {
            var pCode = prodCode.Split("_").ToList();
            if (!pCode.Any() || pCode.Count == 1)
            {
                return 0;
            }

            var item = int.Parse(pCode[^1]);
            return item * 1000;
        }
        catch (Exception e)
        {
            return 0;
        }
    }

    public async Task<MessageResponseBase> StockBatchInfoUpdateAsync()
    {
        _logger.LogInformation("StockBatchInfoUpdateAsync");
        try
        {
            var msg = "StockBatchInfoUpdateAsync: ";
            var batches = await _cardMongoRepository.GetAllAsync<StockBatch>(x => x.ImportType != "AIRTIME");
            if (batches.Any())
            {
                msg += $"batches: {batches.Count}";
                foreach (var batch in batches)
                    if (batch.StockBatchItems.Any())
                    {
                        foreach (var item in batch.StockBatchItems)
                        {
                            item.ServiceCode = getService(item.ProductCode);
                            item.CategoryCode = getCategory(item.ProductCode);
                        }

                        await _cardMongoRepository.UpdateOneAsync(batch);
                    }
            }
            else
            {
                msg += "batches: 0";
            }

            _logger.LogError($"StockBatchInfoUpdateAsync done: {msg}");
            return MessageResponseBase.Success(msg);
        }
        catch (Exception e)
        {
            _logger.LogError("StockBatchInfoUpdateAsync error: " + e);
            return MessageResponseBase.Error($"Lỗi : {e.Message}");
        }
    }

    public async Task<MessageResponseBase> CardsInfoUpdateAsync()
    {
        _logger.LogInformation("CardsInfoUpdateAsync");
        try
        {
            var msg = "CardsInfoUpdateAsync: ";
            var cards = await _cardMongoRepository.GetAllAsync<Card>(x =>
                string.IsNullOrEmpty(x.ServiceCode) || string.IsNullOrEmpty(x.ProviderCode) ||
                string.IsNullOrEmpty(x.CategoryCode));
            var batchs = await _cardMongoRepository.GetAllAsync<StockBatch>(x => x.StockBatchItems.Any());
            if (cards.Any())
            {
                msg += $"cards: {cards.Count}";
                foreach (var item in cards)
                {
                    item.ServiceCode = getService(item.ProductCode);
                    item.CategoryCode = getCategory(item.ProductCode);
                    var batch = batchs.FirstOrDefault(x => x.BatchCode == item.BatchCode);
                    if (batch != null) item.ProviderCode = batch.Provider;

                    await _cardMongoRepository.UpdateOneAsync(item);
                }
            }
            else
            {
                msg += "cards: 0";
            }

            _logger.LogError($"CardsInfoUpdateAsync done: {msg}");
            return MessageResponseBase.Success(msg);
        }
        catch (Exception e)
        {
            _logger.LogError("CardsInfoUpdateAsync error: " + e);
            return MessageResponseBase.Error($"Lỗi : {e.Message}");
        }
    }

    public async Task<MessageResponseBase> StockInfoUpdateAsync()
    {
        _logger.LogInformation("StockInfoUpdateAsync");
        try
        {
            var msg = "StockInfoUpdateAsync: ";
            var items = await _cardMongoRepository.GetAllAsync<Entities.Stock>(x => x.StockType == "PINCODE");
            if (items.Any())
            {
                msg += $"stocks: {items.Count}";
                foreach (var item in items)
                {
                    item.ServiceCode = getService(item.KeyCode);
                    item.CategoryCode = getCategory(item.KeyCode);
                    var val = getItemAmount(item.KeyCode);
                    if (item.ItemValue != val) item.ItemValue = val;

                    await _cardMongoRepository.UpdateOneAsync(item);
                }
            }
            else
            {
                msg += "stocks: 0";
            }

            _logger.LogError($"StockInfoUpdateAsync done: {msg}");
            return MessageResponseBase.Success(msg);
        }
        catch (Exception e)
        {
            _logger.LogError("StockInfoUpdateAsync error: " + e);
            return MessageResponseBase.Error($"Lỗi : {e.Message}");
        }
    }

    public async Task<StockTransRequest> StockTransRequestInsertAsync(StockTransRequest dto)
    {
        try
        {           
            await _cardMongoRepository.AddOneAsync(dto);
            return dto;
        }
        catch (Exception ex)
        {
            _logger.LogError("StockTransRequestInsertAsync error: " + ex.Message);
            return null;
        }
    }

    public async Task<StockTransRequest> StockTransRequestGetAsync(string provider,string transCodeProvider)
    {
        try
        {
            var dto = await _cardMongoRepository.GetOneAsync<StockTransRequest>(x => x.TransCodeProvider == transCodeProvider && x.Provider == provider);            
            return dto;
        }
        catch (Exception ex)
        {
            _logger.LogError("StockTransRequestInsertAsync error: " + ex.Message);
            return null;
        }
    }

    public async Task<bool> StockTransRequestUpdateAsync(string provider, string transCodeProvider, StockBatchStatus status, bool isSyncCard)
    {
        try
        {
            var items = await _cardMongoRepository.GetOneAsync<StockTransRequest>(x => x.TransCodeProvider == transCodeProvider && x.Provider == provider);
            if (items != null)
            {
                items.Status = status;
                if (status == StockBatchStatus.Active && isSyncCard)
                    items.IsSyncCard = isSyncCard;
                await _cardMongoRepository.UpdateOneAsync(items);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError($"{transCodeProvider} - {provider} StockTransRequestUpdateAsync error: " + ex.Message);
            return false;
        }
    }

    public async Task<ResponseMesssageObject<string>> GetProviderConfigRequest(string provider, string productCode)
    {
        try
        {
            Expression<Func<StockProviderConfig, bool>> query = p =>
               p.Provider == provider;

            if (!string.IsNullOrEmpty(productCode))
            {
                Expression<Func<StockProviderConfig, bool>> newQuery = p =>
                    p.ProductCode == productCode;
                query = query.And(newQuery);
            }
            else
            {
                Expression<Func<StockProviderConfig, bool>> newQuery = p =>
              string.IsNullOrEmpty(p.ProductCode);
                query = query.And(newQuery);
            }


            var result = await _cardMongoRepository.GetAllAsync(query);

            var reponse = new ResponseMesssageObject<string>
            {
                ResponseCode = "01",
                Total = result.Count(),
                Payload = result.ToJson(),
            };
            return await Task.FromResult(reponse);
        }
        catch (Exception e)
        {
            _logger.LogError($"GetProviderConfigRequest error: {e}");
            return new ResponseMesssageObject<string>
            {
                ResponseCode = ResponseCodeConst.Error,
                Total = 0,
                Payload = ""
            };
        }
    }

    public async Task<ResponseMesssageObject<string>> CreateProviderConfigRequest(string provider, string productCode, int quantity)
    {
        try
        {
            Expression<Func<StockProviderConfig, bool>> query = p =>
               p.Provider == provider;

            if (!string.IsNullOrEmpty(productCode))
            {
                Expression<Func<StockProviderConfig, bool>> newQuery = p =>
                    p.ProductCode == productCode;
                query = query.And(newQuery);
            }
            else
            {
                Expression<Func<StockProviderConfig, bool>> newQuery = p =>
              string.IsNullOrEmpty(p.ProductCode);
                query = query.And(newQuery);
            }


            var result = await _cardMongoRepository.GetOneAsync(query);
            if (result == null)
            {
                await _cardMongoRepository.AddOneAsync(new StockProviderConfig()
                {
                    Provider = provider,
                    ProductCode = productCode,
                    Quantity = quantity
                });
            }

            var reponse = new ResponseMesssageObject<string>
            {
                ResponseCode = "01",
                Payload = result.ToJson(),
            };
            return await Task.FromResult(reponse);
        }
        catch (Exception e)
        {
            _logger.LogError($"CreateProviderConfigRequest error: {e}");
            return new ResponseMesssageObject<string>
            {
                ResponseCode = ResponseCodeConst.Error,
                Total = 0,
                Payload = ""
            };
        }
    }

    public async Task<ResponseMesssageObject<string>> EditProviderConfigRequest(string provider, string productCode, int quantity)
    {
        try
        {
            Expression<Func<StockProviderConfig, bool>> query = p =>
               p.Provider == provider;

            if (!string.IsNullOrEmpty(productCode))
            {
                Expression<Func<StockProviderConfig, bool>> newQuery = p =>
                    p.ProductCode == productCode;
                query = query.And(newQuery);
            }
            else
            {
                Expression<Func<StockProviderConfig, bool>> newQuery = p =>
              string.IsNullOrEmpty(p.ProductCode);
                query = query.And(newQuery);
            }

            var result = await _cardMongoRepository.GetOneAsync(query);
            if (result != null)
            {
                result.Quantity = quantity;
                await _cardMongoRepository.UpdateOneAsync(result);
            }

            var reponse = new ResponseMesssageObject<string>
            {
                ResponseCode = "01",
                Payload = result.ToJson(),
            };
            return await Task.FromResult(reponse);
        }
        catch (Exception e)
        {
            _logger.LogError($"EditProviderConfigRequest error: {e}");
            return new ResponseMesssageObject<string>
            {
                ResponseCode = ResponseCodeConst.Error,
                Total = 0,
                Payload = ""
            };
        }
    }

    public async Task<StockProviderConfig> GetProviderConfigBy(string provider, string productCode)
    {
        try
        {
            var result = await GetProviderConfigDetailBy(provider, productCode);
            if (result == null) result = await GetProviderConfigDetailBy(provider, string.Empty);
            return result;
        }
        catch (Exception e)
        {
            _logger.LogError($"GetProviderConfigBy error: {e}");
            return null;
        }
    }

    private async Task<StockProviderConfig> GetProviderConfigDetailBy(string provider, string productCode)
    {
        try
        {
            Expression<Func<StockProviderConfig, bool>> query = p =>
               p.Provider == provider;

            if (!string.IsNullOrEmpty(productCode))
            {
                Expression<Func<StockProviderConfig, bool>> newQuery = p =>
                    p.ProductCode == productCode;
                query = query.And(newQuery);
            }
            else
            {
                Expression<Func<StockProviderConfig, bool>> newQuery = p =>
              string.IsNullOrEmpty(p.ProductCode);
                query = query.And(newQuery);
            }


            var result = await _cardMongoRepository.GetOneAsync(query);
            return result;
        }
        catch (Exception e)
        {
            _logger.LogError($"GetProviderConfigBy error: {e}");
            return null;
        }
    }

    #endregion
}