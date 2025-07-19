using Topup.Report.Model.Dtos;
using Topup.Report.Model.Dtos.RequestDto;
using Topup.Shared;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Topup.Report.Domain.Entities;

namespace Topup.Report.Domain.Services
{
    public partial class BalanceReportService
    {

        public async Task<MessagePagedResponseBase> ReportComparePartnerGetList(ReportComparePartnerRequest request)
        {
            try
            {
                Expression<Func<ReportItemDetail, bool>> query = p => (p.AccountCode == request.AgentCode || p.PerformAccount == request.AgentCode)
                && p.CreatedTime >= request.FromDate.Date.ToUniversalTime()
                && p.CreatedTime < request.ToDate.Date.AddDays(1).ToUniversalTime();

                if (request.Type == ReportServiceCode.PIN_CODE.Replace("_", ""))
                {
                    if (request.ServiceCode == ReportServiceCode.PIN_GAME)
                    {
                        Expression<Func<ReportItemDetail, bool>> newQuery = p => p.Status == ReportStatus.Success &&
                        p.ServiceCode == ReportServiceCode.PIN_GAME && p.TransType != ReportServiceCode.REFUND;
                        query = query.And(newQuery);
                    }
                    else if (request.ServiceCode == ReportServiceCode.PIN_CODE)
                    {
                        Expression<Func<ReportItemDetail, bool>> newQuery = p => p.Status == ReportStatus.Success &&
                        p.ServiceCode == ReportServiceCode.PIN_CODE && p.TransType != ReportServiceCode.REFUND;
                        query = query.And(newQuery);
                    }
                    else
                    {
                        Expression<Func<ReportItemDetail, bool>> newQuery = p => p.Status == ReportStatus.Success &&
                        (p.ServiceCode == ReportServiceCode.PIN_CODE || p.ServiceCode == ReportServiceCode.PIN_GAME) && p.TransType != ReportServiceCode.REFUND;
                        query = query.And(newQuery);
                    }

                }
                else if (request.Type == ReportServiceCode.TOPUP)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p => p.Status == ReportStatus.Success &&
                    p.ServiceCode == ReportServiceCode.TOPUP && p.TransType != ReportServiceCode.REFUND;
                    query = query.And(newQuery);

                    if (request.ChangerType == ReceiverType.PostPaid || request.ChangerType == ReceiverType.PrePaid)
                    {
                        Expression<Func<ReportItemDetail, bool>> newQueryChanger = p => p.ReceiverType == request.ChangerType;                 
                        query = query.And(newQueryChanger);
                    }                                           
                }
                else if (request.Type == "DATA")
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p => p.Status == ReportStatus.Success &&
                       (p.ServiceCode == ReportServiceCode.PIN_DATA || p.ServiceCode == ReportServiceCode.TOPUP_DATA)
                       && p.TransType != ReportServiceCode.REFUND;
                    query = query.And(newQuery);
                }
                else if (request.Type == ReportServiceCode.PAY_BILL.Replace("_", ""))
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p => p.Status == ReportStatus.Success &&
                       p.ServiceCode == ReportServiceCode.PAY_BILL && p.TransType != ReportServiceCode.REFUND;
                    query = query.And(newQuery);
                }
                else if (request.Type == "EXPORT" || request.Type == "SENDMAIL")
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p => p.Status == ReportStatus.Success &&
                       (p.ServiceCode == ReportServiceCode.TOPUP || p.ServiceCode == ReportServiceCode.TOPUP_DATA || p.ServiceCode == ReportServiceCode.PIN_CODE
                     || p.ServiceCode == ReportServiceCode.PIN_DATA || p.ServiceCode == ReportServiceCode.PIN_GAME || p.ServiceCode == ReportServiceCode.PAY_BILL) && p.TransType != ReportServiceCode.REFUND;
                    query = query.And(newQuery);
                }

                var listSouces = _reportMongoRepository.GetAll<ReportItemDetail>(query);

                if (request.Type == "BALANCE")
                {
                    var requestSouce = listSouces.Where(c => c.Status == ReportStatus.Error && c.TransType != ReportServiceCode.REFUND).Select(c => c.TransCode).ToList();
                    var requestSouceRefund = listSouces.Where(c => c.Status == ReportStatus.Success && c.TransType == ReportServiceCode.REFUND && c.TransTransSouce != null).Select(c => c.TransTransSouce).ToList();

                    var balance = await GetBalanceAgent(request.AgentCode, request.FromDate, request.ToDate);

                    var balanceItems = new List<ReportBalancePartnerDto>();
                    //1.Dư đầu kỳ
                    balanceItems.Add(new ReportBalancePartnerDto()
                    {
                        Index = 1,
                        Name = "Dư đầu kỳ",
                        Value = balance.BalanceBefore,
                    });

                    //2.Nạp trong kỳ
                    balanceItems.Add(new ReportBalancePartnerDto()
                    {
                        Index = 2,
                        Name = "Nạp trong kỳ",
                        Value = listSouces.Where(c => c.TransType == ReportServiceCode.DEPOSIT && c.Status == ReportStatus.Success).Sum(c => c.TotalPrice),
                    });

                    balanceItems.Add(new ReportBalancePartnerDto()
                    {
                        Index = 3,
                        Name = "Số tiền tạm ứng trong kỳ",
                        Value = 0,
                    });

                    //3.Bán trong kỳ
                    balanceItems.Add(new ReportBalancePartnerDto()
                    {
                        Index = 4,
                        Name = "Bán trong kỳ",
                        Value = listSouces.Where(p => (p.ServiceCode == ReportServiceCode.TOPUP
                        || p.ServiceCode == ReportServiceCode.TOPUP_DATA
                        || p.ServiceCode == ReportServiceCode.PIN_CODE
                        || p.ServiceCode == ReportServiceCode.PIN_DATA
                        || p.ServiceCode == ReportServiceCode.PIN_GAME
                        || p.ServiceCode == ReportServiceCode.PAY_BILL) && p.TransType != ReportServiceCode.REFUND
                        && p.Status == ReportStatus.Success).Sum(c => c.TotalPrice),
                    });

                    //4.Lỗi kỳ trước, hoàn trong kỳ
                    var excpetSouceRefund = requestSouceRefund.Except(requestSouce).ToList();
                    var amountRefund = listSouces.Where(c => c.RequestTransSouce != null && excpetSouceRefund.Contains(c.RequestTransSouce)).Sum(c => Math.Abs(c.TotalPrice));
                    balanceItems.Add(new ReportBalancePartnerDto()
                    {
                        Index = 5,
                        Name = "Lỗi kỳ trước, hoàn trong kỳ",
                        Value = amountRefund,
                    });

                    //5.Lỗi trong kỳ, hoàn kỳ sau
                    var excpetSouce = requestSouce.Except(requestSouceRefund).ToList();
                    var amountSouce = listSouces.Where(c => c.TransCode != null && excpetSouce.Contains(c.TransCode)).Sum(c => c.TotalPrice);
                    balanceItems.Add(new ReportBalancePartnerDto()
                    {
                        Index = 6,
                        Name = "Lỗi trong kỳ, hoàn kỳ sau",
                        Value = amountSouce,
                    });

                    //6.Chưa có kết quả
                    balanceItems.Add(new ReportBalancePartnerDto()
                    {
                        Index = 7,
                        Name = "Chưa có kết quả",
                        Value = listSouces.Where(c =>
                        c.Status == ReportStatus.TimeOut || c.Status == ReportStatus.Process).Sum(c => c.TotalPrice),

                    });
                    //7.Phát sinh tăng khác
                    balanceItems.Add(new ReportBalancePartnerDto()
                    {
                        Index = 8,
                        Name = "Phát sinh tăng khác trong kỳ",
                        Value = listSouces.Where(c => (c.TransType == ReportServiceCode.CORRECTUP || c.TransType == ReportServiceCode.PAYBATCH
                        || (c.TransType == ReportServiceCode.TRANSFER && c.AccountCode == request.AgentCode))
                        && c.Status == ReportStatus.Success).Sum(c => c.TotalPrice),
                    });

                    //8.Giảm trừ trong kỳ
                    balanceItems.Add(new ReportBalancePartnerDto()
                    {
                        Index = 9,
                        Name = "Giảm trừ khác trong kỳ",
                        Value = listSouces.Where(c => (c.TransType == ReportServiceCode.CORRECTDOWN
                        || (c.TransType == ReportServiceCode.TRANSFER && c.AccountCode != request.AgentCode))
                        && c.Status == ReportStatus.Success).Sum(c => c.TotalPrice),
                    });

                    //9.Dư cuối kỳ
                    balanceItems.Add(new ReportBalancePartnerDto()
                    {
                        Index = 10,
                        Name = "Dư cuối kỳ",
                        Value = balance.BalanceAfter,
                    });


                    return new MessagePagedResponseBase
                    {
                        ResponseCode = ResponseCodeConst.Success,
                        ResponseMessage = "Thành công",
                        Total = 1,
                        Payload = balanceItems,
                    };
                }
                else
                {
                    var list = (from g in listSouces
                                select new ReportComparePartnerDto()
                                {
                                    ServiceCode = g.ServiceCode,
                                    ServiceName = g.ServiceName,
                                    CategoryCode = g.CategoryCode,
                                    CategoryName = g.CategoryName,
                                    ProductCode = g.ProductCode,
                                    ProductName = g.ProductName,
                                    Quantity = g.Quantity,
                                    Value = Convert.ToDecimal(g.Amount),
                                    ProductValue = Convert.ToDecimal(g.ServiceCode == ReportServiceCode.PAY_BILL ? 1 : g.Price),
                                    Discount = Convert.ToDecimal(Math.Round(g.Discount, 0)),
                                    DiscountRate = Convert.ToDecimal(g.ServiceCode == ReportServiceCode.PAY_BILL ? 0 : Math.Round(g.Price != 0 && g.Quantity != 0 ? g.Discount / g.Price / g.Quantity * 100 : 0, 2)),
                                    Fee = Convert.ToDecimal(Math.Round(g.Fee, 0)),
                                    FeeText = g.FeeText ?? string.Empty,
                                    Price = Convert.ToDecimal(Math.Round(g.TotalPrice, 0)),
                                    Note = g.ReceiverType == "POSTPAID" ? "Trả sau" : g.ReceiverType == "PREPAID" ? "Trả trước" : "",
                                    ReceiverType = g.ReceiverType
                                }).OrderBy(c => c.ProductCode).ToList();

                    var listGroup = (from g in list
                                     group g by new { g.ServiceCode, g.ServiceName, g.CategoryCode, g.CategoryName, g.ProductCode, g.ProductName, g.ProductValue, g.DiscountRate, g.FeeText, g.Note, g.ReceiverType } into g
                                     select new ReportComparePartnerDto()
                                     {
                                         ServiceCode = g.Key.ServiceCode,
                                         ServiceName = g.Key.ServiceName,
                                         CategoryCode = g.Key.CategoryCode,
                                         CategoryName = g.Key.CategoryName,
                                         ProductCode = g.Key.ProductCode,
                                         ProductName = g.Key.ProductName,
                                         ProductValue = g.Key.ProductValue,
                                         DiscountRate = g.Key.DiscountRate,
                                         FeeText = g.Key.FeeText,
                                         ReceiverType = g.Key.ReceiverType,
                                         Quantity = g.Sum(c => c.Quantity),
                                         Discount = g.Sum(c => c.Discount),
                                         Fee = g.Sum(c => c.Fee),
                                         Price = g.Sum(c => c.Price),
                                         Value = g.Sum(c => c.Value),
                                         Note = g.Key.Note                                         
                                     }).OrderBy(c => c.ProductCode).ToList();

                    var total = listGroup.Count;
                    var sumTotal = new ReportComparePartnerDto()
                    {
                        Quantity = listGroup.Sum(c => c.Quantity),
                        Discount = listGroup.Sum(c => c.Discount),
                        Value = listGroup.Sum(c => c.Value),
                        Fee = listGroup.Sum(c => c.Fee),
                        Price = listGroup.Sum(c => c.Price),
                    };

                    var serviceNames = listGroup.Select(c => c.ServiceName).Distinct().ToList();
                    var listGroupOrder = new List<ReportComparePartnerDto>();

                    foreach (var service in serviceNames.OrderBy(c => c))
                    {
                        var litCates = listGroup.Where(c => c.ServiceName == service).ToList();
                        var cates = litCates.Select(c => c.CategoryName).Distinct().ToList();
                        foreach (var cate in cates.OrderBy(c => c))
                        {
                            var products = litCates.Where(c => c.CategoryName == cate && c.ServiceName == service).OrderBy(c => c.ProductValue).ToList();
                            listGroupOrder.AddRange(products);
                        }
                    }

                    var lst = listGroupOrder.Skip(request.Offset).Take(request.Limit);

                    return new MessagePagedResponseBase
                    {
                        ResponseCode = ResponseCodeConst.Success,
                        ResponseMessage = "Thành công",
                        Total = (int)total,
                        SumData = sumTotal,
                        Payload = lst,
                    };
                }
            }
            catch (Exception e)
            {
                _logger.LogError($"ReportComparePartnerGetList error: {e}");
                return new MessagePagedResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error
                };
            }
        }

        public Task<List<ReportWarning>> GetCheckAgentBalance(SyncAgentBalanceRequest request)
        {
            try
            {
                Expression<Func<ReportAccountBalanceDay, bool>> query = p =>
                   p.CurrencyCode == "VND" && p.AccountType != "SYSTEM"
                && p.CreatedDay >= request.FromDate.ToUniversalTime()
                && p.CreatedDay <= request.ToDate.ToUniversalTime();

                if (!string.IsNullOrEmpty(request.AgentCode))
                {
                    Expression<Func<ReportAccountBalanceDay, bool>> newQuery = p =>
                       p.AccountCode == request.AgentCode;
                    query = query.And(newQuery);
                }

                var list = _reportMongoRepository.GetAll(query).ToList();

                var litCheck = list.Where(p => p.BalanceBefore + (p.IncDeposit ?? 0) + (p.IncOther ?? 0)
                - (p.DecOther ?? 0) - (p.DecPayment ?? 0) - p.BalanceAfter != 0).ToList();

                var lit = (from x in litCheck
                           select new ReportWarning
                           {
                               AgentCode = x.AccountCode,
                               AgentName = x.AccountInfo,
                               CreatedDay = _dateHepper.ConvertToUserTime(x.CreatedDay, DateTimeKind.Utc).ToString("yyyy-MM-dd")
                           }).ToList();

                return Task.FromResult(lit);
            }
            catch (Exception e)
            {
                _logger.LogError($"GetCheckAgentBalance Exception : {e.Message}|{e.InnerException}|{e.StackTrace}");
                return Task.FromResult(new List<ReportWarning>());
            }
        }

        public async Task<object> SysAgentBalance(SyncAgentBalanceRequest request)
        {
            try
            {
                var date = request.FromDate.ToString("yyyyMMdd");
                var dateAfter = request.FromDate.AddDays(-1).ToString("yyyyMMdd");
                var dateFromSearch = request.FromDate.Date.ToUniversalTime();
                var dateToSearch = request.FromDate.Date.AddDays(1).ToUniversalTime();
                Expression<Func<ReportItemDetail, bool>> query = p => p.AccountCode == request.AgentCode
                && p.CreatedTime >= dateFromSearch
                && p.CreatedTime < dateToSearch;
                var listAll = await _reportMongoRepository.GetAllAsync<ReportItemDetail>(query);

                var infoAccount = _reportMongoRepository.GetReportAccountBalanceDayOpenAsync(request.AgentCode, "VND", request.AgentCode + "_" + date).Result;
                if (infoAccount != null)
                {
                    infoAccount.IncDeposit = listAll.Where(c => c.TransType == ReportServiceCode.DEPOSIT).Sum(c => Math.Abs(c.TotalPrice));
                    infoAccount.IncOther = listAll.Where(c => (c.TransType == ReportServiceCode.TRANSFER
                    || c.TransType == ReportServiceCode.REFUND
                    || c.TransType == ReportServiceCode.CORRECTUP
                    || c.TransType == ReportServiceCode.PAYBATCH)
                    && c.AccountCode == request.AgentCode).Sum(c => Math.Abs(c.TotalPrice));
                    infoAccount.DecPayment = listAll.Where(c => (c.ServiceCode == ReportServiceCode.TOPUP || c.ServiceCode == ReportServiceCode.TOPUP_DATA
                    || c.ServiceCode == ReportServiceCode.PAY_BILL || c.ServiceCode == ReportServiceCode.PIN_DATA || c.ServiceCode == ReportServiceCode.PIN_GAME || c.ServiceCode == ReportServiceCode.PIN_CODE)
                     && c.TransType != ReportServiceCode.REFUND
                    ).Sum(c => Math.Abs(c.TotalPrice));

                    infoAccount.DecOther = listAll.Where(c => (c.TransType == ReportServiceCode.TRANSFER && c.AccountCode != request.AgentCode)
                    || (c.TransType == ReportServiceCode.CORRECTDOWN)).Sum(c => Math.Abs(c.TotalPrice));
                    infoAccount.Credite = (infoAccount.IncDeposit ?? 0) + (infoAccount.IncOther ?? 0);
                    infoAccount.Debit = (infoAccount.DecPayment ?? 0) + (infoAccount.DecOther ?? 0);

                    if (listAll.Count > 0)
                    {
                        var maxDate = listAll.Max(c => c.CreatedTime);
                        var balance = listAll.Where(c => c.CreatedTime == maxDate).OrderByDescending(c => c.CreatedTime).FirstOrDefault().Balance;
                        infoAccount.BalanceAfter = balance ?? 0;
                    }

                    await _reportMongoRepository.UpdateOneAsync(infoAccount);
                }

                return new MessagePagedResponseBase
                {
                    ResponseCode = ResponseCodeConst.Success,
                    ResponseMessage = "Thành công."
                };
            }
            catch (Exception e)
            {
                _logger.LogError($"SysAgentBalance return: {e.Message}|{e.InnerException}|{e.StackTrace}");
                return new MessagePagedResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error,
                    ResponseMessage = "Thất bại."
                };
            }
        }

        public Task<object> ReportSyncAgentBalanceRequest(SyncAgentBalanceRequest request)
        {
            if (request.Type == 1)
                return Task.FromResult<object>(GetCheckAgentBalance(request));
            else if (request.Type == 2)
            {
                request.FromDate = request.FromDate.Date;
                request.ToDate = request.FromDate.Date.AddDays(1);
                return Task.FromResult<object>(SysAgentBalance(request));
            }
            return Task.FromResult<object>(null);
        }

        public Task<ReportAccountBalanceDay> GetBalanceAgent(string agentCode, DateTime fromDate, DateTime toDate)
        {
            var toTxt = agentCode + "_" + toDate.ToString("yyyyMMdd");
            var fTxt = agentCode + "_" + fromDate.ToString("yyyyMMdd");

            var balance = new ReportAccountBalanceDay()
            {
                AccountCode = agentCode,
            };
            Expression<Func<ReportAccountBalanceDay, bool>> queryAfter = p =>
               p.CurrencyCode == "VND" && p.AccountType == "CUSTOMER"
               && p.AccountCode == agentCode && p.TextDay == toTxt;

            var fAfter = _reportMongoRepository.GetAll<ReportAccountBalanceDay>(queryAfter).FirstOrDefault();
            if (fAfter != null)
            {
                balance.BalanceAfter = fAfter.BalanceAfter;
                if (toTxt == fTxt)
                {
                    balance.BalanceBefore = fAfter.BalanceBefore;
                    return Task.FromResult(balance);
                }
            }
            else
            {
                Expression<Func<ReportAccountBalanceDay, bool>> queryAfter2 = p => p.CurrencyCode == "VND" && p.AccountType == "CUSTOMER"
                && p.AccountCode == agentCode && p.CreatedDay < toDate.Date.AddDays(1).ToUniversalTime();
                fAfter = _reportMongoRepository.GetAll<ReportAccountBalanceDay>(queryAfter2).OrderByDescending(c => c.CreatedDay).FirstOrDefault();
                balance.BalanceAfter = fAfter?.BalanceAfter ?? 0;
                if (fAfter != null && fAfter.TextDay == fTxt)
                {
                    balance.BalanceBefore = fAfter.BalanceBefore;
                    return Task.FromResult(balance);
                }
            }

            Expression<Func<ReportAccountBalanceDay, bool>> queryBefore = p =>
               p.CurrencyCode == "VND" && p.AccountType == "CUSTOMER"
               && p.AccountCode == agentCode && p.TextDay == fTxt;

            var fBefore = _reportMongoRepository.GetAll<ReportAccountBalanceDay>(queryBefore).FirstOrDefault();
            if (fBefore != null)
            {
                balance.BalanceBefore = fBefore.BalanceBefore;
                return Task.FromResult(balance);
            }
            else
            {
                Expression<Func<ReportAccountBalanceDay, bool>> queryBefore2 = p => p.CurrencyCode == "VND" && p.AccountType == "CUSTOMER"
                && p.AccountCode == agentCode && p.CreatedDay <= fromDate.ToUniversalTime();
                fBefore = _reportMongoRepository.GetAll<ReportAccountBalanceDay>(queryBefore2).OrderByDescending(c => c.CreatedDay).FirstOrDefault();
                if (fBefore != null)
                {
                    balance.BalanceBefore = fBefore.TextDay == fTxt ? fBefore.BalanceBefore : fBefore.BalanceAfter;
                    return Task.FromResult(balance);
                }
            }

            return Task.FromResult(balance);
        }

        public async Task<List<ReportItemDetail>> QueryDetailGetList(DateTime date)
        {
            try
            {

                var fromDate = date.Date.ToUniversalTime();
                var toDate = date.Date.AddDays(1).ToUniversalTime();
                Expression<Func<ReportItemDetail, bool>> query = p => true &&
                p.CreatedTime >= fromDate && p.CreatedTime < toDate
                &&
                (p.ServiceCode == ReportServiceCode.TOPUP
                || p.ServiceCode == ReportServiceCode.TOPUP_DATA
                || p.ServiceCode == ReportServiceCode.PIN_CODE
                || p.ServiceCode == ReportServiceCode.PIN_DATA
                || p.ServiceCode == ReportServiceCode.PIN_GAME
                || p.ServiceCode == ReportServiceCode.PAY_BILL
                ) && p.TransType != ReportServiceCode.REFUND;


                var listAll = await _reportMongoRepository.GetAllAsync<ReportItemDetail>(query);
                return listAll;
            }
            catch (Exception e)
            {
                _logger.LogError($"QueryDetailGetList error: {e}");
                return new List<ReportItemDetail>();
            }
        }

        private ReportBalanceHistories GetBalanceHistoryInsert(BalanceHistories request)
        {

            var item = new ReportBalanceHistories
            {
                TransactionType = request.TransType.ToString("G"),
                SrcAccountCode = request.SrcAccountCode,
                DesAccountCode = request.DesAccountCode,
                SrcAccountBalanceAfterTrans = Convert.ToDouble(request.SrcAccountBalance),
                DesAccountBalanceAfterTrans = Convert.ToDouble(request.DesAccountBalance),
                SrcAccountBalanceBeforeTrans = Convert.ToDouble(request.SrcAccountBalanceBeforeTrans),
                DesAccountBalanceBeforeTrans = Convert.ToDouble(request.DesAccountBalanceBeforeTrans),
                TransCode = request.TransRef,
                TransRef = request.TransCode,
                Amount = Convert.ToDouble(request.Amount),
                CurrencyCode = request.CurrencyCode,
                Status = (byte)request.Status,
                ModifiedDate = request.ModifiedDate,
                ModifiedBy = request.ModifiedBy,
                CreatedBy = request.CreatedBy,
                CreatedDate = request.CreatedDate,
                Description = request.Description,
                TransNote = request.TransNote,
                RevertTransCode = request.RevertTransCode,
                TransType = request.TransType
            };
            if (!string.IsNullOrEmpty(item.SrcAccountCode))
                item.SrcAccountType = item.SrcAccountCode.StartsWith("NT9")
                    ? BalanceAccountTypeConst.CUSTOMER
                    : BalanceAccountTypeConst.SYSTEM;

            if (item.TransType == TransactionType.MasterTopup)
            {
                item.SrcAccountBalanceBeforeTrans = 0;
                item.SrcAccountBalanceAfterTrans = 0;
            }

            if (!string.IsNullOrEmpty(item.DesAccountCode))
                item.DesAccountType = item.DesAccountCode.StartsWith("NT9")
                    ? BalanceAccountTypeConst.CUSTOMER
                    : BalanceAccountTypeConst.SYSTEM;


            if (request.TransType == TransactionType.Payment)
            {
                var itemReport = _reportMongoRepository.GetReportItemByTransCode(item.TransRef).Result;
                if (itemReport != null)
                    item.ServiceCode = itemReport.ServiceCode;
            }
            else if (request.TransType == TransactionType.CancelPayment)
                item.ServiceCode = ReportServiceCode.REFUND;
            else if (request.TransType == TransactionType.Deposit)
                item.ServiceCode = ReportServiceCode.DEPOSIT;
            else if (request.TransType == TransactionType.PayBatch)
                item.ServiceCode = ReportServiceCode.PAYBATCH;
            else if (request.TransType == TransactionType.AdjustmentDecrease)
                item.ServiceCode = ReportServiceCode.CORRECTDOWN;
            else if (request.TransType == TransactionType.AdjustmentIncrease)
                item.ServiceCode = ReportServiceCode.CORRECTUP;
            else if (request.TransType == TransactionType.Transfer)
                item.ServiceCode = ReportServiceCode.TRANSFER;
            else if (request.TransType == TransactionType.PayBatch)
                item.ServiceCode = ReportServiceCode.PAYBATCH;
            return item;
        }

        private async Task<ReportItemDetail> ReportItemHistoryBuMessage(ReportBalanceHistories message)
        {
            try
            {
                var account = await GetAccountBackend(message.DesAccountCode);
                var service = await GetServiceBackend(ReportServiceCode.REFUND);
                var item = new ReportItemDetail
                {
                    Id = message.Id,
                    AccountCode = message.DesAccountCode,
                    AccountInfo = account.Mobile + "-" + account.FullName,
                    AccountCityId = account.CityId,
                    AccountCityName = account.CityName,
                    AccountDistrictId = account.DistrictId,
                    AccountDistrictName = account.DistrictName,
                    AccountWardId = account.WardId,
                    AccountWardName = account.WardName,
                    ServiceCode = service.ServiceCode,
                    TransType = service.ServiceCode,
                    ServiceName = service.ServiceName,
                    TransCode = string.Empty,
                    PaidTransCode = message.TransCode,
                    CreatedTime = message.CreatedDate,
                    Amount = Convert.ToDouble(message.Amount),
                    Quantity = 1,
                    Price = Convert.ToDouble(message.Amount),
                    TotalPrice = Convert.ToDouble(message.Amount),
                    PriceIn = Convert.ToDouble(message.Amount),
                    PriceOut = 0,
                    Balance = Convert.ToDouble(message.DesAccountBalanceAfterTrans),
                    PaidAmount = Convert.ToDouble(message.Amount),
                    TransNote = message.TransNote,
                    ExtraInfo = "",
                    Status = ReportStatus.Success,
                    TextDay = message.CreatedDate.Date.ToString("yyyyMMdd"),
                    RequestRef = string.Empty
                };

                item.TransCode = string.Empty;
                item.TransType = ReportServiceCode.REFUND;
                var itemRef = await _reportMongoRepository.GetReportItemByTransCode(message.TransRef);
                if (itemRef != null)
                    item = await ConvertInfoSouceRefund(item, itemRef);

                return item;
            }
            catch (Exception ex)
            {
                _logger.LogError($"ReportItemHistoryBuMessage error: {ex}");
                return null;
            }
        }

        public async Task<List<ReportAccountBalanceDay>> ReportQueryAccountBalanceDayRequest(DateTime date, string currencyCode, string accountCode)
        {
            try
            {
                var toDate = date.Date.AddDays(1);
                Expression<Func<ReportAccountBalanceDay, bool>> query = p =>
                    p.CreatedDay >= date.Date.ToUniversalTime()
                    && p.CreatedDay < toDate.ToUniversalTime();

                if (!string.IsNullOrEmpty(accountCode))
                {
                    Expression<Func<ReportAccountBalanceDay, bool>> newQuery = p =>
                        p.AccountCode == accountCode;
                    query = query.And(newQuery);
                }

                if (!string.IsNullOrEmpty(currencyCode))
                {
                    Expression<Func<ReportAccountBalanceDay, bool>> newQuery = p =>
                        p.CurrencyCode == currencyCode;
                    query = query.And(newQuery);
                }

                var lstSearch = await _reportMongoRepository.GetAllAsync(query);
                return lstSearch;
            }
            catch (Exception ex)
            {
                _logger.LogError($"ReportQueryAccountBalanceDayRequest: {ex.Message}|{ex.StackTrace}|{ex.InnerException}");
                return new List<ReportAccountBalanceDay>();
            }
        }

        public async Task<List<ReportStaffDetail>> ReportQueryStaffDetailRequest(DateTime date, string accountCode)
        {
            try
            {
                var toDate = date.Date.AddDays(1);
                Expression<Func<ReportStaffDetail, bool>> query = p =>
                    p.CreatedTime >= date.Date.ToUniversalTime()
                    && p.CreatedTime < toDate.ToUniversalTime();

                if (!string.IsNullOrEmpty(accountCode))
                {
                    Expression<Func<ReportStaffDetail, bool>> newQuery = p =>
                        p.AccountCode == accountCode;
                    query = query.And(newQuery);
                }

                var lstSearch = await _reportMongoRepository.GetAllAsync(query);
                return lstSearch;
            }
            catch (Exception ex)
            {
                _logger.LogError($"ReportQueryStaffDetailRequest: {ex.Message}|{ex.StackTrace}|{ex.InnerException}");
                return new List<ReportStaffDetail>();
            }
        }

        public async Task<List<ReportCardStockByDate>> ReportQueryCardStockByDateRequest(DateTime date, string stockCode)
        {
            try
            {
                var toDate = date.Date.AddDays(1);
                Expression<Func<ReportCardStockByDate, bool>> query = p =>
                    p.CreatedDate >= date.Date.ToUniversalTime()
                    && p.CreatedDate < toDate.ToUniversalTime();

                if (!string.IsNullOrEmpty(stockCode))
                {
                    Expression<Func<ReportCardStockByDate, bool>> newQuery = p =>
                        p.StockCode == stockCode;
                    query = query.And(newQuery);
                }

                var lstSearch = await _reportMongoRepository.GetAllAsync(query);
                return lstSearch;
            }
            catch (Exception ex)
            {
                _logger.LogError($"ReportQueryCardStockByDateRequest: {ex.Message}|{ex.StackTrace}|{ex.InnerException}");
                return new List<ReportCardStockByDate>();
            }
        }

        public async Task<List<ReportCardStockProviderByDate>> ReportQueryCardStockProviderByDateRequest(DateTime date)
        {
            try
            {
                var toDate = date.Date.AddDays(1);
                Expression<Func<ReportCardStockProviderByDate, bool>> query = p =>
                    p.CreatedDate >= date.Date.ToUniversalTime()
                    && p.CreatedDate < toDate.ToUniversalTime();

                var lstSearch = await _reportMongoRepository.GetAllAsync(query);
                return lstSearch;
            }
            catch (Exception ex)
            {
                _logger.LogError($"ReportQueryCardStockProviderByDateRequest: {ex.Message}|{ex.StackTrace}|{ex.InnerException}");
                return new List<ReportCardStockProviderByDate>();
            }
        }

        public async Task UpdateBalanceByInput(ReportAccountBalanceDay input)
        {
            try
            {
                var infoAccount = _reportMongoRepository.GetReportAccountBalanceDayOpenAsync(input.AccountCode, input.CurrencyCode, input.TextDay).Result;
                if (infoAccount != null)
                {
                    infoAccount.LimitBefore = input.LimitBefore;
                    infoAccount.BalanceBefore = input.BalanceBefore;
                    infoAccount.IncDeposit = input.IncDeposit;
                    infoAccount.IncOther = input.IncOther;
                    infoAccount.Credite = input.Credite;
                    infoAccount.DecPayment = input.DecPayment;
                    infoAccount.DecOther = input.DecOther;
                    infoAccount.Debit = input.Debit;
                    infoAccount.BalanceAfter = input.BalanceAfter;
                    infoAccount.LimitAfter = input.LimitAfter;
                    await _reportMongoRepository.UpdateOneAsync(infoAccount);
                }
            }
            catch (Exception e)
            {
                _logger.LogError($"UpdateBalanceByInput return: {e.Message}|{e.InnerException}|{e.StackTrace}");
            }
        }
    }
}