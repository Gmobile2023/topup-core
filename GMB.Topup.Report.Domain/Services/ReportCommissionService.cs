using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using GMB.Topup.Gw.Model.Events;
using GMB.Topup.Report.Domain.Entities;

using GMB.Topup.Report.Model.Dtos;
using GMB.Topup.Report.Model.Dtos.RequestDto;
using GMB.Topup.Shared;
using Microsoft.Extensions.Logging;

namespace GMB.Topup.Report.Domain.Services
{
    public partial class BalanceReportService
    {
        public Task<MessagePagedResponseBase> ReportCommissionDetailGetList(ReportCommissionDetailRequest request)
        {
            try
            {
                if (request.ToDate != null)
                {
                    request.ToDate = request.ToDate.Value.Date.AddDays(1);
                }

                Expression<Func<ReportItemDetail, bool>> query = p => p.Status == ReportStatus.Success
                                                                    && (p.ServiceCode == ReportServiceCode.TOPUP
                                                                       || p.ServiceCode == ReportServiceCode.TOPUP_DATA
                                                                       || p.ServiceCode == ReportServiceCode.PIN_CODE
                                                                       || p.ServiceCode == ReportServiceCode.PIN_DATA
                                                                       || p.ServiceCode == ReportServiceCode.PIN_GAME
                                                                       || p.ServiceCode == ReportServiceCode.PAY_BILL
                                                                       ) && p.ParentCode != null
                                                                         && p.TransType != ReportServiceCode.REFUND
                                                                         && p.AccountAgentType == 5;


                if (!string.IsNullOrEmpty(request.TransCode))
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.TransCode == request.TransCode
                        || p.CommissionPaidCode == request.TransCode;
                    query = query.And(newQuery);
                }

                var serviceCode = new List<string>();
                var categoryCode = new List<string>();
                var productCode = new List<string>();
                if (request.ServiceCode != null && request.ServiceCode.Count > 0)
                {
                    serviceCode.AddRange(request.ServiceCode.Where(a => !string.IsNullOrEmpty(a)));
                }

                if (request.CategoryCode != null && request.CategoryCode.Count > 0)
                {
                    categoryCode.AddRange(request.CategoryCode.Where(a => !string.IsNullOrEmpty(a)));
                }

                if (request.ProductCode != null && request.ProductCode.Count > 0)
                {
                    productCode.AddRange(request.ProductCode.Where(a => !string.IsNullOrEmpty(a)));
                }



                if (serviceCode.Count > 0)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                       serviceCode.Contains(p.ServiceCode);
                    query = query.And(newQuery);
                }


                if (categoryCode.Count > 0)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                       categoryCode.Contains(p.CategoryCode);
                    query = query.And(newQuery);
                }

                if (productCode.Count > 0)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                       productCode.Contains(p.ProductCode);
                    query = query.And(newQuery);
                }

                if (!string.IsNullOrEmpty(request.AgentCode))
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.AccountCode == request.AgentCode;
                    query = query.And(newQuery);
                }

                if (!string.IsNullOrEmpty(request.AgentCodeSum))
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.ParentCode == request.AgentCodeSum;
                    query = query.And(newQuery);
                }

                if (!string.IsNullOrEmpty(request.Filter))
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.RequestRef.Contains(request.Filter)
                        || p.TransCode.Contains(request.Filter)
                        || p.CommissionPaidCode.Contains(request.Filter);
                    query = query.And(newQuery);
                }

                if (request.Status >= 0)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.CommissionStatus == request.Status;
                    query = query.And(newQuery);
                }

                if (request.FromDate != null)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.CreatedTime >= request.FromDate.Value.ToUniversalTime();
                    query = query.And(newQuery);
                }

                if (request.ToDate != null)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.CreatedTime < request.ToDate.Value.ToUniversalTime();
                    query = query.And(newQuery);
                }


                var listSouces = _reportMongoRepository.GetAll<ReportItemDetail>(query);
                var total = listSouces.Count();
                var lst = listSouces.OrderByDescending(c => c.CreatedTime).Skip(request.Offset).Take(request.Limit);
                var list = (from g in lst
                            select new ReportCommissionDetailDto()
                            {
                                AgentSumCode = g.ParentCode,
                                AgentSumInfo = g.ParentName,
                                CommissionAmount = g.CommissionAmount ?? 0,
                                CommissionCode = g.CommissionPaidCode,
                                StatusName = g.CommissionStatus == 1 ? "Đã trả" : "Chưa trả",
                                Status = g.CommissionStatus ?? 0,
                                AgentCode = g.AccountCode,
                                AgentInfo = g.AccountInfo,
                                RequestRef = g.RequestRef,
                                TransCode = g.TransCode,
                                ServiceCode = g.ServiceCode,
                                ServiceName = g.ServiceName,
                                CategoryName = g.CategoryName,
                                ProductName = g.ProductName,
                                CreateDate = g.CreatedTime,
                                PayDate = g.CommissionDate,

                            }).OrderByDescending(c => c.CreateDate).ToList();


                var sumTotal = new ReportCommissionDetailDto()
                {
                    CommissionAmount =listSouces.Sum(c => c.CommissionAmount ?? 0),
                };


                return Task.FromResult(new MessagePagedResponseBase
                {
                    ResponseCode = "01",
                    ResponseMessage = "Thành công",
                    Total = (int)total,
                    SumData = sumTotal,
                    Payload = list,
                });
            }
            catch (Exception e)
            {
                _logger.LogError($"ReportCommissionDetailGetList error: {e}");
                return Task.FromResult(new MessagePagedResponseBase
                {
                    ResponseCode = "00"
                });
            }
        }

        public Task<MessagePagedResponseBase> ReportCommissionTotalGetList(ReportCommissionTotalRequest request)
        {
            try
            {
                if (request.ToDate != null)
                {
                    request.ToDate = request.ToDate.Value.Date.AddDays(1);
                }

                Expression<Func<ReportItemDetail, bool>> query = p => p.Status == ReportStatus.Success &&
                                                                       (p.ServiceCode == ReportServiceCode.TOPUP
                                                                       || p.ServiceCode == ReportServiceCode.TOPUP_DATA
                                                                       || p.ServiceCode == ReportServiceCode.PIN_CODE
                                                                       || p.ServiceCode == ReportServiceCode.PIN_DATA
                                                                       || p.ServiceCode == ReportServiceCode.PIN_GAME
                                                                       || p.ServiceCode == ReportServiceCode.PAY_BILL
                                                                       ) && p.ParentCode != null
                                                                         && p.TransType != ReportServiceCode.REFUND
                                                                         && p.AccountAgentType == 5;



                if (!string.IsNullOrEmpty(request.AgentCode))
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.ParentCode == request.AgentCode;
                    query = query.And(newQuery);
                }



                if (request.FromDate != null)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.CreatedTime >= request.FromDate.Value.ToUniversalTime();
                    query = query.And(newQuery);
                }

                if (request.ToDate != null)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.CreatedTime < request.ToDate.Value.ToUniversalTime();
                    query = query.And(newQuery);
                }


                var listSouces = _reportMongoRepository.GetAll<ReportItemDetail>(query);

                var list = (from x in listSouces
                            group x by new { x.ParentCode, x.ParentName } into g
                            select new ReportCommissionTotalDto()
                            {
                                AgentCode = g.Key.ParentCode,
                                AgentName = g.Key.ParentName,
                                Quantity = g.Count(),
                                CommissionAmount = g.Sum(c => c.CommissionAmount ?? 0),
                                Payment = g.Where(c => c.CommissionStatus == 1).Sum(c => c.CommissionAmount ?? 0),
                                UnPayment = g.Where(c => c.CommissionStatus == 0).Sum(c => c.CommissionAmount ?? 0)
                            }).ToList();

                int total = list.Count();
                var lst = list.OrderBy(c => c.AgentCode).Skip(request.Offset).Take(request.Limit);
                var sumTotal = new ReportCommissionTotalDto()
                {
                    Quantity = list.Sum(c => c.Quantity),
                    CommissionAmount = list.Sum(c => c.CommissionAmount),
                    Payment = list.Sum(c => c.Payment),
                    UnPayment = list.Sum(c => c.UnPayment),
                };


                return Task.FromResult(new MessagePagedResponseBase
                {
                    ResponseCode = "01",
                    ResponseMessage = "Thành công",
                    Total = (int)total,
                    SumData = sumTotal,
                    Payload = lst,
                });
            }
            catch (Exception e)
            {
                _logger.LogError($"ReportCommissionTotalGetList error: {e}");
                return Task.FromResult(new MessagePagedResponseBase
                {
                    ResponseCode = "00"
                });
            }
        }

        public Task<MessagePagedResponseBase> ReportCommissionAgentDetailGetList(ReportCommissionAgentDetailRequest request)
        {
            try
            {
                if (request.ToDate != null)
                {
                    request.ToDate = request.ToDate.Value.Date.AddDays(1);
                }

                Expression<Func<ReportItemDetail, bool>> query = p => (p.ServiceCode == ReportServiceCode.TOPUP
                                                                       || p.ServiceCode == ReportServiceCode.TOPUP_DATA
                                                                       || p.ServiceCode == ReportServiceCode.PIN_CODE
                                                                       || p.ServiceCode == ReportServiceCode.PIN_DATA
                                                                       || p.ServiceCode == ReportServiceCode.PIN_GAME
                                                                       || p.ServiceCode == ReportServiceCode.PAY_BILL
                                                                      ) && p.ParentCode == request.LoginCode
                                                                        && p.TransType != ReportServiceCode.REFUND
                                                                        && p.AccountAgentType == 5; ;


                if (!string.IsNullOrEmpty(request.TransCode))
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.TransCode == request.TransCode
                        || p.RequestRef == request.TransCode
                        || p.CommissionPaidCode == request.TransCode;
                    query = query.And(newQuery);
                }

                var serviceCode = new List<string>();
                var categoryCode = new List<string>();
                var productCode = new List<string>();

                if (serviceCode.Count > 0)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                       serviceCode.Contains(p.ServiceCode);
                    query = query.And(newQuery);
                }


                if (categoryCode.Count > 0)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                         categoryCode.Contains(p.CategoryCode);
                    query = query.And(newQuery);
                }

                if (productCode.Count > 0)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                       productCode.Contains(p.ProductCode);
                    query = query.And(newQuery);
                }

                if (!string.IsNullOrEmpty(request.AgentCode))
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.AccountCode == request.AgentCode;
                    query = query.And(newQuery);
                }

                if (request.Status > 0)
                {
                    if (request.Status == 1)
                    {
                        Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                          p.Status == ReportStatus.Success;
                        query = query.And(newQuery);
                    }
                    else if (request.Status == 3)
                    {
                        Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                         p.Status == ReportStatus.Error;
                        query = query.And(newQuery);
                    }
                    else
                    {
                        Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        (p.Status == ReportStatus.TimeOut || p.Status == ReportStatus.Process);
                        query = query.And(newQuery);
                    }

                }

                if (request.StatusPayment >= 0)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.CommissionStatus == request.StatusPayment;
                    query = query.And(newQuery);
                }

                if (request.FromDate != null)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.CreatedTime >= request.FromDate.Value.ToUniversalTime();
                    query = query.And(newQuery);
                }

                if (request.ToDate != null)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.CreatedTime < request.ToDate.Value.ToUniversalTime();
                    query = query.And(newQuery);
                }


                var listSouces = _reportMongoRepository.GetAll<ReportItemDetail>(query);
                var total = listSouces.Count();
                var lst = listSouces.OrderByDescending(c => c.CreatedTime).Skip(request.Offset).Take(request.Limit);
                var list = (from g in lst
                            select new ReportCommissionAgentDetailDto()
                            {
                                CommissionAmount = g.CommissionAmount ?? 0,
                                CommissionCode = g.CommissionPaidCode,
                                Status = g.Status == ReportStatus.Success
                                    ? 1
                                    : (g.Status == ReportStatus.TimeOut || g.Status == ReportStatus.Process)
                                        ? 2
                                        : 3,
                                StatusName = g.Status == ReportStatus.Success
                                    ? "Thành công"
                                    : (g.Status == ReportStatus.TimeOut || g.Status == ReportStatus.Process)
                                        ? "Chưa có kết quả"
                                        : "Lỗi",
                                AgentCode = g.AccountCode,
                                AgentInfo = g.AccountInfo,
                                RequestRef = g.RequestRef,
                                TransCode = g.TransCode,
                                ServiceCode = g.ServiceCode,
                                ServiceName = g.ServiceName,
                                CategoryName = g.CategoryName,
                                ProductName = g.ProductName,
                                CreateDate = g.CreatedTime,
                                PayDate = g.CommissionDate,
                                Amount = g.Amount,
                                Discount = g.Discount,
                                Fee = g.Fee,
                                Price = g.Price,
                                TotalPrice =g.TotalPrice,
                                Quantity = g.Quantity,
                                StatusPayment = g.CommissionStatus ?? 0,
                                StatusPaymentName = g.CommissionStatus == 1 ? "Đã trả" : "Chưa trả",

                            }).OrderByDescending(c => c.CreateDate).ToList();


                var sumTotal = new ReportCommissionAgentDetailDto()
                {
                    Quantity = listSouces.Sum(c => c.Quantity),
                    CommissionAmount = listSouces.Sum(c => c.CommissionAmount ?? 0),
                    Amount = listSouces.Sum(c => c.Amount),
                    TotalPrice = listSouces.Sum(c => c.TotalPrice),
                };


                return Task.FromResult(new MessagePagedResponseBase
                {
                    ResponseCode = "01",
                    ResponseMessage = "Thành công",
                    Total = (int)total,
                    SumData = sumTotal,
                    Payload = list,
                });
            }
            catch (Exception e)
            {
                _logger.LogError($"ReportCommissionAgentDetailGetList error: {e}");
                return Task.FromResult(new MessagePagedResponseBase
                {
                    ResponseCode = "00"
                });
            }
        }

        public Task<MessagePagedResponseBase> ReportCommissionAgentTotalGetList(ReportCommissionAgentTotalRequest request)
        {
            try
            {
                if (request.ToDate != null)
                    request.ToDate = request.ToDate.Value.Date.AddDays(1);

                var fromDateLimit = request.FromDate.Value.AddDays(-35);
                Expression<Func<ReportAccountBalanceDay, bool>> query = p => true
                                                                             && p.CurrencyCode == "VND" &&
                                                                             p.AccountType == "CUSTOMER" &&
                                                                             p.CreatedDay >= fromDateLimit.ToUniversalTime() &&
                                                                             p.CreatedDay < request.ToDate.Value.ToUniversalTime() &&
                                                                             p.ParentCode == request.LoginCode
                                                                             && p.AgentType == 5;

                if (!string.IsNullOrEmpty(request.AgentCode))
                {
                    Expression<Func<ReportAccountBalanceDay, bool>> newQuery = p => p.AccountCode == request.AgentCode;
                    query = query.And(newQuery);
                }

                var listSouces = _reportMongoRepository.GetAll<ReportAccountBalanceDay>(query);

                #region 1.Đầu kỳ
                var listBefore = listSouces.Where(c => c.CreatedDay < request.FromDate.Value.ToUniversalTime());

                var listGroupBefore = from x in listBefore
                                      group x by new { x.AccountCode } into g
                                      select new ReportAgentBalanceTemp()
                                      {
                                          AgentCode = g.Key.AccountCode,
                                          MaxDate = g.Max(c => c.CreatedDay),
                                      };

                var listViewBefore = from x in listGroupBefore
                                     join yc in listBefore on x.AgentCode equals yc.AccountCode
                                     where x.MaxDate == yc.CreatedDay
                                     select new ReportAgentBalanceTemp()
                                     {
                                         AgentCode = x.AgentCode,
                                         BeforeAmount = yc.BalanceAfter,
                                     };

                #endregion

                #region 2.Cuối kỳ


                var listGroupAfter = from x in listSouces
                                     group x by new { x.AccountCode } into g
                                     select new ReportAgentBalanceTemp()
                                     {
                                         AgentCode = g.Key.AccountCode,
                                         MaxDate = g.Max(c => c.CreatedDay),
                                     };

                var listViewAfter = from x in listGroupAfter
                                    join yc in listSouces on x.AgentCode equals yc.AccountCode
                                    where x.MaxDate == yc.CreatedDay
                                    select new ReportAgentBalanceTemp()
                                    {
                                        AgentCode = x.AgentCode,
                                        AgentInfo = yc.AccountInfo,
                                        AfterAmount = yc.BalanceAfter,
                                    };


                #endregion

                var listKy = listSouces.Where(c => c.CreatedDay >= request.FromDate.Value.ToUniversalTime());


                var listGroupKy = (from x in listKy
                                   group x by x.AccountCode into g
                                   select new ReportAgentBalanceTemp
                                   {
                                       AgentCode = g.Key,
                                       InputAmount = Math.Round(g.Sum(c => c.IncDeposit ?? 0), 0),
                                       AmountUp = Math.Round(g.Sum(c => c.IncOther ?? 0), 0),
                                       SaleAmount = Math.Round(g.Sum(c => c.DecPayment ?? 0), 0),
                                       AmountDown = Math.Round(g.Sum(c => c.DecOther ?? 0), 0)
                                   }).ToList();




                var listView = (from c in listViewAfter
                                join k in listGroupKy on c.AgentCode equals k.AgentCode into gk
                                from ky in gk.DefaultIfEmpty()
                                join d in listViewBefore on c.AgentCode equals d.AgentCode into gd
                                from before in gd.DefaultIfEmpty()
                                select new ReportCommissionAgentTotalDto()
                                {
                                    AgentCode = c.AgentCode,
                                    AgentName = c.AgentInfo,
                                    Before = before?.BeforeAmount ?? 0,
                                    AmountUp = ky != null ? ky.InputAmount + ky.AmountUp : 0,
                                    AmountDown = ky != null ? ky.SaleAmount + ky.AmountDown : 0,
                                    After = c.AfterAmount,
                                }).ToList();




                var total = listView.Count;
                var sumTotal = new ReportCommissionAgentTotalDto()
                {
                };

                var lst = listView.OrderBy(c => c.AgentCode).Skip(request.Offset).Take(request.Limit).ToList();

                return Task.FromResult(new MessagePagedResponseBase
                {
                    ResponseCode = "01",
                    ResponseMessage = "Thành công",
                    Total = total,
                    SumData = sumTotal,
                    Payload = lst,
                });
            }
            catch (Exception e)
            {
                _logger.LogError($"ReportCommissionAgentTotalGetList error: {e}");
                return Task.FromResult(new MessagePagedResponseBase
                {
                    ResponseCode = "00"
                });
            }
        }

        public async Task<MessagePagedResponseBase> ReportAgentGeneralDayGetDash(
         ReportAgentGeneralDashRequest request)
        {
            try
            {
                if (request.ToDate != null)
                {
                    request.ToDate = request.ToDate.Value.Date.AddDays(1).AddSeconds(-1);
                }

                Expression<Func<ReportItemDetail, bool>> query = p => true && p.Status == ReportStatus.Success &&
                                                                      (p.ServiceCode == ReportServiceCode.TOPUP
                                                                       || p.ServiceCode == ReportServiceCode.TOPUP_DATA
                                                                       || p.ServiceCode == ReportServiceCode.PIN_CODE
                                                                       || p.ServiceCode == ReportServiceCode.PIN_DATA
                                                                       || p.ServiceCode == ReportServiceCode.PIN_GAME
                                                                       || p.ServiceCode == ReportServiceCode.PAY_BILL
                                                                      ) && p.TransType != ReportServiceCode.REFUND
                                                                        && p.ParentCode == request.LoginCode
                                                                        && p.AccountAgentType == 5;




                var serviceCode = new List<string>();
                var categoryCode = new List<string>();
                var productCode = new List<string>();
                if (request.ServiceCode != null && request.ServiceCode.Count > 0)
                {
                    serviceCode.AddRange(request.ServiceCode.Where(a => !string.IsNullOrEmpty(a)));
                }

                if (request.CategoryCode != null && request.CategoryCode.Count > 0)
                {
                    categoryCode.AddRange(request.CategoryCode.Where(a => !string.IsNullOrEmpty(a)));
                }

                if (request.ProductCode != null && request.ProductCode.Count > 0)
                {
                    productCode.AddRange(request.ProductCode.Where(a => !string.IsNullOrEmpty(a)));
                }


                if (serviceCode.Count > 0)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                       serviceCode.Contains(p.ServiceCode);
                    query = query.And(newQuery);
                }

                if (categoryCode.Count > 0)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        categoryCode.Contains(p.CategoryCode);
                    query = query.And(newQuery);
                }

                if (productCode.Count > 0)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        productCode.Contains(p.ProductCode);
                    query = query.And(newQuery);
                }

                if (!string.IsNullOrEmpty(request.AgentCode))
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.AccountCode == request.AgentCode;
                    query = query.And(newQuery);
                }


                if (request.FromDate != null)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.CreatedTime >= request.FromDate.Value.ToUniversalTime();
                    query = query.And(newQuery);
                }

                if (request.ToDate != null)
                {
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                        p.CreatedTime <= request.ToDate.Value.ToUniversalTime();
                    query = query.And(newQuery);
                }

                var lstSearch = await _reportMongoRepository.GetAllAsync(query);
                var detailList = from x in lstSearch
                                 select new ReportRevenueCommistionDashDay
                                 {
                                     CreatedDay = _dateHepper.ConvertToUserTime(x.CreatedTime, DateTimeKind.Utc).Date,
                                     Revenue = Convert.ToDecimal(x.Amount),
                                     Commission = Convert.ToDecimal(x.CommissionAmount ?? 0)
                                 };

                var list = (from x in detailList
                            group x by x.CreatedDay
                    into g
                            select new ReportRevenueCommistionDashDay()
                            {
                                CreatedDay = g.Key,
                                DayText = g.Key.ToString("dd-MM-yyyy"),
                                Revenue = g.Sum(c => c.Revenue),
                                Commission = g.Sum(c => c.Commission)
                            }).OrderByDescending(c => c.CreatedDay).ToList();

                var tempDate = request.FromDate.Value.Date;
                var toDate = request.ToDate.Value.Date;

                while (tempDate <= toDate)
                {
                    if (list.Where(c => c.CreatedDay == tempDate).Count() == 0)
                    {
                        list.Add(new ReportRevenueCommistionDashDay()
                        {
                            CreatedDay = tempDate,
                            DayText = tempDate.ToString("dd-MM-yyyy"),
                            Commission = 0,
                            Revenue = 0,
                        });
                    }

                    tempDate = tempDate.AddDays(1);
                }

                var total = list.Count();
                var sumtotal = new ReportRevenueCommistionDashDay()
                {
                    Revenue = list.Sum(c => c.Revenue),
                    Commission = list.Sum(c => c.Commission)
                };

                var lst = list.OrderByDescending(c => c.CreatedDay).Skip(request.Offset).Take(request.Limit).ToList();

                return new MessagePagedResponseBase
                {
                    ResponseCode = "01",
                    ResponseMessage = "Thành công",
                    Total = (int)total,
                    SumData = sumtotal,
                    Payload = lst,
                };
            }
            catch (Exception e)
            {
                _logger.LogError($"ReportAgentGeneralDayGetDash error: {e}");
                return new MessagePagedResponseBase
                {
                    ResponseCode = "00"
                };
            }
        }

        private string GetAgenTypeName(int agenType)
        {
            switch (agenType)
            {
                case 1:
                    return "Đại lý";
                case 2:
                    return "Đại lý API";
                case 3:
                    return "Đại lý công ty";
                case 4:
                    return "Đại lý Tổng";
                case 5:
                    return "Đại lý cấp 1";
                case 6:
                    return "Đại lý sỉ";
                default:
                    return "Đại lý";
            }
        }

        private ReportBalanceHistories GetBalanceHistoryInsert(ReportBalanceHistoriesMessage request)
        {
            var transaction = request.Transaction;
            var settlement = request.Settlement;
            var item = new ReportBalanceHistories
            {
                TransactionType = transaction.TransType.ToString("G"),
                SrcAccountCode = settlement.SrcAccountCode,
                DesAccountCode = settlement.DesAccountCode,
                SrcAccountBalanceAfterTrans = Convert.ToDouble(settlement.SrcAccountBalance),
                DesAccountBalanceAfterTrans = Convert.ToDouble(settlement.DesAccountBalance),
                SrcAccountBalanceBeforeTrans = Convert.ToDouble(settlement.SrcAccountBalanceBeforeTrans),
                DesAccountBalanceBeforeTrans = Convert.ToDouble(settlement.DesAccountBalanceBeforeTrans),
                TransCode = transaction.TransactionCode,
                TransRef = transaction.TransRef,
                Amount = Convert.ToDouble(transaction.Amount),
                CurrencyCode = transaction.CurrencyCode,
                Status = transaction.Status,
                ModifiedDate = transaction.ModifiedDate,
                ModifiedBy = transaction.ModifiedBy,
                CreatedBy = transaction.CreatedBy,
                CreatedDate = transaction.CreatedDate,
                Description = transaction.Description,
                TransNote = transaction.TransNote,
                RevertTransCode = transaction.RevertTransCode,
                TransType = transaction.TransType
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


            if (request.Transaction.TransType == TransactionType.Payment)
            {
                item.ServiceCode = TransactionType.Payment.ToString();
                //var itemReport = _reportMongoRepository.GetReportItemByTransCode(item.TransRef).Result;              
                //if (itemReport != null)
                //    item.ServiceCode = itemReport.ServiceCode;
            }
            else if (request.Transaction.TransType == TransactionType.CancelPayment)
                item.ServiceCode = ReportServiceCode.REFUND;
            else if (request.Transaction.TransType == TransactionType.Deposit 
                || request.Transaction.TransType == TransactionType.SaleDeposit)
                item.ServiceCode = ReportServiceCode.DEPOSIT;
            else if (request.Transaction.TransType == TransactionType.PayBatch)
                item.ServiceCode = ReportServiceCode.PAYBATCH;
            else if (request.Transaction.TransType == TransactionType.AdjustmentDecrease)
                item.ServiceCode = ReportServiceCode.CORRECTDOWN;
            else if (request.Transaction.TransType == TransactionType.AdjustmentIncrease)
                item.ServiceCode = ReportServiceCode.CORRECTUP;
            else if (request.Transaction.TransType == TransactionType.Transfer)
                item.ServiceCode = ReportServiceCode.TRANSFER;
            else if (request.Transaction.TransType == TransactionType.PayBatch)
                item.ServiceCode = ReportServiceCode.PAYBATCH;
            else if (request.Transaction.TransType == TransactionType.PayCommission)
                item.ServiceCode = ReportServiceCode.PAYCOMMISSION;         

            return item;
        }
        public async Task SaveBalanceHistorySouce(BalanceHistories request)
        {
            Expression<Func<ReportBalanceHistories, bool>> query = p => p.TransCode == request.TransCode;
            var lstOne = await _reportMongoRepository.GetOneAsync(query);
            if (lstOne != null)
                return;

            var item = new ReportBalanceHistories
            {
                TransactionType = request.TransType.ToString("G"),
                SrcAccountCode = request.SrcAccountCode,
                DesAccountCode = request.DesAccountCode,
                SrcAccountBalanceAfterTrans = Convert.ToDouble(request.SrcAccountBalance),
                DesAccountBalanceAfterTrans = Convert.ToDouble(request.DesAccountBalance),
                SrcAccountBalanceBeforeTrans = Convert.ToDouble(request.SrcAccountBalanceBeforeTrans),
                DesAccountBalanceBeforeTrans = Convert.ToDouble(request.DesAccountBalanceBeforeTrans),
                TransCode = request.TransCode,
                TransRef = request.TransRef,
                Amount = Convert.ToDouble(request.Amount),
                CurrencyCode = request.CurrencyCode,
                Status = Convert.ToByte(request.Status),
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
                var itemReport = await _reportMongoRepository.GetReportItemByTransCode(item.TransRef);

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
            else if (request.TransType == TransactionType.PayCommission)
                item.ServiceCode = ReportServiceCode.PAYCOMMISSION;

            await _reportMongoRepository.AddOneAsync(item);
        }
    }
}