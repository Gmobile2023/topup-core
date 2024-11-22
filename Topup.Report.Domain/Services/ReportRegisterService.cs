using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Topup.Report.Model.Dtos;
using Topup.Report.Model.Dtos.RequestDto;
using Topup.Shared;
using Microsoft.Extensions.Logging;
using Nest;
using NPOI.SS.Formula.Functions;
using Topup.Discovery.Requests.Reports;
using ServiceStack;
using ServiceStack.Caching;
using Topup.Report.Domain.Entities;
using Topup.Report.Domain.Repositories;

namespace Topup.Report.Domain.Services;

public partial class BalanceReportService
{
    public async Task<List<ReportCardStockDayDto>> CardStockDateAuto(CardStockAutoRequest request)
    {
        try
        {
            Expression<Func<ReportCardStockByDate, bool>> query = p => true
                                                                       && p.CreatedDate <=
                                                                       DateTime.Now.ToUniversalTime();

            var lst = await _reportMongoRepository.GetAllAsync(query);
            var listSale = await new ConvertDataRepository().FillterStock(lst.Where(c => c.StockCode == "STOCK_SALE").ToList(), request.FromDate, request.ToDate);
            var listTemp = await new ConvertDataRepository().FillterStock(lst.Where(c => c.StockCode == "STOCK_TEMP").ToList(), request.FromDate, request.ToDate);
            var productCodes = lst.Select(c => c.ProductCode).Distinct().ToList();
            Expression<Func<ReportProductDto, bool>> queryProduct = p => productCodes.Contains(p.ProductCode);
            var lstProduct = await _reportMongoRepository.GetAllAsync(queryProduct);
            var mView = from p in lstProduct
                        join s in listSale on p.ProductCode equals s.ProductCode into sg
                        from sale in sg.DefaultIfEmpty()
                        join t in listTemp on p.ProductCode equals t.ProductCode into tg
                        from temp in tg.DefaultIfEmpty()
                        select new ReportCardStockDayDto
                        {
                            ServiceName = p.ServiceName,
                            ProductCode = p.ProductCode,
                            ProductName = p.ProductName,
                            CategoryName = p.CategoryName,
                            CardValue = Convert.ToInt32(p.ProductValue),
                            Before_Sale = sale != null ? Convert.ToInt32(sale.InventoryBefore.ToString()) : 0,
                            Import_Sale = sale != null
                                ? Convert.ToInt32((sale.IncreaseSupplier + sale.IncreaseOther).ToString())
                                : 0,
                            Export_Sale = sale != null ? Convert.ToInt32((sale.ExportOther + sale.Sale).ToString()) : 0,
                            After_Sale = sale != null ? Convert.ToInt32(sale.InventoryAfter.ToString()) : 0,
                            Monney_Sale = sale != null ? Convert.ToDecimal(sale.InventoryAfter * sale.CardValue) : 0,
                            Before_Temp = temp != null ? Convert.ToInt32(temp.InventoryBefore.ToString()) : 0,
                            Import_Temp = temp != null
                                ? Convert.ToInt32((temp.IncreaseSupplier + temp.IncreaseOther).ToString())
                                : 0,
                            Export_Temp = temp != null ? Convert.ToInt32((temp.ExportOther + temp.Sale).ToString()) : 0,
                            After_Temp = temp != null ? Convert.ToInt32(temp.InventoryAfter.ToString()) : 0,
                            Monney_Temp = temp != null ? Convert.ToDecimal(temp.InventoryAfter * temp.CardValue) : 0
                        };

            return mView.ToList();
        }
        catch (Exception ex)
        {
            //_logger.Log($"CardStockDateAuto error {ex}");
            return new List<ReportCardStockDayDto>();
        }
    }

    public async Task<List<ReportRevenueTotalAutoDto>> ReportTotal0hDateAuto(ReportTotalAuto0hRequest request)
    {
        try
        {
            Expression<Func<ReportAccountBalanceDay, bool>> query = p =>
                p.CurrencyCode == "VND" && p.AccountType == "CUSTOMER"
                                        && p.CreatedDay <= request.ToDate.ToUniversalTime()
                                        && p.CreatedDay >= request.FromDate.ToUniversalTime();

            var lst = await _reportMongoRepository.GetAllAsync(query);
            Expression<Func<ReportAccountDto, bool>> queryAccount = p => p.CreationTime != null;
            var lstAccount = await _reportMongoRepository.GetAllAsync(queryAccount);
            lstAccount.ForEach(c =>
            {
                c.CreationTime = _dateHepper.ConvertToUserTime(c.CreationTime.Value, DateTimeKind.Utc).Date;
            });

            var dayActives = (from x in lstAccount
                              group x by x.CreationTime
                into g
                              select new ReportRevenueTotalAutoDto
                              {
                                  CreatedDay = g.Key.Value,
                                  AccountActive = g.Count()
                              }).ToList();


            var list = (from x in lst
                        group x by new { x.AccountCode, x.CreatedDay }
                into g
                        select new ReportRevenueTotalAutoDto
                        {
                            AccountActive = 0,
                            AccountRevenue = 0,
                            CreatedDay = _dateHepper.ConvertToUserTime(g.Key.CreatedDay, DateTimeKind.Utc).Date,
                            Before = g.Sum(c => c.BalanceBefore),
                            After = g.Sum(c => c.BalanceAfter),
                            InputDeposit = g.Sum(c => c.IncDeposit ?? 0),
                            IncOther = g.Sum(c => c.IncOther ?? 0),
                            Sale = g.Sum(c => c.DecPayment ?? 0),
                            DecOther = g.Sum(c => c.DecOther ?? 0),
                            AccountCode = g.Key.AccountCode
                        }).ToList();

            list.ForEach(c =>
            {
                if (c.InputDeposit > 0 && c.Sale > 0)
                    c.AccountRevenue = 1;
            });


            var litView = (from x in list
                           group x by x.CreatedDay
                into g
                           select new ReportRevenueTotalAutoDto
                           {
                               CreatedDay = g.Key,
                               AccountActive = g.Sum(c => c.AccountActive),
                               AccountRevenue = g.Sum(c => c.AccountRevenue),
                               Before = Math.Round(g.Sum(c => c.Before), 0),
                               InputDeposit = Math.Round(g.Sum(c => c.InputDeposit), 0),
                               IncOther = Math.Round(g.Sum(c => c.IncOther), 0),
                               Sale = Math.Round(g.Sum(c => c.Sale), 0),
                               DecOther = Math.Round(g.Sum(c => c.DecOther), 0),
                               After = Math.Round(g.Sum(c => c.After), 0),
                               AccountCode = string.Empty
                           }).OrderByDescending(c => c.CreatedDay).ToList();


            var viewLst = (from x in litView
                           join d in dayActives on x.CreatedDay equals d.CreatedDay into g
                           from u in g.DefaultIfEmpty()
                           select new ReportRevenueTotalAutoDto
                           {
                               AccountActive = u?.AccountActive ?? 0,
                               AccountRevenue = x.AccountRevenue,
                               CreatedDay = x.CreatedDay,
                               Before = x.Before,
                               After = x.After,
                               InputDeposit = x.InputDeposit,
                               IncOther = x.IncOther,
                               Sale = x.Sale,
                               DecOther = x.DecOther,
                               AccountCode = x.AccountCode
                           }).OrderByDescending(c => c.CreatedDay).ToList();

            return viewLst.ToList();
        }
        catch (Exception ex)
        {
            return new List<ReportRevenueTotalAutoDto>();
        }
    }

    public async Task<List<ReportBalanceSupplierDto>> ReportBalanceSupplierAuto(ReportBalanceSupplierRequest request)
    {
        try
        {
            Expression<Func<ReportBalanceSupplierDay, bool>> query = p =>
                p.CreatedDay <= request.ToDate.ToUniversalTime()
                && p.CreatedDay >= request.FromDate.ToUniversalTime();

            var lst = await _reportMongoRepository.GetAllAsync(query);
            var list = (from x in lst
                        select new ReportBalanceSupplierDay
                        {
                            CreatedDay = _dateHepper.ConvertToUserTime(x.CreatedDay, DateTimeKind.Utc).Date,
                            SupplierName = x.SupplierName,
                            SupplierCode = x.SupplierCode,
                            Balance = x.Balance
                        }).ToList();

            var days = list.Select(c => c.CreatedDay).Distinct().OrderByDescending(c => c).ToList();
            var fDate = request.FromDate;
            var tDate = request.ToDate;
            while (fDate <= tDate)
            {
                var fCount = days.Where(c => c == fDate.Date).Count();
                if (fCount == 0)
                    days.Add(fDate.Date);

                fDate = fDate.AddDays(1);
            }

            var dtoLit = new List<ReportBalanceSupplierDto>();
            var providers = request.Providers.Split(',', ';', '|');
            foreach (var item in days)
            {
                var msgLit = list.Where(c => c.CreatedDay == item).OrderBy(c => c.SupplierCode).ToArray();
                var dto = new ReportBalanceSupplierDto
                {
                    CreatedDay = item,
                    Items = new List<BalanceSupplierItem>()
                };
                foreach (var p in providers)
                {
                    var detail = msgLit.FirstOrDefault(c => c.SupplierCode == p);
                    if (detail != null)
                    {
                        dto.Items.Add(new BalanceSupplierItem
                        {
                            Name = detail.SupplierCode,
                            Balance = detail.Balance
                        });
                    }
                    else
                    {
                        dto.Items.Add(new BalanceSupplierItem
                        {
                            Name = p,
                            Balance = 0
                        });
                    }

                }

                dtoLit.Add(dto);
            }

            return dtoLit.ToList();
        }
        catch (Exception ex)
        {
            return new List<ReportBalanceSupplierDto>();
        }
    }

    public async Task<List<ReportSmsDto>> ReportSmsAuto(ReportSmsRequest request)
    {
        try
        {
            Expression<Func<SmsMessage, bool>> query = p =>
                p.CreatedDate <= request.ToDate.ToUniversalTime()
                && p.CreatedDate >= request.FromDate.ToUniversalTime();

            var lst = await _reportMongoRepository.GetAllAsync(query);
            var list = (from x in lst
                        select new ReportSmsDto
                        {
                            CreatedDate = _dateHepper.ConvertToUserTime(x.CreatedDate, DateTimeKind.Utc).Date,
                            Phone = x.PhoneNumber,
                            Message = x.Message,
                            TransCode = x.TransCode,
                            Channel = x.SmsChannel,
                            Status = x.Status == 1 ? 1 : 0,
                            Result = x.Result
                        }).OrderBy(c => c.CreatedDate).ToList();

            return list.ToList();
        }
        catch (Exception ex)
        {
            // _logger.Log($"ReportSmsAuto error {ex}");
            return new List<ReportSmsDto>();
        }
    }

    public async Task<ReportRegisterInfo> GetRegisterInfo(string code, bool isCache = false)
    {
        try
        {
            if (isCache)
            {
                var checkData = _cacheManager.GetEntity<ReportRegisterInfo>($"ReportRegister:Items:{code}").Result;
                if (checkData != null)
                    return checkData;
            }
            Expression<Func<ReportRegisterInfo, bool>> query = p => p.Code == code;
            var first = await _reportMongoRepository.GetOneAsync(query);
            if (isCache)
            {
                await _cacheManager.AddEntity($"ReportRegister:Items:{code}", first, new TimeSpan(365, 0, 0));
            }
            return first;
        }
        catch (Exception ex)
        {
            _logger.LogError($"{code} GetRegisterInfo Exception: {ex}");
            return null;
        }
    }

    public async Task UpdateRegisterInfo(ReportRegisterInfo info)
    {
        Expression<Func<ReportRegisterInfo, bool>> query = p => p.Code == info.Code;
        var first =await _reportMongoRepository.GetOneAsync(query);
        if (first != null)
        {
            first.AccountList = info.AccountList;
            first.Content = info.Content;
            first.EmailSend = info.EmailSend;
            first.EmailCC = info.EmailCC;
            first.Name = info.Name;
            first.IsAuto = info.IsAuto;
            first.Providers = info.Providers;
            first.Total = info.Total;
            first.Extend = info.Extend;
            await _reportMongoRepository.UpdateOneAsync(first);
            await _cacheManager.DeleteEntity($"ReportRegister:Items:{info.Code}");
        }
        else
        {
            await _reportMongoRepository.AddOneAsync(info);
        }
    }

    public async Task UpdateBalanceSupplierInfo(ReportBalanceSupplierDay info)
    {
        Expression<Func<ReportBalanceSupplierDay, bool>> query = p => p.TextDay == info.TextDay
                                                                      && p.SupplierCode == info.SupplierCode;
        var first = _reportMongoRepository.GetAll(query).FirstOrDefault();
        if (first != null)
        {
            first.Balance = info.Balance;
            await _reportMongoRepository.UpdateOneAsync(first);
        }
        else
        {
            await _reportMongoRepository.AddOneAsync(info);
        }
    }

    private async Task<List<ReportCardStockByDate>> FillterStock(List<ReportCardStockByDate> lst, DateTime fromDate,
        DateTime toDate)
    {
        try
        {
            #region 0.Trong kỳ

            var listKy = lst.Where(c => c.CreatedDate <= toDate.ToUniversalTime()
                                        && c.CreatedDate >= fromDate.ToUniversalTime());

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

            #endregion

            #region 1.Đầu kỳ

            var listBefore = lst.Where(c => c.CreatedDate < fromDate.ToUniversalTime());

            var listGroupBefore = from x in listBefore
                                  group x by new { x.StockCode, x.ProductCode, x.CategoryCode, x.CardValue }
                into g
                                  select new ReportCardStockByDate
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
                                 select new ReportCardStockByDate
                                 {
                                     StockCode = x.StockCode,
                                     ProductCode = x.ProductCode,
                                     CategoryCode = x.CategoryCode,
                                     CardValue = x.CardValue,
                                     InventoryBefore = Convert.ToInt32(yc.InventoryAfter)
                                 };

            #endregion

            #region 2.Cuối kỳ

            var listAfter = lst.Where(c => c.CreatedDate <= toDate.ToUniversalTime());

            var listGroupAfter = from x in listAfter
                                 group x by new { x.StockCode, x.ProductCode, x.CategoryCode, x.CardValue }
                into g
                                 select new ReportCardStockByDate
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
                                select new ReportCardStockByDate
                                {
                                    StockCode = x.StockCode,
                                    ProductCode = x.ProductCode,
                                    CategoryCode = x.CategoryCode,
                                    CardValue = x.CardValue,
                                    InventoryAfter = Convert.ToInt32(yc.InventoryAfter)
                                };

            #endregion


            var listView = from after in listViewAfter
                           join k in listGroupKy on after.ProductCode equals k.ProductCode into gk
                           from ky in gk.DefaultIfEmpty()
                           join d in listViewBefore on after.ProductCode equals d.ProductCode into gd
                           from before in gd.DefaultIfEmpty()
                           select new ReportCardStockByDate
                           {
                               StockCode = after.StockCode,
                               ProductCode = after.ProductCode,
                               CardValue = after.CardValue,
                               InventoryBefore = before?.InventoryBefore ?? 0,
                               InventoryAfter = after?.InventoryAfter ?? 0,
                               IncreaseSupplier = ky != null ? Convert.ToInt32(ky.IncreaseSupplier) : 0,
                               IncreaseOther = ky != null ? Convert.ToInt32(ky.IncreaseOther) : 0,
                               Sale = ky != null ? Convert.ToInt32(ky.Sale) : 0,
                               ExportOther = ky != null ? Convert.ToInt32(ky.ExportOther) : 0
                           };


            return listView.ToList();
        }
        catch (Exception ex)
        {
            // _logger.LogError($"FillterStock error {ex}");
            return new List<ReportCardStockByDate>();
        }
    }

    public async Task<List<ReportSystemDay>> GetAccountSystemDay(string dateTxt)
    {
        try
        {
            Expression<Func<ReportSystemDay, bool>> query = p =>
                p.TextDay == dateTxt;

            var lst = await _reportMongoRepository.GetAllAsync(query);
            return lst.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError($"GetAccountSystemDay error {ex}");
            return null;
        }
    }
    public async Task<ReportSystemDay> GetAccountSystemDayByCode(string accountCode, string dateTxt)
    {
        try
        {
            Expression<Func<ReportSystemDay, bool>> query = p =>
                p.TextDay == dateTxt && p.AccountCode == accountCode;

            var data = await _reportMongoRepository.GetOneAsync(query);
            return data;
        }
        catch (Exception ex)
        {
            _logger.LogError($"GetAccountSystemDayByCode error {ex}");
            return null;
        }
    }
    public async Task<bool> UpdateAccountSystemDay(ReportSystemDay dto)
    {
        try
        {
            Expression<Func<ReportSystemDay, bool>> query = p =>
                p.TextDay == dto.TextDay && p.AccountCode == dto.AccountCode;
            var queryData = await _reportMongoRepository.GetOneAsync(query);
            if (queryData == null)
                await _reportMongoRepository.AddOneAsync(dto);
            else
            {
                queryData.BalanceBefore = dto.BalanceBefore;
                queryData.BalanceAfter = dto.BalanceAfter;
                await _reportMongoRepository.UpdateOneAsync(queryData);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"UpdateAccountSystemDay error {ex}");
            return false;
        }
    }

    public async Task<double> getCheckBalance(string accountCode)
    {
        try
        {
            var client = new JsonServiceClient(_apiUrl)
            {
                Timeout = TimeSpan.FromMinutes(20)
            };
            _logger.LogInformation($"getCheckBalance|AccountCode= {accountCode}");
            var reponse = await client.GetAsync<ResponseObject<decimal>>(new BalanceCheckRequest { AccountCode = accountCode, CurrencyCode = "VND" });
            _logger.LogInformation($"getCheckBalance|AccountCode= {accountCode}|Result= {reponse.Result}");
            return Convert.ToDouble(reponse.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError($"AccountCode= {accountCode}|getCheckBalance_Exception= {ex.Message}|{ex.StackTrace}|{ex.InnerException}");
            return 0;
        }
    }
}