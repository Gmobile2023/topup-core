using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Topup.Gw.Model;
using Topup.Gw.Model.Dtos;
using Topup.Gw.Model.RequestDtos;
using Topup.Shared;
using Topup.Shared.Dtos;
using Topup.Shared.Helpers;
using Topup.Shared.Utils;
using Microsoft.Extensions.Logging;
using Topup.Discovery.Requests.Backends;
using ServiceStack;
using Topup.Gw.Domain.Entities;
using Topup.Gw.Domain.Repositories;

namespace Topup.Gw.Domain.Services;

public class TransactionService : ITransactionService
{
    // private readonly IRequestClient<CollectDiscountCommand> _requestClient;
    private readonly IDateTimeHelper _dateTimeHelper;

    //private readonly Logger _logger = LogManager.GetLogger("BackendTransactionService");
    private readonly ILogger<TransactionService> _logger;
    private readonly IPaygateMongoRepository _paygateMongoRepository;

    public TransactionService(IPaygateMongoRepository paygateMongoRepository,
        IDateTimeHelper dateTimeHelper, ILogger<TransactionService> logger)
    {
        _paygateMongoRepository = paygateMongoRepository;

        _dateTimeHelper = dateTimeHelper;
        _logger = logger;
    }


    public async Task<InvoiceDto> InvoiceCreateAync(InvoiceDto input)
    {
        try
        {
            var item = input.ConvertTo<Invoices>();
            await _paygateMongoRepository.AddOneAsync(item);
            return input;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<SaleHistoryDto> GetSaleRequest(GetSaleRequest input)
    {
        try
        {
            var item = await _paygateMongoRepository.GetOneAsync<SaleRequest>(x =>
                x.TransCode == input.Filter || x.TransRef == input.Filter);


            var result = item.ConvertTo<SaleHistoryDto>();
            if (result != null)
                result.CreatedTime = result.CreatedTime =
                    _dateTimeHelper.ConvertToUserTime(result.CreatedTime, DateTimeKind.Utc);

            return result;
        }
        catch (Exception e)
        {
            _logger.LogError($"GetSaleRequest error: {e}");
            return null;
        }
    }

    public async Task<ResponseMesssageObject<string>> GetSaleTopupRequest(GetSaleTopupRequest input)
    {
        try
        {
            var fromDate = input.FromDate.ToUniversalTime();
            var toDate = input.ToDate.ToUniversalTime();

            Expression<Func<SaleRequest, bool>> query = p =>
                p.CreatedTime >= fromDate
                && p.CreatedTime <= toDate;

            if (!string.IsNullOrEmpty(input.Provider))
            {
                Expression<Func<SaleRequest, bool>> newQuery = p =>
                    p.Provider == input.Provider;
                query = query.And(newQuery);
            }

            if (!string.IsNullOrEmpty(input.PartnerCode))
            {
                Expression<Func<SaleRequest, bool>> newQuery = p =>
                    p.PartnerCode == input.PartnerCode;
                query = query.And(newQuery);
            }

            var result = await _paygateMongoRepository.GetAllAsync(query);

            var reponse = new ResponseMesssageObject<string>
            {
                ResponseCode = ResponseCodeConst.Success,
                ResponseMessage = "Thanh cong",
                Payload = result.ToJson(),
                Total = result.Count(),
            };
            return await Task.FromResult(reponse);
        }
        catch (Exception e)
        {
            _logger.LogError($"GetSaleTopupRequest error: {e}");
            return new ResponseMesssageObject<string>
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Lỗi",
                Total = 0,
            };
        }
    }

    public async Task<ResponseMesssageObject<string>> GetCardBatchRequest(GetCardBatchRequest input)
    {
        try
        {
            var fromDate = input.Date.ToUniversalTime();
            var toDate = input.Date.AddDays(1).ToUniversalTime();

            Expression<Func<CardBatchRequest, bool>> query = p =>
                p.CreatedTime >= fromDate
                && p.CreatedTime < toDate;

            if (!string.IsNullOrEmpty(input.Provider))
            {
                Expression<Func<CardBatchRequest, bool>> newQuery = p =>
                    p.Provider == input.Provider;
                query = query.And(newQuery);
            }

            var result = _paygateMongoRepository.GetAll(query);

            var reponse = new ResponseMesssageObject<string>
            {
                ResponseCode = ResponseCodeConst.Success,
                Total = result.Count(),
                Payload = result.ToJson(),
            };
            return await Task.FromResult(reponse);
        }
        catch (Exception e)
        {
            _logger.LogError($"GetCardBatchRequest error: {e}");
            return new ResponseMesssageObject<string>
            {
                ResponseCode = ResponseCodeConst.Error,
                Total = 0,
                Payload = ""
            };
        }
    }

    public async Task<MessagePagedResponseBase> GetPayBatchBillRequest(GetPayBatchBill request)
    {
        try
        {
            request.ToDate = request.ToDate.Date.AddDays(1).AddSeconds(-1);
            request.FromDate = request.FromDate;

            Expression<Func<SaleRequest, bool>> query = p =>
                p.CreatedTime >= request.FromDate.ToUniversalTime()
                && p.CreatedTime <= request.ToDate.ToUniversalTime()
                && p.Status == SaleRequestStatus.Success
                && p.Amount >= request.BillAmountMin
                && p.CategoryCode == request.CategoryCode;


            if (!string.IsNullOrEmpty(request.ProductCode))
            {
                Expression<Func<SaleRequest, bool>> newQuery = p =>
                    p.ProductCode == request.ProductCode;
                query = query.And(newQuery);
            }

            var lstSearch = await _paygateMongoRepository.GetAllAsync(query);
            var listGroup = (from x in lstSearch
                group x by new { x.PartnerCode }
                into g
                select new PayBatchBillItem
                {
                    AgentCode = g.Key.PartnerCode,
                    Quantity = g.Count(),
                    PayAmount = g.Sum(c => c.Amount)
                }).ToList();

            var listPayBatch = (from x in listGroup
                where x.Quantity >= request.BlockMin
                select new PayBatchBillItem
                {
                    AgentCode = x.AgentCode,
                    Quantity = x.Quantity,
                    PayAmount = x.PayAmount,
                    PayBatchMoney = request.BonusMoneyMax == -1
                        ? request.MoneyBlock *
                          Math.Floor(Convert.ToDecimal(x.Quantity) / Convert.ToDecimal(request.BlockMin))
                        : request.MoneyBlock *
                          Math.Floor(Convert.ToDecimal(x.Quantity) / Convert.ToDecimal(request.BlockMin)) <=
                          request.BonusMoneyMax
                            ? request.MoneyBlock * Math.Floor(Convert.ToDecimal(x.Quantity) /
                                                              Convert.ToDecimal(request.BlockMin))
                            : request.BonusMoneyMax
                }).OrderByDescending(c => c.PayAmount);

            var total = listPayBatch.Count();
            var sumTotal = new PayBatchBillItem
            {
                Quantity = listPayBatch.Sum(c => c.Quantity),
                PayAmount = listPayBatch.Sum(c => c.PayAmount),
                PayBatchMoney = listPayBatch.Sum(c => c.PayBatchMoney)
            };
            var lst = listPayBatch.Skip(request.Offset).Take(request.Limit).ToList();

            return new MessagePagedResponseBase
            {
                ResponseCode = ResponseCodeConst.Success,
                ResponseMessage = "Thành công",
                Total = total,
                SumData = sumTotal,
                Payload = lst
            };
        }
        catch (Exception e)
        {
            _logger.LogError($"GetPayBatchBillRequest error: {e}");
            return null;
        }
    }

    public async Task<MessagePagedResponseBase> GetTopupHistoriesAsync(GetTopupHistoryRequest request)
    {
        try
        {
            var saleRequest =
                await _paygateMongoRepository.GetOneAsync<SaleRequest>(x => x.TransRef == request.TransCode);
            if (saleRequest == null)
                return new MessagePagedResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error,
                    ResponseMessage = "Transaction not found"
                };

            Expression<Func<SaleItem, bool>> query = p => p.SaleTransCode == saleRequest.TransCode;
            var total = await _paygateMongoRepository.CountAsync(query);
            var lst = await _paygateMongoRepository.GetSortedPaginatedAsync<SaleItem, Guid>(query,
                s => s.CreatedTime, false,
                request.Offset, request.Limit);

            var newList = lst.Select(x =>
            {
                var item = saleRequest.ConvertTo<SaleHistoryDto>();
                item.Serial = x.Serial;
                item.CardCode = x.CardCode.DecryptTripleDes();
                item.CardValue = x.CardValue;
                item.CardTransCode = x.CardTransCode;
                item.ItemAmount = x.Amount;
                // item.ExpiredDate = x.CardExpiredDate;
                item.ExpiredDate = _dateTimeHelper.ConvertToUserTime(x.CardExpiredDate, DateTimeKind.Utc);
                return item;
            }).ToList();

            return new MessagePagedResponseBase
            {
                ResponseCode = ResponseCodeConst.Success,
                ResponseMessage = "Thành công",
                Total = (int)total,
                Payload = newList
            };
        }
        catch (Exception e)
        {
            _logger.LogError("TransactionReportsGetAsync error " + e);
            return new MessagePagedResponseBase
            {
                ResponseCode = ResponseCodeConst.Error
            };
        }
    }

    public async Task<MessagePagedResponseBase> GetSaleTransactionDetailAsync(
        GetTopupItemsRequest request)
    {
        try
        {
            var saleRequest = _paygateMongoRepository.GetQueryable<SaleRequest>();
            var topupItem = _paygateMongoRepository.GetQueryable<SaleItem>();
            var query = from a in saleRequest
                join b in topupItem on a.TransCode equals b.SaleTransCode into item
                from topupitem in item.DefaultIfEmpty()
                select new SaleHistoryDto
                {
                    Email = a.Email,
                    Id = a.Id,
                    // Multiples = a.Multiples,
                    ReceiverInfo = a.ReceiverInfo,
                    Provider = a.Provider,
                    Quantity = a.Quantity,
                    Serial = topupitem != null ? topupitem.Serial : null,
                    Status = a.Status,
                    // Vendor = a.Vendor,
                    // Timeout = a.Timeout,
                    CardCode = topupitem != null ? topupitem.CardCode : null,
                    Amount = (int)a.Amount,
                    CardValue = topupitem != null ? topupitem.CardValue : 0,
                    CategoryCode = a.CategoryCode,
                    CreatedTime = a.CreatedTime,
                    CurrencyCode = a.CurrencyCode,
                    DiscountRate = a.DiscountRate,
                    ExpiredDate = topupitem != null ? topupitem.CardExpiredDate : DateTime.Now,
                    PaymentAmount = a.PaymentAmount,
                    PartnerCode = a.PartnerCode,
                    // PasswordApp = a.PasswordApp,
                    ProductCode = a.ProductCode,
                    // TopupCommand = a.TopupCommand,
                    // PriorityFee = a.PriorityFee,
                    // ProcessedAmount = a.ProcessedAmount,
                    ProductProvider = topupitem != null ? topupitem.ProductProvider : null,
                    // RevertAmount = a.RevertAmount,
                    // ShortCode = a.ShortCode,
                    ServiceCode = a.ServiceCode,
                    TransCode = a.TransCode,
                    TransRef = a.TransRef,
                    // WorkerApp = a.WorkerApp,
                    SaleRequestType = a.SaleRequestType,
                    // EndProcessTime = a.EndProcessTime,
                    TopupTransCode = topupitem != null ? topupitem.SaleTransCode : null,
                    PaymentTransCode = a.PaymentTransCode,
                    // PriorityDiscountRate = a.PriorityDiscountRate,
                    // BatchTransCode = a.BatchTransCode,
                    CardTransCode = topupitem != null ? topupitem.CardTransCode : null,
                    TopupTransactionType = topupitem != null ? topupitem.SaleType : null,
                    TopupItemAmount = topupitem != null ? topupitem.Amount : 0,
                    Price = a.Price,
                    StaffAccount = a.StaffAccount
                };


            //Expression<Func<TopupRequest, bool>> query = p => true;

            if (!string.IsNullOrEmpty(request.PartnerCode))
                query = query.Where(p => p.PartnerCode == request.PartnerCode);

            if (!string.IsNullOrEmpty(request.CategoryCode))
                query = query.Where(p => p.CategoryCode == request.CategoryCode);

            if (!string.IsNullOrEmpty(request.ServiceCode))
                query = query.Where(p => p.ServiceCode == request.ServiceCode);


            if (request.ServiceCodes != null && request.ServiceCodes.Count > 0)
                query = query.Where(p => request.ServiceCodes.Contains(p.ServiceCode));

            if (request.CategoryCodes != null && request.CategoryCodes.Count > 0)
                query = query.Where(p => request.CategoryCodes.Contains(p.CategoryCode));

            if (request.ProductCodes != null && request.ProductCodes.Count > 0)
                query = query.Where(p => request.ProductCodes.Contains(p.ProductCode));

            // if (!string.IsNullOrEmpty(request.Vendor))
            //     query = query.Where(p => p.Vendor == request.Vendor);

            if (request.Status != SaleRequestStatus.Undefined)
                query = query.Where(p => p.Status == request.Status);

            if (!string.IsNullOrEmpty(request.MobileNumber))
                query = query.Where(p => p.ReceiverInfo == request.MobileNumber);

            if (!string.IsNullOrEmpty(request.TransRef))
                query = query.Where(p => p.TransRef == request.TransRef);

            if (!string.IsNullOrEmpty(request.TransCode))
                query = query.Where(p => p.TransCode == request.TransCode);

            if (!string.IsNullOrEmpty(request.Serial))
                query = query.Where(p => p.Serial == request.Serial);

            if (!string.IsNullOrEmpty(request.TopupTransactionType))
                query = query.Where(p => p.TopupTransactionType == request.TopupTransactionType);

            if (!string.IsNullOrEmpty(request.Filter))
                query = query.Where(p =>
                    p.TransCode == request.Filter || p.TransRef == request.Filter ||
                    p.ReceiverInfo == request.Filter || p.Provider == request.Filter ||
                    p.Email == request.Filter ||
                    p.ProductCode == request.Filter ||
                    p.CategoryCode == request.Filter ||
                    p.PartnerCode == request.Filter ||
                    p.PaymentTransCode == request.Filter ||
                    p.StaffAccount == request.Filter);

            if (default != request.FromDate)
            {
                var fromDate = (DateTime?)_dateTimeHelper.ConvertToUtcTime(request.FromDate,
                    _dateTimeHelper.CurrentTimeZone());
                query = query.Where(p => p.CreatedTime >= fromDate);
            }

            if (default != request.ToDate)
            {
                var toDate = (DateTime?)_dateTimeHelper.ConvertToUtcTime(request.ToDate,
                    _dateTimeHelper.CurrentTimeZone());
                query = query.Where(p => p.CreatedTime <= toDate);
            }

            if (!string.IsNullOrEmpty(request.TopupTransactionType))
                query = query.Where(x => x.TopupTransactionType == request.TopupTransactionType);


            if (!string.IsNullOrEmpty(request.StaffAccount) &&
                request.StaffAccount != request.PartnerCode)
                query = query.Where(x => x.StaffAccount == request.StaffAccount);

            // if (!string.IsNullOrEmpty(request.WorkerApp))
            //     query = query.Where(x => x.WorkerApp.Contains(request.WorkerApp));

            query = query.OrderByDescending(x => x.CreatedTime).ThenBy(x => x.TransCode).ThenBy(x => x.PartnerCode);
            var total = query.Count();
            var saleRequests = query.Skip(request.Offset).Take(request.Limit);
            var lst = saleRequests.ConvertTo<List<SaleHistoryDto>>();
            foreach (var item in lst)
            {
                item.CreatedTime = _dateTimeHelper.ConvertToUserTime(item.CreatedTime, DateTimeKind.Utc);
                if (item.PaymentAmount > 0)
                    item.PaymentAmount /= item.Quantity;
                item.Amount /= item.Quantity;
                item.Quantity = 1;
            }

            return new MessagePagedResponseBase
            {
                ResponseCode = ResponseCodeConst.Success,
                ResponseMessage = "Thành công",
                Total = total,
                Payload = lst
            };
        }
        catch (Exception)
        {
            return new MessagePagedResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Error",
                Total = 0,
                Payload = new List<SaleHistoryDto>()
            };
        }
    }

    public async Task<decimal> GetTotalAmountPerDay(string accountCode, string serviceCode, string categoryCode,
        string productCode)
    {
        Expression<Func<SaleRequest, bool>> query = p =>
            p.StaffAccount == accountCode && p.Status == SaleRequestStatus.Success &&
            p.CreatedTime >= DateTime.Today &&
            p.CreatedTime <= DateTime.UtcNow;

        if (!string.IsNullOrEmpty(serviceCode))
        {
            Expression<Func<SaleRequest, bool>> newQuery = p =>
                p.ServiceCode == serviceCode;
            query = query.And(newQuery);
        }

        if (!string.IsNullOrEmpty(categoryCode))
        {
            Expression<Func<SaleRequest, bool>> newQuery = p =>
                p.CategoryCode == categoryCode;
            query = query.And(newQuery);
        }

        if (!string.IsNullOrEmpty(productCode))
        {
            Expression<Func<SaleRequest, bool>> newQuery = p =>
                p.ProductCode == productCode;
            query = query.And(newQuery);
        }

        var values =
            await _paygateMongoRepository.SumByAsync(query, x => x.PaymentAmount);
        return values;
    }

    public async Task<AccountProductLimitDto> GetLimitProductTransPerDay(string accountCode, string productCode)
    {
        try
        {
            Expression<Func<SaleRequest, bool>> query = p =>
                p.PartnerCode == accountCode && p.Status == SaleRequestStatus.Success &&
                p.ProductCode == productCode &&
                p.CreatedTime >= DateTime.Today &&
                p.CreatedTime <= DateTime.UtcNow;
            var model = new AccountProductLimitDto
            {
                TotalAmount = await _paygateMongoRepository.SumByAsync(query, x => x.Price),
                TotalQuantity = await _paygateMongoRepository.SumByAsync(query, x => x.Quantity)
            };
            return model;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<MessagePagedResponseBase> GetOffsetTopupHistoriesAsync(GetOffsetTopupHistoryRequest request)
    {
        try
        {
            var fromDate = request.FromDate.ToUniversalTime();
            var toDate = request.ToDate.AddDays(1).ToUniversalTime();

            Expression<Func<SaleOffsetRequest, bool>> query = p =>
                p.CreatedTime >= fromDate
                && p.CreatedTime < toDate;

            if (!string.IsNullOrEmpty(request.TransCode))
            {
                Expression<Func<SaleOffsetRequest, bool>> newQuery = p =>
                    p.TransCode == request.TransCode;
                query = query.And(newQuery);
            }

            if (!string.IsNullOrEmpty(request.OriginTransCode))
            {
                Expression<Func<SaleOffsetRequest, bool>> newQuery = p =>
                    p.OriginTransCode == request.OriginTransCode;
                query = query.And(newQuery);
            }


            if (!string.IsNullOrEmpty(request.OriginPartnerCode))
            {
                Expression<Func<SaleOffsetRequest, bool>> newQuery = p =>
                    p.OriginPartnerCode == request.OriginPartnerCode;
                query = query.And(newQuery);
            }


            if (!string.IsNullOrEmpty(request.PartnerCode))
            {
                Expression<Func<SaleOffsetRequest, bool>> newQuery = p =>
                    p.PartnerCode == request.PartnerCode;
                query = query.And(newQuery);
            }

            if (!string.IsNullOrEmpty(request.ReceiverInfo))
            {
                Expression<Func<SaleOffsetRequest, bool>> newQuery = p =>
                    p.ReceiverInfo == request.ReceiverInfo;
                query = query.And(newQuery);
            }

            if (request.Status > 0)
            {
                var status = SaleRequestStatus.InProcessing;
                if (request.Status == 1)
                    status = SaleRequestStatus.Success;
                else if (request.Status == 3)
                    status = SaleRequestStatus.Failed;

                Expression<Func<SaleOffsetRequest, bool>> newQuery = p =>
                    p.Status == status;
                query = query.And(newQuery);
            }

            var total = await _paygateMongoRepository.CountAsync(query);
            var lst = await _paygateMongoRepository.GetSortedPaginatedAsync<SaleOffsetRequest, Guid>(query,
                s => s.CreatedTime, false,
                request.Offset, request.Limit);

            foreach (var item in lst)
            {
                item.CreatedTime = _dateTimeHelper.ConvertToUserTime(item.CreatedTime, DateTimeKind.Utc);
            }

            return new MessagePagedResponseBase
            {
                ResponseCode = ResponseCodeConst.Success,
                ResponseMessage = "Thành công",
                Total = (int)total,
                Payload = lst.ConvertTo<List<SaleOffsetRequestDto>>()
            };
        }
        catch (Exception e)
        {
            _logger.LogError($"GetOffsetTopupHistoriesAsync error: {e}");
            return new MessagePagedResponseBase
            {
                ResponseCode = ResponseCodeConst.Error
            };
        }
    }
}