using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Topup.Report.Model.Dtos;
using Topup.Report.Model.Dtos.RequestDto;
using Topup.Report.Model.Dtos.ResponseDto;
using Topup.Shared;
using Topup.Shared.Contracts.Events.Report;
using Topup.Shared.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ServiceStack;
using Topup.Report.Domain.Connectors;
using Topup.Report.Domain.Entities;
using Topup.Report.Domain.Repositories;

namespace Topup.Report.Domain.Services;

public class CardStockReportService : ICardStockReportService
{
    private readonly IConfiguration _configuration;
    private readonly IDateTimeHelper _dateHepper;
    private readonly WebApiConnector _externalServiceConnector;

    private readonly ILogger<CardStockReportService> _logger;

    private readonly IReportMongoRepository _reportMongoRepository;

    public CardStockReportService(IReportMongoRepository reportMongoRepository,
        IDateTimeHelper dateHepper, IConfiguration configuration, WebApiConnector externalServiceConnector,
        ILogger<CardStockReportService> logger)
    {
        _reportMongoRepository = reportMongoRepository;
        _dateHepper = dateHepper;
        _configuration = configuration;
        _externalServiceConnector = externalServiceConnector;
        _logger = logger;
    }

    public async Task<MessageResponseBase> CardStockReportInsertAsync(ReportCardStockMessage request)
    {
        try
        {
            _logger.LogInformation($"CardStockReportInsertAsync request: {request.ToJson()}");
            var item = request.ConvertTo<ReportCardStockHistories>();
            var product = await GetProductBackend(item.ProductCode);
            if (product != null)
            {
                item.CategoryCode = product.CategoryCode;
                item.ServiceCode = product.ServiceCode;
            }

            item.CreatedDate = DateTime.Now;
            await _reportMongoRepository.AddOneAsync(item);
            return new CardResponseMesssage
            {
                ResponseCode = ResponseCodeConst.Success,
                ResponseMessage = "Success"
            };
        }
        catch (Exception e)
        {
            _logger.LogInformation($"CardStockReportInsertAsync error: {e}");
            return new CardResponseMesssage
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Error"
            };
        }
    }

    public async Task<MessageResponseBase> CreateOrUpdateReportCardStockDate(ReportCardStockMessage request)
    {
        try
        {
            var date = request.CreatedDate.Date;
            var exist = await _reportMongoRepository.GetReportCardStockByDate(request.StockType, request.StockCode,
                request.ProductCode, date);

            if (exist != null)
            {
                _logger.LogInformation($"CreateOrUpdateReportCardStockDate update:{request.ToJson()}");
                exist.InventoryAfter = request.InventoryAfter;
                exist.Increase += request.Increase;
                exist.Decrease += request.Decrease;
                if (request.InventoryType.ToUpper() == "SALE")
                {
                    exist.Sale = exist.Sale + request.Decrease;
                }
                else if (request.InventoryType.ToUpper() == "EXCHANGE")
                {
                    exist.ExportOther = exist.ExportOther + request.Decrease;
                    exist.IncreaseOther = exist.IncreaseOther + request.Increase;
                }
                else if (request.InventoryType.ToUpper() == "INVENTORY")
                {
                    exist.IncreaseSupplier = exist.IncreaseSupplier + request.Increase;
                }

                exist.ModifiedDate = DateTime.Now;
                if (string.IsNullOrEmpty(exist.ServiceCode) || string.IsNullOrEmpty(exist.CategoryCode))
                {
                    var product = await GetProductBackend(exist.ProductCode);
                    if (product != null)
                    {
                        exist.CategoryCode = product.CategoryCode;
                        exist.ServiceCode = product.ServiceCode;
                    }
                }

                await _reportMongoRepository.UpdateOneAsync(exist);
            }
            else
            {
                _logger.LogInformation($"CreateOrUpdateReportCardStockDate init:{request.ToJson()}");
                var create = request.ConvertTo<ReportCardStockByDate>();
                create.ShortDate = request.CreatedDate.ToShortDateString();
                if (request.InventoryType.ToUpper() == "SALE")
                {
                    create.Sale = create.Sale + request.Decrease;
                }
                else if (request.InventoryType.ToUpper() == "EXCHANGE")
                {
                    create.ExportOther = create.ExportOther + request.Decrease;
                    create.IncreaseOther = create.IncreaseOther + request.Increase;
                }
                else if (request.InventoryType.ToUpper() == "INVENTORY")
                {
                    create.IncreaseSupplier = create.IncreaseSupplier + request.Increase;
                }

                if (string.IsNullOrEmpty(create.ServiceCode) || string.IsNullOrEmpty(create.CategoryCode))
                {
                    var product = await GetProductBackend(create.ProductCode);
                    if (product != null)
                    {
                        create.CategoryCode = product.CategoryCode;
                        create.ServiceCode = product.ServiceCode;
                    }
                }

                await _reportMongoRepository.AddOneAsync(create);
            }

            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Success,
                ResponseMessage = "Success"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"CreateOrUpdateReportCardStockDate Exception: {ex}");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Error"
            };
        }
    }

    public async Task<MessageResponseBase> CreateOrUpdateReportCardStockProviderDate(ReportCardStockMessage request)
    {
        try
        {
            if (request.ProviderItem != null && request.ProviderItem.Count > 0)
                foreach (var item in request.ProviderItem)
                {
                    var date = request.CreatedDate.Date;
                    var exist = await _reportMongoRepository.GetReportCardStockProviderByDate(item.ProviderCode,
                        request.StockType, request.StockCode, request.ProductCode);

                    if (exist != null && exist.ShortDate == request.CreatedDate.Date.ToShortDateString())
                    {
                        _logger.LogInformation($"CreateOrUpdateReportCardStockProviderDate update:{request.ToJson()}");
                        if (request.Increase != 0)
                            exist.Increase += item.Quantity;
                        else if (request.Decrease != 0) exist.Decrease += item.Quantity;

                        if (request.InventoryType.ToUpper() == "SALE")
                        {
                            exist.Sale += item.Quantity;
                        }
                        else if (request.InventoryType.ToUpper() == "EXCHANGE")
                        {
                            if (request.Decrease != 0)
                                exist.ExportOther += item.Quantity;
                            else if (request.Increase != 0)
                                exist.IncreaseOther += item.Quantity;
                        }
                        else if (request.InventoryType.ToUpper() == "INVENTORY")
                        {
                            exist.IncreaseSupplier += item.Quantity;
                        }

                        exist.ModifiedDate = DateTime.Now;                       
                        exist.InventoryAfter = exist.InventoryBefore + exist.IncreaseSupplier + exist.IncreaseOther -
                                               exist.Sale - exist.ExportOther;

                        if (string.IsNullOrEmpty(exist.ServiceCode) || string.IsNullOrEmpty(exist.CategoryCode))
                        {
                            var product = await GetProductBackend(exist.ProductCode);
                            if (product != null)
                            {
                                exist.CategoryCode = product.CategoryCode;
                                exist.ServiceCode = product.ServiceCode;
                            }
                        }

                        await _reportMongoRepository.UpdateOneAsync(exist);
                    }
                    else
                    {
                        _logger.LogInformation($"CreateOrUpdateReportCardStockProviderDate init:{request.ToJson()}");
                        var create = request.ConvertTo<ReportCardStockProviderByDate>();
                        create.Decrease = 0;
                        create.Increase = 0;
                        create.ShortDate = request.CreatedDate.ToShortDateString();
                        create.ProviderCode = item.ProviderCode;
                        create.InventoryBefore = exist?.InventoryAfter ?? 0;
                        if (request.InventoryType.ToUpper() == "SALE")
                        {
                            create.Sale = create.Sale + item.Quantity;
                        }
                        else if (request.InventoryType.ToUpper() == "EXCHANGE")
                        {
                            if (request.Increase != 0)
                                create.IncreaseOther = create.IncreaseOther + item.Quantity;
                            else if (request.Decrease != 0)
                                create.ExportOther = create.ExportOther + item.Quantity;
                        }
                        else if (request.InventoryType.ToUpper() == "INVENTORY")
                        {
                            create.IncreaseSupplier = create.IncreaseSupplier + item.Quantity;
                        }                      
                        create.InventoryAfter = create.InventoryBefore + create.IncreaseSupplier +
                            create.IncreaseOther - create.Sale - create.ExportOther;
                        if (string.IsNullOrEmpty(create.ServiceCode) || string.IsNullOrEmpty(create.CategoryCode))
                        {
                            var product = await GetProductBackend(create.ProductCode);
                            if (product != null)
                            {
                                create.CategoryCode = product.CategoryCode;
                                create.ServiceCode = product.ServiceCode;
                            }
                        }

                        await _reportMongoRepository.AddOneAsync(create);
                    }
                }


            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Success,
                ResponseMessage = "Success"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"CreateOrUpdateReportCardStockDate Exception: {ex}");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Error"
            };
        }
    }

    public async Task<MessagePagedResponseBase> CardStockHistories(CardStockHistoriesRequest request)
    {
        try
        {
            if (request.ToDate != null) request.ToDate = request.ToDate.Value.Date.AddDays(1).AddSeconds(-1);

            Expression<Func<ReportCardStockHistories, bool>> query = p => true;

            if (request.CardValue > 0)
            {
                Expression<Func<ReportCardStockHistories, bool>> newQuery = p =>
                    p.CardValue == request.CardValue;
                query = query.And(newQuery);
            }

            if (!string.IsNullOrEmpty(request.Vendor))
            {
                Expression<Func<ReportCardStockHistories, bool>> newQuery = p =>
                    p.Vendor == request.Vendor;
                query = query.And(newQuery);
            }

            if (!string.IsNullOrEmpty(request.ProductCode))
            {
                Expression<Func<ReportCardStockHistories, bool>> newQuery = p =>
                    p.ProductCode == request.ProductCode;
                query = query.And(newQuery);
            }

            if (!string.IsNullOrEmpty(request.CategoryCode))
            {
                Expression<Func<ReportCardStockHistories, bool>> newQuery = p =>
                    p.CategoryCode == request.CategoryCode;
                query = query.And(newQuery);
            }

            if (!string.IsNullOrEmpty(request.StockType))
            {
                Expression<Func<ReportCardStockHistories, bool>> newQuery = p =>
                    p.StockType == request.StockType;
                query = query.And(newQuery);
            }

            if (!string.IsNullOrEmpty(request.StockCode))
            {
                Expression<Func<ReportCardStockHistories, bool>> newQuery = p =>
                    p.StockCode == request.StockCode;
                query = query.And(newQuery);
            }

            if (request.FromDate != null)
            {
                Expression<Func<ReportCardStockHistories, bool>> newQuery = p =>
                    p.CreatedDate >= request.FromDate.Value.ToUniversalTime();
                query = query.And(newQuery);
            }

            if (request.ToDate != null)
            {
                Expression<Func<ReportCardStockHistories, bool>> newQuery = p =>
                    p.CreatedDate <= request.ToDate.Value.ToUniversalTime();
                query = query.And(newQuery);
            }

            var total = await _reportMongoRepository.CountAsync(query);

            var lst = await _reportMongoRepository.GetSortedPaginatedAsync<ReportCardStockHistories, Guid>(query,
                s => s.CreatedDate, true,
                request.Offset, request.Limit);
            foreach (var item in lst)
                item.CreatedDate = _dateHepper.ConvertToUserTime(item.CreatedDate, DateTimeKind.Utc);

            return new MessagePagedResponseBase
            {
                ResponseCode = ResponseCodeConst.Success,
                ResponseMessage = "Thành công",
                Total = (int)total,
                Payload = lst.OrderBy(x => x.CreatedDate).ThenBy(x => x.StockCode).ThenBy(x => x.Vendor)
                    .ThenBy(x => x.CardValue).ConvertTo<List<ReportCardStockHistoriesDto>>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"CardStockHistories error {ex}");
            return new MessagePagedResponseBase
            {
                ResponseCode = ResponseCodeConst.Error
            };
        }
    }

    public async Task<MessagePagedResponseBase> CardStockImExPort(CardStockImExPortRequest request)
    {
        return await CardStockImExPortDate(request);
    }

    public async Task<MessagePagedResponseBase> CardStockImExPortProvider(CardStockImExPortProviderRequest request)
    {
        return await CardStockImExPortProviderDate(request);
    }


    public async Task<MessagePagedResponseBase> CardStockInventory(CardStockInventoryRequest request)
    {
        try
        {
            if (request.ToDate != null) request.ToDate = request.ToDate.Value.Date.AddDays(1).AddSeconds(-1);

            Expression<Func<ReportCardStockByDate, bool>> query = p => true;

            if (request.CardValue > 0)
            {
                Expression<Func<ReportCardStockByDate, bool>> newQuery = p =>
                    p.CardValue == request.CardValue;
                query = query.And(newQuery);
            }

            if (!string.IsNullOrEmpty(request.Vendor))
            {
                Expression<Func<ReportCardStockByDate, bool>> newQuery = p =>
                    p.Vendor == request.Vendor;
                query = query.And(newQuery);
            }

            if (!string.IsNullOrEmpty(request.StockCode))
            {
                Expression<Func<ReportCardStockByDate, bool>> newQuery = p =>
                    p.StockCode == request.StockCode;
                query = query.And(newQuery);
            }

            if (!string.IsNullOrEmpty(request.ProductCode))
            {
                Expression<Func<ReportCardStockByDate, bool>> newQuery = p =>
                    p.ProductCode == request.ProductCode;
                query = query.And(newQuery);
            }

            if (!string.IsNullOrEmpty(request.CategoryCode))
            {
                Expression<Func<ReportCardStockByDate, bool>> newQuery = p =>
                    p.CategoryCode == request.CategoryCode;
                query = query.And(newQuery);
            }

            if (!string.IsNullOrEmpty(request.StockType))
            {
                Expression<Func<ReportCardStockByDate, bool>> newQuery = p =>
                    p.StockType == request.StockType;
                query = query.And(newQuery);
            }

            if (request.FromDate != null)
            {
                Expression<Func<ReportCardStockByDate, bool>> newQuery = p =>
                    p.CreatedDate >= request.FromDate.Value.ToUniversalTime();
                query = query.And(newQuery);
            }

            if (request.ToDate != null)
            {
                Expression<Func<ReportCardStockByDate, bool>> newQuery = p =>
                    p.CreatedDate <= request.ToDate.Value.ToUniversalTime();
                query = query.And(newQuery);
            }

            var total = await _reportMongoRepository.CountAsync(query);

            var lst = await _reportMongoRepository.GetSortedPaginatedAsync<ReportCardStockByDate, Guid>(query,
                s => s.CreatedDate, true,
                request.Offset, request.Limit);
            foreach (var item in lst)
                item.CreatedDate = _dateHepper.ConvertToUserTime(item.CreatedDate, DateTimeKind.Utc);

            return new MessagePagedResponseBase
            {
                ResponseCode = ResponseCodeConst.Success,
                ResponseMessage = "Thành công",
                Total = (int)total,
                Payload = lst.OrderBy(x => x.CreatedDate).ThenBy(x => x.StockCode).ThenBy(x => x.Vendor)
                    .ThenBy(x => x.CardValue).ConvertTo<List<ReportCardStockByDateDto>>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"CardStockImportExport error {ex}");
            return new MessagePagedResponseBase
            {
                ResponseCode = ResponseCodeConst.Error
            };
        }
    }

    public async Task SysCardStockInventoryDay()
    {
        _logger.LogInformation("SysCardStockInventoryDay processing");
        try
        {
            var rs = await CardStockGetListRequest(new CardStockGetListRequest
            {
                Limit = int.MaxValue,
                Offset = 0,
                Status = 99
            });
            _logger.LogInformation($"CardStockGetListRequest return: {rs.ResponseCode}-{rs.Total}");
            if (rs.ResponseCode == ResponseCodeConst.Success)
            {
                var lstStock = rs.Payload.ConvertTo<List<ReportCardStockDto>>();
                if (lstStock != null && lstStock.Any())
                {
                    var date = DateTime.Now;
                    foreach (var item in lstStock)
                    {
                        _logger.LogInformation(
                            $"Processing item: {item.StockCode}-{item.StockType}-{item.CardValue} - {date}");
                        var stockInDay = await _reportMongoRepository.GetReportCardStockByDate(item.StockType,
                            item.ProductCode, item.StockCode, date);
                        if (stockInDay == null)
                        {
                            // stockInDay = await _reportMongoRepository.GetReportCardStockByDate(item.Vendor,
                            //     (int) item.CardValue, item.StockCode, date.AddDays(-1));
                            //if (stockInDay != null)
                            //{
                            var dateNew = DateTime.Now;
                            stockInDay = new ReportCardStockByDate
                            {
                                CreatedDate = dateNew,
                                ShortDate = dateNew.ToShortDateString(),
                                Increase = 0,
                                Decrease = 0,
                                StockType = item.StockType,
                                StockCode = item.StockCode,
                                ProductCode = item.ProductCode,
                                CardValue = (int)item.CardValue,
                                InventoryBefore = item.Inventory,
                                InventoryAfter = item.Inventory
                            };
                            await _reportMongoRepository.AddOneAsync(stockInDay);
                            //}
                            // else
                            // {
                            //     _logger.LogInformation(
                            //         $"NotFound item: {item.StockCode}-{item.Vendor}-{item.CardValue} - Day {date.AddDays(-1)}");
                            // }
                        }
                        else
                        {
                            _logger.LogInformation(
                                $"Already exist item: {item.StockCode}-{item.StockCode}-{item.CardValue}  Day - {date}");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogInformation($"SysCardStockInventoryDay error: {ex}");
        }
    }

    private async Task<MessagePagedResponseBase> CardStockImExPortDate(CardStockImExPortRequest request)
    {
        try
        {
            Expression<Func<ReportCardStockByDate, bool>> query = p => true
                                                                       && p.StockCode == request.StoreCode
                                                                       && p.CreatedDate <=
                                                                       DateTime.Now.ToUniversalTime();

            if (!string.IsNullOrEmpty(request.ProductCode))
            {
                Expression<Func<ReportCardStockByDate, bool>> newQuery = p =>
                    p.ProductCode == request.ProductCode;
                query = query.And(newQuery);
            }

            if (!string.IsNullOrEmpty(request.CategoryCode))
            {
                Expression<Func<ReportCardStockByDate, bool>> newQuery = p =>
                    p.CategoryCode == request.CategoryCode;
                query = query.And(newQuery);
            }

            if (!string.IsNullOrEmpty(request.ServiceCode))
            {
                Expression<Func<ReportCardStockByDate, bool>> newQuery = p =>
                    p.ServiceCode == request.ServiceCode;
                query = query.And(newQuery);
            }

            if (request.ToDate != null) request.ToDate = request.ToDate.Value.Date.AddDays(1).AddSeconds(-1);

            if (request.FromDate != null) request.FromDate = request.FromDate.Value.Date;

            var lst = await _reportMongoRepository.GetAllAsync(query);

            var listKy = lst.Where(c => c.CreatedDate <= request.ToDate.Value.ToUniversalTime()
                                        && c.CreatedDate >= request.FromDate.Value.ToUniversalTime());

            var listGroupKy = from x in listKy
                              group x by new { x.StockCode, x.ProductCode, x.CategoryCode, x.CardValue }
                into g
                              select new ReportCardStockByDate
                              {
                                  StockCode = g.Key.StockCode,
                                  ProductCode = g.Key.ProductCode,
                                  CategoryCode = g.Key.CategoryCode,
                                  CardValue = g.Key.CardValue,
                                  Decrease = g.Sum(c => c.Decrease),
                                  Increase = g.Sum(c => c.Increase),
                                  IncreaseOther = g.Sum(c => c.IncreaseOther),
                                  IncreaseSupplier = g.Sum(c => c.IncreaseSupplier),
                                  Sale = g.Sum(c => c.Sale),
                                  ExportOther = g.Sum(c => c.ExportOther)
                              };

            #region 1.Đầu kỳ

            var listBefore = lst.Where(c => c.CreatedDate < request.FromDate.Value.ToUniversalTime());

            var listGroupBefore = from x in listBefore
                                  group x by new { x.StockCode, x.ProductCode, x.CategoryCode, x.CardValue }
                into g
                                  select new ReportCardStockHistories
                                  {
                                      StockCode = g.Key.StockCode,
                                      ProductCode = g.Key.ProductCode,
                                      CategoryCode = g.Key.CategoryCode,
                                      CardValue = Convert.ToInt32(g.Key.CardValue),
                                      CreatedDate = g.Max(c => c.CreatedDate)
                                  };

            var listViewBefore = from x in listGroupBefore
                                 join yc in listBefore on x.ProductCode equals yc.ProductCode
                                 where x.CategoryCode == yc.CategoryCode && x.CardValue == yc.CardValue
                                                                         && x.CreatedDate == yc.CreatedDate
                                 select new ReportCardStockImExPortDto
                                 {
                                     StoreCode = x.StockCode,
                                     ProductCode = x.ProductCode,
                                     CategoryName = x.CategoryCode,
                                     CardValue = x.CardValue,
                                     Before = Convert.ToInt32(yc.InventoryAfter)
                                 };

            #endregion

            #region 2.Cuối kỳ

            var listAfter = lst.Where(c => c.CreatedDate <= request.ToDate.Value.ToUniversalTime());

            var listGroupAfter = from x in listAfter
                                 group x by new { x.StockCode, x.ProductCode, x.CategoryCode, x.CardValue }
                into g
                                 select new ReportCardStockHistories
                                 {
                                     StockCode = g.Key.StockCode,
                                     ProductCode = g.Key.ProductCode,
                                     CategoryCode = g.Key.CategoryCode,
                                     CardValue = Convert.ToInt32(g.Key.CardValue),
                                     CreatedDate = g.Max(c => c.CreatedDate)
                                 };

            var listViewAfter = from x in listGroupAfter
                                join yc in listAfter on x.ProductCode equals yc.ProductCode
                                where x.CategoryCode == yc.CategoryCode && x.CardValue == yc.CardValue
                                                                        && x.CreatedDate == yc.CreatedDate
                                select new ReportCardStockImExPortDto
                                {
                                    StoreCode = x.StockCode,
                                    ProductCode = x.ProductCode,
                                    CategoryName = x.CategoryCode,
                                    CardValue = x.CardValue,
                                    After = Convert.ToInt32(yc.InventoryAfter)
                                };

            #endregion

            #region 3.Hiện tại

            var listGroupCurrent = from x in lst
                                   group x by new { x.StockCode, x.ProductCode, x.CategoryCode, x.CardValue }
                into g
                                   select new ReportCardStockHistories
                                   {
                                       StockCode = g.Key.StockCode,
                                       ProductCode = g.Key.ProductCode,
                                       CategoryCode = g.Key.CategoryCode,
                                       CardValue = Convert.ToInt32(g.Key.CardValue),
                                       CreatedDate = g.Max(c => c.CreatedDate)
                                   };

            var listViewCurrent = from x in listGroupCurrent
                                  join yc in lst on x.ProductCode equals yc.ProductCode
                                  where x.CategoryCode == yc.CategoryCode && x.CardValue == yc.CardValue
                                                                          && x.CreatedDate == yc.CreatedDate
                                  select new ReportCardStockImExPortDto
                                  {
                                      StoreCode = x.StockCode,
                                      ProductCode = x.ProductCode,
                                      CategoryName = x.CategoryCode,
                                      CardValue = x.CardValue,
                                      Current = Convert.ToInt32(yc.InventoryAfter)
                                  };

            #endregion


            var listView = from current in listViewCurrent
                           join k in listGroupKy on current.ProductCode equals k.ProductCode into gk
                           from ky in gk.DefaultIfEmpty()
                           join d in listViewBefore on current.ProductCode equals d.ProductCode into gd
                           from before in gd.DefaultIfEmpty()
                           join c in listViewAfter on current.ProductCode equals c.ProductCode into gc
                           from after in gc.DefaultIfEmpty()
                           select new ReportCardStockImExPortDto
                           {
                               StoreCode = current.StoreCode,
                               ProductCode = current.ProductCode,
                               CategoryName = current.CategoryName,
                               CardValue = current.CardValue,
                               Before = before != null ? before.Before : 0,
                               After = after != null ? after.After : 0,
                               IncreaseSupplier = ky != null ? Convert.ToInt32(ky.IncreaseSupplier) : 0,
                               IncreaseOther = ky != null ? Convert.ToInt32(ky.IncreaseOther) : 0,
                               Sale = ky != null ? Convert.ToInt32(ky.Sale) : 0,
                               ExportOther = ky != null ? Convert.ToInt32(ky.ExportOther) : 0,
                               Current = current.Current
                           };

            var total = listView.Count();
            var sumTotal = new ReportCardStockImExPortDto
            {
                Before = listView.Sum(c => c.Before),
                After = listView.Sum(c => c.After),
                IncreaseSupplier = listView.Sum(c => c.IncreaseSupplier),
                IncreaseOther = listView.Sum(c => c.IncreaseOther),
                Sale = listView.Sum(c => c.Sale),
                ExportOther = listView.Sum(c => c.ExportOther),
                Current = listView.Sum(c => c.Current)
            };

            listView = listView.OrderBy(c => c.CategoryName).OrderBy(c => c.ProductCode).Skip(request.Offset)
                .Take(request.Limit).ToList();

            var productCodes = listView.Select(c => c.ProductCode).Distinct().ToList();
            Expression<Func<ReportProductDto, bool>> queryProduct = p => productCodes.Contains(p.ProductCode);
            var lstProduct = await _reportMongoRepository.GetAllAsync(queryProduct);
            var mView = from x in listView
                        join y in lstProduct on x.ProductCode equals y.ProductCode
                        select new ReportCardStockImExPortDto
                        {
                            StoreCode = x.StoreCode,
                            ServiceName = y.ServiceName,
                            ProductCode = x.ProductCode,
                            ProductName = y.ProductName,
                            CategoryName = y.CategoryName,
                            CardValue = x.CardValue,
                            Before = x.Before,
                            IncreaseSupplier = x.IncreaseSupplier,
                            IncreaseOther = x.IncreaseOther,
                            Sale = x.Sale,
                            ExportOther = x.ExportOther,
                            After = x.After,
                            Current = x.Current
                        };

            return new MessagePagedResponseBase
            {
                ResponseCode = ResponseCodeConst.Success,
                ResponseMessage = "Thành công",
                Total = total,
                SumData = sumTotal,
                Payload = mView
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"CardStockImExPortDate error {ex}");
            return new MessagePagedResponseBase
            {
                ResponseCode = ResponseCodeConst.Error
            };
        }
    }


    private async Task<MessagePagedResponseBase> CardStockImExPortProviderDate(CardStockImExPortProviderRequest request)
    {
        try
        {
            Expression<Func<ReportCardStockProviderByDate, bool>> query = p => true
                                                                               && p.StockCode == request.StoreCode
                                                                               && p.CreatedDate <=
                                                                               DateTime.Now.ToUniversalTime();

            if (!string.IsNullOrEmpty(request.ProductCode))
            {
                Expression<Func<ReportCardStockProviderByDate, bool>> newQuery = p =>
                    p.ProductCode == request.ProductCode;
                query = query.And(newQuery);
            }

            if (!string.IsNullOrEmpty(request.ProviderCode))
            {
                Expression<Func<ReportCardStockProviderByDate, bool>> newQuery = p =>
                    p.ProviderCode == request.ProviderCode;
                query = query.And(newQuery);
            }

            if (!string.IsNullOrEmpty(request.CategoryCode))
            {
                Expression<Func<ReportCardStockProviderByDate, bool>> newQuery = p =>
                    p.CategoryCode == request.CategoryCode;
                query = query.And(newQuery);
            }

            if (!string.IsNullOrEmpty(request.ServiceCode))
            {
                Expression<Func<ReportCardStockProviderByDate, bool>> newQuery = p =>
                    p.ServiceCode == request.ServiceCode;
                query = query.And(newQuery);
            }

            if (request.ToDate != null) request.ToDate = request.ToDate.Value.Date.AddDays(1).AddSeconds(-1);

            if (request.FromDate != null) request.FromDate = request.FromDate.Value.Date;

            var lst = await _reportMongoRepository.GetAllAsync(query);

            var listKy = lst.Where(c => c.CreatedDate <= request.ToDate.Value.ToUniversalTime()
                                        && c.CreatedDate >= request.FromDate.Value.ToUniversalTime());

            var listGroupKy = from x in listKy
                              group x by new { x.StockCode, x.ProviderCode, x.ProductCode, x.CategoryCode, x.CardValue }
                into g
                              select new ReportCardStockProviderByDate
                              {
                                  Description = g.Key.ProviderCode + "|" + g.Key.ProductCode,
                                  ProviderCode = g.Key.ProviderCode,
                                  StockCode = g.Key.StockCode,
                                  ProductCode = g.Key.ProductCode,
                                  CategoryCode = g.Key.CategoryCode,
                                  CardValue = g.Key.CardValue,
                                  Decrease = g.Sum(c => c.Decrease),
                                  Increase = g.Sum(c => c.Increase),
                                  IncreaseOther = g.Sum(c => c.IncreaseOther),
                                  IncreaseSupplier = g.Sum(c => c.IncreaseSupplier),
                                  Sale = g.Sum(c => c.Sale),
                                  ExportOther = g.Sum(c => c.ExportOther)
                              };

            #region 1.Đầu kỳ

            var listBefore = lst.Where(c => c.CreatedDate < request.FromDate.Value.ToUniversalTime());

            var listGroupBefore = from x in listBefore
                                  group x by new { x.StockCode, x.ProviderCode, x.ProductCode, x.CategoryCode, x.CardValue }
                into g
                                  select new ReportCardStockImExPortDto
                                  {
                                      KeyCode = g.Key.ProviderCode + "|" + g.Key.ProductCode,
                                      ProviderCode = g.Key.ProviderCode,
                                      StoreCode = g.Key.StockCode,
                                      ProductCode = g.Key.ProductCode,
                                      CategoryCode = g.Key.CategoryCode,
                                      CardValue = Convert.ToInt32(g.Key.CardValue),
                                      CreatedDay = g.Max(c => c.CreatedDate)
                                  };

            var listViewBefore = from x in listGroupBefore
                                 join yc in listBefore on x.ProductCode equals yc.ProductCode
                                 where x.CategoryCode == yc.CategoryCode && x.CardValue == yc.CardValue
                                                                         && x.CreatedDay == yc.CreatedDate &&
                                                                         x.ProviderCode == yc.ProviderCode
                                 select new ReportCardStockImExPortDto
                                 {
                                     KeyCode = x.ProviderCode + "|" + x.ProductCode,
                                     ProviderCode = x.ProviderCode,
                                     StoreCode = x.StoreCode,
                                     ProductCode = x.ProductCode,
                                     CategoryName = x.CategoryCode,
                                     CardValue = x.CardValue,
                                     Before = Convert.ToInt32(yc.InventoryAfter)
                                 };

            #endregion

            #region 2.Cuối kỳ

            var listAfter = lst.Where(c => c.CreatedDate <= request.ToDate.Value.ToUniversalTime());

            var listGroupAfter = from x in listAfter
                                 group x by new { x.ProviderCode, x.StockCode, x.ProductCode, x.CategoryCode, x.CardValue }
                into g
                                 select new ReportCardStockImExPortDto
                                 {
                                     KeyCode = g.Key.ProviderCode + "|" + g.Key.ProductCode,
                                     ProviderCode = g.Key.ProviderCode,
                                     StoreCode = g.Key.StockCode,
                                     ProductCode = g.Key.ProductCode,
                                     CategoryCode = g.Key.CategoryCode,
                                     CardValue = Convert.ToInt32(g.Key.CardValue),
                                     CreatedDay = g.Max(c => c.CreatedDate)
                                 };

            var listViewAfter = from x in listGroupAfter
                                join yc in listAfter on x.ProductCode equals yc.ProductCode
                                where x.CategoryCode == yc.CategoryCode && x.CardValue == yc.CardValue
                                                                        && x.CreatedDay == yc.CreatedDate &&
                                                                        x.ProviderCode == yc.ProviderCode
                                select new ReportCardStockImExPortDto
                                {
                                    KeyCode = x.ProviderCode + "|" + x.ProductCode,
                                    ProviderCode = x.ProviderCode,
                                    StoreCode = x.StoreCode,
                                    ProductCode = x.ProductCode,
                                    CategoryName = x.CategoryCode,
                                    CardValue = x.CardValue,
                                    After = Convert.ToInt32(yc.InventoryAfter)
                                };

            #endregion

            #region 3.Hiện tại

            var listGroupCurrent = from x in lst
                                   group x by new { x.ProviderCode, x.StockCode, x.ProductCode, x.CategoryCode, x.CardValue }
                into g
                                   select new ReportCardStockImExPortDto
                                   {
                                       KeyCode = g.Key.ProviderCode + "|" + g.Key.ProductCode,
                                       ProviderCode = g.Key.ProviderCode,
                                       StoreCode = g.Key.StockCode,
                                       ProductCode = g.Key.ProductCode,
                                       CategoryCode = g.Key.CategoryCode,
                                       CardValue = Convert.ToInt32(g.Key.CardValue),
                                       CreatedDay = g.Max(c => c.CreatedDate)
                                   };

            var listViewCurrent = from x in listGroupCurrent
                                  join yc in lst on x.ProductCode equals yc.ProductCode
                                  where x.CategoryCode == yc.CategoryCode && x.CardValue == yc.CardValue
                                                                          && x.CreatedDay == yc.CreatedDate &&
                                                                          x.ProviderCode == yc.ProviderCode
                                  select new ReportCardStockImExPortDto
                                  {
                                      KeyCode = x.ProviderCode + "|" + x.ProductCode,
                                      ProviderCode = x.ProviderCode,
                                      StoreCode = x.StoreCode,
                                      ProductCode = x.ProductCode,
                                      CategoryName = x.CategoryCode,
                                      CardValue = x.CardValue,
                                      Current = Convert.ToInt32(yc.InventoryAfter)
                                  };

            #endregion


            var listView = from current in listViewCurrent
                           join k in listGroupKy on current.KeyCode equals k.Description into gk
                           from ky in gk.DefaultIfEmpty()
                           join d in listViewBefore on current.KeyCode equals d.KeyCode into gd
                           from before in gd.DefaultIfEmpty()
                           join c in listViewAfter on current.KeyCode equals c.KeyCode into gc
                           from after in gc.DefaultIfEmpty()
                           select new ReportCardStockImExPortDto
                           {
                               KeyCode = current.KeyCode,
                               ProviderCode = current.ProviderCode,
                               StoreCode = current.StoreCode,
                               ProductCode = current.ProductCode,
                               CategoryCode = current.CategoryCode,
                               CardValue = current.CardValue,
                               Before = before?.Before ?? 0,
                               After = after?.After ?? 0,
                               IncreaseSupplier = ky != null ? Convert.ToInt32(ky.IncreaseSupplier) : 0,
                               IncreaseOther = ky != null ? Convert.ToInt32(ky.IncreaseOther) : 0,
                               Sale = ky != null ? Convert.ToInt32(ky.Sale) : 0,
                               ExportOther = ky != null ? Convert.ToInt32(ky.ExportOther) : 0,
                               Current = current.Current
                           };

            var listNoProvider = (from x in listView
                                  group x by new { x.CategoryCode, x.ProductCode, x.StoreCode, x.CardValue }
                into g
                                  select new ReportCardStockImExPortDto
                                  {
                                      ProductCode = g.Key.ProductCode,
                                      StoreCode = g.Key.StoreCode,
                                      CardValue = g.Key.CardValue,
                                      After = g.Sum(c => c.After),
                                      Before = g.Sum(c => c.Before),
                                      IncreaseSupplier = g.Sum(c => c.IncreaseSupplier),
                                      IncreaseOther = g.Sum(c => c.IncreaseOther),
                                      Sale = g.Sum(c => c.Sale),
                                      ExportOther = g.Sum(c => c.ExportOther),
                                      Current = g.Sum(c => c.Current)
                                  }).ToList();

            var total = listNoProvider.Count();
            var sumTotal = new ReportCardStockImExPortDto
            {
                Before = listNoProvider.Sum(c => c.Before),
                After = listNoProvider.Sum(c => c.After),
                IncreaseSupplier = listNoProvider.Sum(c => c.IncreaseSupplier),
                IncreaseOther = listNoProvider.Sum(c => c.IncreaseOther),
                Sale = listNoProvider.Sum(c => c.Sale),
                ExportOther = listNoProvider.Sum(c => c.ExportOther),
                Current = listNoProvider.Sum(c => c.Current)
            };

            listNoProvider = listNoProvider.OrderBy(c => c.CategoryCode).OrderBy(c => c.ProductCode)
                .Skip(request.Offset).Take(request.Limit).ToList();

            var productCodes = listNoProvider.Select(c => c.ProductCode).Distinct().ToList();
            Expression<Func<ReportProductDto, bool>> queryProduct = p => productCodes.Contains(p.ProductCode);
            var lstProduct = await _reportMongoRepository.GetAllAsync(queryProduct);
            var mView = from x in listNoProvider
                        join y in lstProduct on x.ProductCode equals y.ProductCode
                        select new ReportCardStockImExPortDto
                        {
                            ProviderCode = x.ProviderCode,
                            ProviderName = x.ProviderName,
                            StoreCode = x.StoreCode,
                            ServiceName = y.ServiceName,
                            ProductCode = x.ProductCode,
                            ProductName = y.ProductName,
                            CategoryName = y.CategoryName,
                            CardValue = x.CardValue,
                            Before = x.Before,
                            IncreaseSupplier = x.IncreaseSupplier,
                            IncreaseOther = x.IncreaseOther,
                            Sale = x.Sale,
                            ExportOther = x.ExportOther,
                            After = x.After,
                            Current = x.Current
                        };

            return new MessagePagedResponseBase
            {
                ResponseCode = ResponseCodeConst.Success,
                ResponseMessage = "Thành công",
                Total = total,
                SumData = sumTotal,
                Payload = mView
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"CardStockImExPortDate error {ex}");
            return new MessagePagedResponseBase
            {
                ResponseCode = ResponseCodeConst.Error
            };
        }
    }

    private async Task<ResponseMesssageObject<List<ReportCardStockDto>>> CardStockGetListRequest(
        CardStockGetListRequest input)
    {
        //Đoạn này xem lại sao. Hiện tại k có chỗ nào có cái route này
        var client = new JsonServiceClient(_configuration["ServiceConfig:GatewayPrivate"]);
        try
        {
            var rs = await client.GetAsync<ResponseMesssageObject<List<ReportCardStockDto>>>(input);
            return rs;
        }
        catch (Exception ex)
        {
            _logger.LogError($"CardStockGetListRequest error: {ex}");
            return new ResponseMesssageObject<List<ReportCardStockDto>>
            {
                ResponseCode = ResponseCodeConst.Error
            };
        }
    }

    private async Task<ReportProductDto> GetProductBackend(string productCode)
    {
        try
        {
            ReportInfoCache.Products ??= new List<ReportProductDto>();
            var product = ReportInfoCache.Products.FirstOrDefault(c => c.ProductCode == productCode);
            if (product != null)
                return product;

            product = await _reportMongoRepository.GetReportProductByProductCode(productCode);

            if (product == null)
            {
                var sv = await _externalServiceConnector.GetProductInfoAsync(productCode);
                if (sv != null)
                {
                    product = new ReportProductDto
                    {
                        ProductId = sv.Id,
                        ProductCode = productCode,
                        ProductName = sv.ProductName,
                        ProductValue = sv.ProductValue,
                        CategoryCode = sv.CategoryCode,
                        CategoryName = sv.CategoryName,
                        CategoryId = sv.CategoryId,
                        ServiceId = sv.ServiceId,
                        ServiceCode = sv.ServiceCode,
                        ServiceName = sv.ServiceName
                    };
                    await _reportMongoRepository.UpdateProduct(product);
                }
            }

            if (ReportInfoCache.Products.FirstOrDefault(c => c.ProductCode == productCode) == null && product != null)
                ReportInfoCache.Products.Add(product);

            return product;
        }
        catch (Exception exp)
        {
            _logger.LogError($"GetProductBackend error: {exp}");

            return null;
        }
    }
}