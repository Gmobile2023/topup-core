using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using GMB.Topup.Report.Domain.Entities;
using GMB.Topup.Report.Domain.Exporting;
using GMB.Topup.Report.Model.Dtos;
using GMB.Topup.Report.Model.Dtos.RequestDto;
using GMB.Topup.Report.Model.Dtos.ResponseDto;
using GMB.Topup.Shared;
using GMB.Topup.Shared.CacheManager;
using GMB.Topup.Shared.EsIndexs;
using GMB.Topup.Shared.Helpers;
using Microsoft.Extensions.Logging;
using Nest;
using ServiceStack;

namespace GMB.Topup.Report.Domain.Repositories
{
    public partial class ElasticReportRepository : IElasticReportRepository
    {
        /// <summary>
        /// Báo cáo tổng hợp theo loại tài khoản
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<MessagePagedResponseBase> ReportBalanceGroupTotalGetList(BalanceGroupTotalRequest request)
        {
            try
            {
                var query = new SearchDescriptor<ReportAccountBalanceDay>();
                var fromDate = request.FromDate.Value.ToUniversalTime();
                var toDate = request.ToDate.Value.AddDays(1).ToUniversalTime();

                query.Index(ReportIndex.ReportAccountbalanceDayIndex).Query(q => q.Bool(b =>
                    b.Must(mu => mu.DateRange(r => r.Field(f => f.CreatedDay).GreaterThanOrEquals(fromDate).LessThan(toDate))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.CurrencyCode).Query("VND"))
                    )
                ));

                query.From(0).Size(10000).Scroll("5m");
                var searchData = new List<ReportAccountBalanceDay>();
                var scanResults = await _elasticClient.SearchAsync<ReportAccountBalanceDay>(query);
                ScrollAccountBalanceDay(scanResults, int.MaxValue, ref searchData);

                var mslist = (from x in searchData.ToList()
                              select new BalanceTotalItem
                              {
                                  AccountCode = x.AccountCode,
                                  AccountType = x.AccountType,
                                  CreateDate = x.CreatedDay,
                                  Credited = x.Credite,
                                  Debit = x.Debit,
                                  BalanceBefore = x.BalanceBefore,
                                  BalanceAfter = x.BalanceAfter,
                              }).ToList();


                var revenues = (from x in mslist
                                group x by new { x.AccountCode, x.AccountType }
                    into g
                                select new BalanceTotalItem()
                                {
                                    AccountCode = g.Key.AccountCode,
                                    AccountType = g.Key.AccountType,
                                    Credited = Math.Round(g.Sum(c => c.Credited), 0),
                                    Debit = Math.Round(g.Sum(c => c.Debit), 0),
                                }).ToList();

                var minDate = (from x in mslist
                               group x by new { x.AccountCode, x.AccountType }
                    into g
                               select new BalanceTotalItem()
                               {
                                   AccountCode = g.Key.AccountCode,
                                   AccountType = g.Key.AccountType,
                                   CreateDate = g.Min(c => c.CreateDate),
                               }).ToList();

                var minBalance = (from x in minDate
                                  join y in mslist on x.AccountCode equals y.AccountCode
                                  where x.CreateDate == y.CreateDate && x.AccountType == y.AccountType
                                  select new BalanceTotalItem
                                  {
                                      AccountCode = x.AccountCode,
                                      AccountType = x.AccountType,
                                      BalanceBefore = y.BalanceBefore,
                                  }).ToList();


                var maxDate = (from x in mslist
                               group x by new { x.AccountCode, x.AccountType }
                    into g
                               select new BalanceTotalItem()
                               {
                                   AccountCode = g.Key.AccountCode,
                                   AccountType = g.Key.AccountType,
                                   CreateDate = g.Max(c => c.CreateDate),
                               }).ToList();

                var maxBalance = (from x in maxDate
                                  join y in mslist on x.AccountCode equals y.AccountCode
                                  where x.CreateDate == y.CreateDate && x.AccountType == y.AccountType
                                  select new BalanceTotalItem
                                  {
                                      AccountCode = x.AccountCode,
                                      AccountType = x.AccountType,
                                      BalanceAfter = y.BalanceAfter,
                                  }).ToList();

                var list = (from r in revenues
                            join mx in maxBalance on r.AccountCode equals mx.AccountCode
                            join mi in minBalance on r.AccountCode equals mi.AccountCode
                            select new ReportBalanceTotalDto()
                            {
                                AccountCode = r.AccountCode,
                                AccountType = r.AccountType,
                                BalanceBefore = Math.Round(mi.BalanceBefore, 0),
                                BalanceAfter = Math.Round(mx.BalanceAfter, 0),
                                Credited = Math.Round(r.Credited, 0),
                                Debit = Math.Round(r.Debit, 0),
                            }).ToList();


                var groupList = (from x in list
                                 where x.AccountType == "SYSTEM"
                                 select x).ToList();

                var groupCustomer = new ReportBalanceTotalDto()
                {
                    AccountCode = "CUSTOMER",
                    AccountType = "CUSTOMER",
                    BalanceAfter = Math.Round(list.Where(c => c.AccountType == "CUSTOMER").Sum(c => c.BalanceAfter), 0),
                    BalanceBefore = Math.Round(list.Where(c => c.AccountType == "CUSTOMER").Sum(c => c.BalanceBefore),
                        0),
                    Credited = Math.Round(list.Where(c => c.AccountType == "CUSTOMER").Sum(c => c.Credited), 0),
                    Debit = Math.Round(list.Where(c => c.AccountType == "CUSTOMER").Sum(c => c.Debit), 0),
                };
                groupList.Add(groupCustomer);

                var total = groupList.Count();
                var sumtotal = new ReportBalanceTotalDto()
                {
                    BalanceBefore = Math.Round(groupList.Sum(c => c.BalanceBefore), 0),
                    BalanceAfter = Math.Round(groupList.Sum(c => c.BalanceAfter), 0),
                    Credited = Math.Round(groupList.Sum(c => c.Credite), 0),
                    Debit = Math.Round(groupList.Sum(c => c.Debit), 0),
                };
                var vList = groupList.Skip(request.Offset).Take(request.Limit).ToList();
                return new MessagePagedResponseBase
                {
                    ResponseCode = ResponseCodeConst.Success,
                    ResponseMessage = "Thành công",
                    Total = total,
                    SumData = sumtotal,
                    Payload = vList
                };

            }
            catch (Exception e)
            {
                _logger.LogError($"ReportBalanceGroupTotalGetList error {e}");
                return new MessagePagedResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error
                };
            }
        }

        /// <summary>
        /// Báo cáo tổng hợp theo tài khoản
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<MessagePagedResponseBase> ReportBalanceTotalGetList(BalanceTotalRequest request)
        {
            try
            {
                var query = new SearchDescriptor<ReportAccountBalanceDay>();
                var fromDate = request.FromDate.Value.ToUniversalTime();
                var toDate = request.ToDate.Value.AddDays(1).ToUniversalTime();
                string agentType = string.Empty;
                string saleCode = string.Empty;
                string saleLeaderCode = string.Empty;
                string accountCode = request.AccountCode;
                if (request.AgentType <= 0)
                    agentType = "";
                else agentType = request.AgentType.ToString();

                if (request.AccountType > 0)
                {
                    if (request.AccountType == 1 || request.AccountType == 2 || request.AccountType == 3 ||
                        request.AccountType == 4)
                        accountCode = request.LoginCode;
                    else if (request.AccountType == 5)
                        saleLeaderCode = request.LoginCode;
                    else if (request.AccountType == 6)
                        saleCode = request.LoginCode;
                }

                query.Index(ReportIndex.ReportAccountbalanceDayIndex).Query(q => q.Bool(b =>
                    b.Must(mu => mu.DateRange(r => r.Field(f => f.CreatedDay).GreaterThanOrEquals(fromDate).LessThan(toDate))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.CurrencyCode).Query("VND"))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.AccountType).Query("CUSTOMER"))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.AgentType).Query(agentType))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.AccountCode).Query(accountCode))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.SaleCode).Query(saleCode))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.SaleLeaderCode).Query(saleLeaderCode))
                    )
                ));

                query.From(0).Size(10000).Scroll("3m");
                var searchData = new List<ReportAccountBalanceDay>();
                var scanResults = await _elasticClient.SearchAsync<ReportAccountBalanceDay>(query);
                ScrollAccountBalanceDay(scanResults, int.MaxValue, ref searchData);

                var listSelect = from x in searchData
                                 select new ReportAccountBalanceDayTemp()
                                 {
                                     AccountCode = x.AccountCode,
                                     AgentType = (x.AgentType == 0 ? 1 : x.AgentType ?? 0),
                                     AccountInfo = x.AccountCode + (!string.IsNullOrEmpty(x.AccountInfo) ? ("-" + x.AccountInfo) : ""),
                                     BalanceBefore = x.BalanceBefore,
                                     BalanceAfter = x.BalanceAfter,
                                     Credited = x.Credite,
                                     Debit = x.Debit,
                                     CreatedDay = x.CreatedDay,
                                 };

                var listGroup = from x in listSelect
                                group x by new { x.AccountCode, x.AccountInfo, x.AgentType }
                    into g
                                select new ReportAccountBalanceDayTemp()
                                {
                                    AccountCode = g.Key.AccountCode,
                                    AgentType = g.Key.AgentType,
                                    AccountInfo = g.Key.AccountInfo,
                                    Credited = g.Sum(c => c.Credited),
                                    Debit = g.Sum(c => c.Debit),
                                    MaxDate = g.Max(c => c.CreatedDay),
                                    MinDate = g.Min(c => c.CreatedDay),
                                };


                var listView = from g in listGroup
                               join minG in listSelect on g.AccountCode equals minG.AccountCode
                               join maxG in listSelect on g.AccountCode equals maxG.AccountCode
                               where g.MinDate == minG.CreatedDay && g.MaxDate == maxG.CreatedDay
                               select new ReportAccountBalanceDayInfo()
                               {
                                   AccountCode = g.AccountCode,
                                   AccountInfo = g.AccountInfo,
                                   AgentType = g.AgentType,
                                   Credited = Math.Round(g.Credited, 0),
                                   Debit = Math.Round(g.Debit, 0),
                                   BalanceBefore = Math.Round(minG.BalanceBefore, 0),
                                   BalanceAfter = Math.Round(maxG.BalanceAfter, 0),
                               };

                var total = listView.Count();
                var sumtotal = new ReportAccountBalanceDayInfo()
                {
                    BalanceBefore = Math.Round(listView.Sum(c => c.BalanceBefore), 0),
                    BalanceAfter = Math.Round(listView.Sum(c => c.BalanceAfter), 0),
                    Credited = Math.Round(listView.Sum(c => c.Credited), 0),
                    Debit = Math.Round(listView.Sum(c => c.Debit), 0),
                };
                listView = listView.OrderBy(x => x.AccountCode).Skip(request.Offset).Take(request.Limit);

                return new MessagePagedResponseBase
                {
                    ResponseCode = ResponseCodeConst.Success,
                    ResponseMessage = "Thành công",
                    Total = (int)total,
                    SumData = sumtotal,
                    Payload = listView
                };

            }
            catch (Exception ex)
            {
                _logger.LogError($"ReportBalanceTotalGetList error {ex}");
                return new MessagePagedResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error
                };
            }
        }

        /// <summary>
        /// Báo cáo NXT mã thẻ
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<MessagePagedResponseBase> CardStockImExPort(CardStockImExPortRequest request)
        {
            try
            {
                var query = new SearchDescriptor<ReportCardStockByDate>();
                var fromDate = request.FromDate.Value.ToUniversalTime();
                var toDate = request.ToDate.Value.AddDays(1).ToUniversalTime();
                var dateNow = DateTime.Now.ToUniversalTime();
                string storeCode = request.StoreCode ?? "";
                string productCode = request.ProductCode ?? "";
                string categoryCode = request.CategoryCode ?? "";
                string serviceCode = request.ServiceCode ?? "";


                query.Index(ReportIndex.ReportCardstockbydatesIndex).Query(q => q.Bool(b =>
                    b.Must(mu => mu.DateRange(r => r.Field(f => f.CreatedDate).LessThanOrEquals(dateNow))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.StockCode).Query(storeCode))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.ProductCode).Query(productCode))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.CategoryCode).Query(categoryCode))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.ServiceCode).Query(serviceCode))
                    )
                ));

                query.From(0).Size(10000).Scroll("5m");
                var searchData = new List<ReportCardStockByDate>();
                var scanResults = await _elasticClient.SearchAsync<ReportCardStockByDate>(query);
                ScrollCardStockByDate(scanResults, int.MaxValue, ref searchData);
                var listKy = searchData.Where(c => c.CreatedDate >= fromDate && c.CreatedDate <= toDate);

                var listGroupKy = from x in listKy
                                  group x by new { x.StockCode, x.ProductCode, x.CategoryCode, x.CardValue } into g
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

                var listBefore = searchData.Where(c => c.CreatedDate < fromDate);

                var listGroupBefore = from x in listBefore
                                      group x by new { x.StockCode, x.ProductCode, x.CategoryCode, x.CardValue } into g
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

                var listAfter = searchData.Where(c => c.CreatedDate <= toDate);

                var listGroupAfter = from x in listAfter
                                     group x by new { x.StockCode, x.ProductCode, x.CategoryCode, x.CardValue } into g
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

                var listGroupCurrent = from x in searchData
                                       group x by new { x.StockCode, x.ProductCode, x.CategoryCode, x.CardValue } into g
                                       select new ReportCardStockHistories
                                       {
                                           StockCode = g.Key.StockCode,
                                           ProductCode = g.Key.ProductCode,
                                           CategoryCode = g.Key.CategoryCode,
                                           CardValue = Convert.ToInt32(g.Key.CardValue),
                                           CreatedDate = g.Max(c => c.CreatedDate)
                                       };

                var listViewCurrent = from x in listGroupCurrent
                                      join yc in searchData on x.ProductCode equals yc.ProductCode
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
                _logger.LogError($"CardStockImExPort error {ex}");
                return new MessagePagedResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error
                };

            }
        }

        /// <summary>
        /// Báo cáo NXT theo nhà cung cấp mã thẻ
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<MessagePagedResponseBase> CardStockImExPortProvider(CardStockImExPortProviderRequest request)
        {
            try
            {
                var query = new SearchDescriptor<ReportCardStockProviderByDate>();
                var fromDate = request.FromDate.Value.ToUniversalTime();
                var toDate = request.ToDate.Value.AddDays(1).ToUniversalTime();
                var dateNow = DateTime.Now.ToUniversalTime();
                string storeCode = request.StoreCode ?? "";
                string productCode = request.ProductCode ?? "";
                string categoryCode = request.CategoryCode ?? "";
                string serviceCode = request.ServiceCode ?? "";
                string providerCode = request.ProviderCode ?? string.Empty;


                query.Index(ReportIndex.ReportCardstockproviderbydates).Query(q => q.Bool(b =>
                    b.Must(mu => mu.DateRange(r => r.Field(f => f.CreatedDate).LessThanOrEquals(dateNow))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.StockCode).Query(storeCode))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.ProductCode).Query(productCode))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.CategoryCode).Query(categoryCode))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.ServiceCode).Query(serviceCode))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.ProviderCode).Query(providerCode))
                    )
                ));

                query.From(0).Size(10000).Scroll("3m");
                var searchData = new List<ReportCardStockProviderByDate>();
                var scanResults = await _elasticClient.SearchAsync<ReportCardStockProviderByDate>(query);
                ScrollCardStockProviderByDate(scanResults, int.MaxValue, ref searchData);

                var listKy = searchData.Where(c => c.CreatedDate >= fromDate && c.CreatedDate < toDate);

                var listGroupKy = from x in listKy
                                  group x by new { x.StockCode, x.ProviderCode, x.ProductCode, x.CategoryCode, x.CardValue } into g
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

                var listBefore = searchData.Where(c => c.CreatedDate < fromDate);

                var listGroupBefore = from x in listBefore
                                      group x by new { x.StockCode, x.ProviderCode, x.ProductCode, x.CategoryCode, x.CardValue } into g
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

                var listAfter = searchData.Where(c => c.CreatedDate <= toDate);

                var listGroupAfter = from x in listAfter
                                     group x by new { x.ProviderCode, x.StockCode, x.ProductCode, x.CategoryCode, x.CardValue } into g
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

                var listGroupCurrent = from x in searchData
                                       group x by new { x.ProviderCode, x.StockCode, x.ProductCode, x.CategoryCode, x.CardValue } into g
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
                                      join yc in searchData on x.ProductCode equals yc.ProductCode
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
                                      group x by new { x.CategoryCode, x.ProductCode, x.StoreCode, x.CardValue } into g
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

                _logger.LogError($"CardStockImExPortProvider error {ex}");
                return new MessagePagedResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error
                };
            }
        }

        /// <summary>
        /// Báo cáo tổng hợp lúc 0h
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<List<ReportRevenueTotalAutoDto>> ReportTotal0hDateAuto(ReportTotalAuto0hRequest request)
        {
            try
            {
                var query = new SearchDescriptor<ReportAccountBalanceDay>();
                var fromDate = request.FromDate.ToUniversalTime();
                var toDate = request.ToDate.ToUniversalTime();
                query.Index(ReportIndex.ReportAccountbalanceDayIndex).Query(q => q.Bool(b =>
                    b.Must(mu => mu.DateRange(r => r.Field(f => f.CreatedDay).GreaterThanOrEquals(fromDate).LessThan(toDate))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.CurrencyCode).Query("VND"))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.AccountType).Query("CUSTOMER"))
                    )
                ));

                query.From(0).Size(10000).Scroll("5m");
                var searchData = new List<ReportAccountBalanceDay>();
                var scanResults = await _elasticClient.SearchAsync<ReportAccountBalanceDay>(query);
                ScrollAccountBalanceDay(scanResults, int.MaxValue, ref searchData);
                var agentTypes = new[] { "1", "2", "3" };
                var queryAccount = new SearchDescriptor<ReportAccountDto>();
                queryAccount.Index(ReportIndex.ReportaccountdtosIndex).Query(q => q.Bool(b =>
                    b.Must(mu => mu.Terms(m => m.Field(f => f.AgentType).Terms(agentTypes.ToArray())))
                ));

                queryAccount.From(0).Size(10000).Scroll("5m");
                var searchDataAccount = new List<ReportAccountDto>();
                var scanResultsAccount = await _elasticClient.SearchAsync<ReportAccountDto>(queryAccount);
                ScrollAccountInfo(scanResultsAccount, int.MaxValue, ref searchDataAccount);
                searchDataAccount.ForEach(c =>
                {
                    if (c.CreationTime != null)
                        c.CreationTime = _dateHepper.ConvertToUserTime(c.CreationTime.Value, DateTimeKind.Utc).Date;
                });

                var dayActives = (from x in searchDataAccount
                                  where x.CreationTime != null
                                  group x by x.CreationTime.Value.Date into g
                                  select new ReportRevenueTotalAutoDto
                                  {
                                      CreatedDay = g.Key,
                                      AccountActive = g.Count()
                                  }).ToList();


                var list = (from x in searchData
                            group x by new { x.AccountCode, x.CreatedDay } into g
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
                               group x by x.CreatedDay into g
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
                _logger.LogError($"ReportTotal0hDateAuto error {ex}");
                return new List<ReportRevenueTotalAutoDto>();
            }
        }

        public async Task<MessagePagedResponseBase> ReportTotalDayGetList(ReportTotalDayRequest request)
        {
            try
            {
                var query = new SearchDescriptor<ReportAccountBalanceDay>();
                var fromDate = request.FromDate.Value.ToUniversalTime();
                var toDate = request.ToDate.Value.AddDays(1).ToUniversalTime();
                string accountCode = string.Empty;
                string accountType = string.Empty;
                if (!string.IsNullOrEmpty(request.AccountCode))
                {
                    accountCode = request.AccountCode;
                    accountType = "CUSTOMER";
                }

                query.Index(ReportIndex.ReportAccountbalanceDayIndex).Query(q => q.Bool(b =>
                    b.Must(mu => mu.DateRange(r => r.Field(f => f.CreatedDay).GreaterThanOrEquals(fromDate).LessThan(toDate))
                          , mu => mu.MatchPhrase(m => m.Field(f => f.CurrencyCode).Query("VND"))
                          , mu => mu.MatchPhrase(m => m.Field(f => f.AccountCode).Query(request.AccountCode))
                          , mu => mu.MatchPhrase(m => m.Field(f => f.AccountCode).Query(accountCode))
                          , mu => mu.MatchPhrase(m => m.Field(f => f.AccountType).Query(accountType))
                    )
                ));

                query.From(0).Size(10000).Scroll("3m");
                var searchData = new List<ReportAccountBalanceDay>();
                var scanResults = await _elasticClient.SearchAsync<ReportAccountBalanceDay>(query);
                ScrollAccountBalanceDay(scanResults, int.MaxValue, ref searchData);

                var total = searchData.Count();
                var maxDate = searchData.Max(c => c.CreatedDay);
                var minDate = searchData.Min(c => c.CreatedDay);

                var lst = searchData.Skip(request.Offset).Take(request.Limit);

                var sumData = new ReportItemTotalDay()
                {
                    BalanceBefore = searchData.FirstOrDefault(c => c.CreatedDay == minDate).BalanceBefore,
                    BalanceAfter = searchData.Where(c => c.CreatedDay == maxDate).FirstOrDefault()!.BalanceAfter,
                    IncDeposit = searchData.Sum(c => c.IncDeposit ?? 0),
                    IncOther = searchData.Sum(x => x.IncOther ?? 0),
                    DecPayment = searchData.Sum(x => x.DecPayment ?? 0),
                    DecOther = searchData.Sum(x => x.DecOther ?? 0)
                };

                //chỗ này sửa lại map đúng tên trường của a Tiến
                var list = lst.OrderBy(x => x.AccountCode).ThenBy(x => x.CreatedDay);
                var mappingList = (from x in lst
                                   select new ReportItemTotalDay()
                                   {
                                       CreatedDay = _dateHepper.ConvertToUserTime(x.CreatedDay, DateTimeKind.Utc),
                                       BalanceBefore = x.BalanceBefore,
                                       BalanceAfter = x.BalanceAfter,
                                       IncDeposit = x.IncDeposit ?? 0,
                                       IncOther = x.IncOther ?? 0,
                                       DecPayment = x.DecPayment ?? 0,
                                       DecOther = x.DecOther ?? 0,
                                   }).ToList();

                return new MessagePagedResponseBase
                {
                    ResponseCode = ResponseCodeConst.Success,
                    ResponseMessage = "Thành công",
                    Total = (int)total,
                    SumData = sumData,
                    Payload = mappingList,
                };
            }
            catch (Exception e)
            {
                _logger.LogError($"ReportDetailGetList error: {e}");
                return new MessagePagedResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error
                };
            }
        }


        public async Task<MessagePagedResponseBase> ReportTotalDebtGetList(ReportTotalDebtRequest request)
        {
            try
            {
                var query = new SearchDescriptor<ReportAccountBalanceDay>();
                var fromDate = request.FromDate.Value.ToUniversalTime();
                var toDate = request.ToDate.Value.Date.AddDays(1).ToUniversalTime();
                string saleCode = string.Empty;
                string accountCode = string.Empty;
                if (request.AccountType > 0)
                {
                    if (request.AccountType == 5)
                        saleCode = request.LoginCode;
                    else if (request.AccountType == 6)
                        accountCode = request.LoginCode;
                }


                query.Index(ReportIndex.ReportAccountbalanceDayIndex).Query(q => q.Bool(b =>
                    b.Must(mu => mu.DateRange(r => r.Field(f => f.CreatedDay).GreaterThanOrEquals(fromDate).LessThan(toDate))
                         , mu => mu.MatchPhrase(m => m.Field(f => f.CurrencyCode).Query("DEBT"))
                         , mu => mu.MatchPhrase(m => m.Field(f => f.AccountCode).Query(accountCode))
                         , mu => mu.MatchPhrase(m => m.Field(f => f.SaleCode).Query(saleCode))
                    )
                ));

                query.From(0).Size(10000).Scroll("5m");
                var searchData = new List<ReportAccountBalanceDay>();
                var scanResults = await _elasticClient.SearchAsync<ReportAccountBalanceDay>(query);
                ScrollAccountBalanceDay(scanResults, int.MaxValue, ref searchData);
                var listSearch = (from g in searchData
                                  select new ReportTotalTempDebt
                                  {
                                      SaleCode = g.AccountCode,
                                      SaleInfo = string.Empty,
                                      DecPayment = g.Debit,
                                      IncDeposit = g.Credite,
                                      CreatedDay = g.CreatedDay,
                                      BalanceAfter = g.BalanceAfter,
                                      BalanceBefore = g.BalanceBefore,
                                      LimitAfter = g.LimitAfter ?? 0,
                                      LimitBefore = g.LimitBefore ?? 0,
                                  }).ToList();


                var listGroup = (from x in listSearch
                                 group x by new { x.SaleCode, x.SaleInfo }
                    into g
                                 select new ReportTotalTempDebt
                                 {
                                     SaleCode = g.Key.SaleCode,
                                     SaleInfo = g.Key.SaleInfo,
                                     DecPayment = g.Sum(c => c.DecPayment),
                                     IncDeposit = g.Sum(c => c.IncDeposit),
                                     MaxDate = g.Max(c => c.CreatedDay),
                                     MinDate = g.Min(c => c.CreatedDay),
                                 }).ToList();

                var listView = (from x in listGroup
                                join mi in listSearch on x.SaleCode equals mi.SaleCode
                                join ma in listSearch on x.SaleCode equals ma.SaleCode
                                where x.MinDate == mi.CreatedDay && x.MaxDate == ma.CreatedDay
                                select new ReportItemTotalDebt()
                                {
                                    SaleCode = x.SaleCode,
                                    SaleInfo = x.SaleInfo,
                                    BalanceBefore = mi.LimitBefore - mi.BalanceBefore,
                                    BalanceAfter = ma.LimitAfter - ma.BalanceAfter,
                                    IncDeposit = x.IncDeposit,
                                    DecPayment = x.DecPayment,
                                }).OrderBy(c => c.SaleInfo).ToList();

                var total = listView.Count;
                var sumtotal = new ReportItemTotalDebt()
                {
                    BalanceBefore = listView.Sum(c => c.BalanceBefore),
                    BalanceAfter = listView.Sum(c => c.BalanceAfter),
                    IncDeposit = listView.Sum(c => c.IncDeposit),
                    DecPayment = listView.Sum(c => c.DecPayment),
                };
                var lst = listView.OrderBy(c => c.SaleInfo).Skip(request.Offset).Take(request.Limit).ToList();
                var saleCodes = lst.Where(c => !string.IsNullOrEmpty(c.SaleCode)).Select(c => c.SaleCode).Distinct()
                    .ToList();

                var lstSysAccounts = await GetAccountByArrays(saleCodes);
                var msglst = (from x in lst
                              join y in lstSysAccounts on x.SaleCode equals y.AccountCode into yg
                              from sale in yg.DefaultIfEmpty()
                              select new ReportItemTotalDebt()
                              {
                                  SaleCode = x.SaleCode,
                                  SaleInfo = sale != null ? sale.UserName + " - " + sale.Mobile + " - " + sale.FullName : "",
                                  BalanceBefore = x.BalanceBefore,
                                  BalanceAfter = x.BalanceAfter,
                                  IncDeposit = x.IncDeposit,
                                  DecPayment = x.DecPayment,
                              }).ToList();

                return new MessagePagedResponseBase
                {
                    ResponseCode = ResponseCodeConst.Success,
                    ResponseMessage = "Thành công",
                    Total = total,
                    SumData = sumtotal,
                    Payload = msglst,
                };
            }
            catch (Exception e)
            {
                _logger.LogError($"ReportTotalDebtGetList error: {e}");
                return new MessagePagedResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error
                };
            }
        }

        public async Task<RevenueInDayDto> ReportRevenueInDayQuery(RevenueInDayRequest request)
        {
            try
            {

                var services = new List<string>
            {
                ReportServiceCode.TOPUP.ToLower(),
                ReportServiceCode.TOPUP_DATA.ToLower(),
                ReportServiceCode.PAY_BILL.ToLower(),
                ReportServiceCode.PIN_CODE.ToLower(),
                ReportServiceCode.PIN_DATA.ToLower(),
                ReportServiceCode.PIN_GAME.ToLower()
            };
                var status = new[] { "1", "2" };
                var query = new SearchDescriptor<ReportItemDetail>();
                var fromDate = DateTime.Now.Date.ToUniversalTime();
                var toDate = DateTime.Now.Date.AddDays(1).ToUniversalTime();
                query.Index(ReportIndex.ReportItemDetailIndex).Query(q => q.Bool(b =>
                    b.Must(mu => mu.DateRange(r => r.Field(f => f.CreatedTime).GreaterThanOrEquals(fromDate).LessThan(toDate))
                        , mu => mu.MultiMatch(m => m.Fields(f => f.Field(c => c.AccountCode).Field(c => c.PerformAccount)).Query(request.AccountCode))
                        , mu => mu.Terms(m => m.Field(f => f.ServiceCode).Terms(services))
                        , mu => mu.Terms(m => m.Field(f => f.TransType).Terms(services))
                        , mu => mu.Terms(m => m.Field(f => f.Status).Terms(status))
                    )
                ));

                var totalQuery = query;
                query.Aggregations(agg => agg
                    .Sum("Value", s => s.Field(p => p.Amount))
                    .Sum("Price", s => s.Field(p => p.TotalPrice))
                );

                query.From(0).Size(1).Scroll("5m");
                var searchData = new List<ReportItemDetail>();
                var scanResults = await _elasticClient.SearchAsync<ReportItemDetail>(query);
                var fValue = scanResults.Aggregations.GetValueOrDefault("Value");
                var fPrice = scanResults.Aggregations.GetValueOrDefault("Price");
                var value = fValue.ConvertTo<ValueTeam>();
                var price = fPrice.ConvertTo<ValueTeam>();
                var quantity = int.Parse((await _elasticClient.SearchAsync<ReportItemDetail>(totalQuery)).Total.ToString());
                var data = new RevenueInDayDto()
                {
                    Quantity = quantity,
                    Revenue = Convert.ToDouble(value.Value),
                    SalePrice = Convert.ToDouble(price.Value),
                };

                return data;
            }
            catch (Exception e)
            {
                _logger.LogError($"ReportRevenueInDayQuery error: {e}");
                return null;
            }
        }

        public async Task<MessagePagedResponseBase> CardStockInventory(CardStockInventoryRequest request)
        {
            try
            {
                var query = new SearchDescriptor<ReportCardStockByDate>();
                var fromDate = request.FromDate.Value.ToUniversalTime();
                var toDate = request.ToDate.Value.AddDays(1).ToUniversalTime();
                string storeCode = request.StockCode ?? string.Empty;
                string productCode = request.ProductCode ?? string.Empty;
                string categoryCode = request.CategoryCode ?? string.Empty;
                string cardValue = string.Empty;
                string stockType = request.StockType ?? string.Empty;

                string vendor = request.Vendor ?? string.Empty;
                if (request.CardValue > 0)
                    cardValue = request.CardValue.ToString();


                query.Index(ReportIndex.ReportCardstockbydatesIndex).Query(q => q.Bool(b =>
                    b.Must(mu => mu.DateRange(r => r.Field(f => f.CreatedDate).GreaterThanOrEquals(fromDate).LessThan(toDate))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.CardValue).Query(cardValue))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.Vendor).Query(vendor))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.StockCode).Query(storeCode))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.CategoryCode).Query(categoryCode))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.ProductCode).Query(productCode))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.StockType).Query(stockType))
                    )
                ));

                query.From(0).Size(10000).Scroll("3m");
                var searchData = new List<ReportCardStockByDate>();
                var scanResults = await _elasticClient.SearchAsync<ReportCardStockByDate>(query);
                ScrollCardStockByDate(scanResults, request.Offset + request.Limit, ref searchData);
                var total = int.Parse((await _elasticClient.SearchAsync<ReportCardStockByDate>(query)).Total.ToString());

                foreach (var item in searchData)
                    item.CreatedDate = _dateHepper.ConvertToUserTime(item.CreatedDate, DateTimeKind.Utc);

                return new MessagePagedResponseBase
                {
                    ResponseCode = ResponseCodeConst.Success,
                    ResponseMessage = "Thành công",
                    Total = (int)total,
                    Payload = searchData.OrderBy(x => x.CreatedDate).ThenBy(x => x.StockCode).ThenBy(x => x.Vendor)
                        .ThenBy(x => x.CardValue).ConvertTo<List<ReportCardStockByDateDto>>()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"CardStockInventory error {ex}");
                return new MessagePagedResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error
                };
            }
        }

        public Task<ReportAccountBalanceDay> GetBalanceAgent(string agentCode, DateTime fromDate, DateTime toDate)
        {
            var toTxt = agentCode + "_" + toDate.ToString("yyyyMMdd");
            var fTxt = agentCode + "_" + fromDate.ToString("yyyyMMdd");

            var balance = new ReportAccountBalanceDay()
            {
                AccountCode = agentCode,
            };

            var query = new SearchDescriptor<ReportAccountBalanceDay>();
            query.Index(ReportIndex.ReportAccountbalanceDayIndex).Query(q => q.Bool(b =>
                b.Must(mu => mu.MatchPhrase(m => m.Field(f => f.CurrencyCode).Query("VND"))
                     , mu => mu.MatchPhrase(m => m.Field(f => f.AccountType).Query("CUSTOMER"))
                     , mu => mu.MatchPhrase(m => m.Field(f => f.TextDay).Query(toTxt))
                     , mu => mu.MatchPhrase(m => m.Field(f => f.AccountCode).Query(agentCode))
                )
            ));

            query.From(0).Size(1).Scroll("5m");
            var scanNow = _elasticClient.SearchAsync<ReportAccountBalanceDay>(query).Result;

            if (scanNow != null && scanNow.Documents.Count > 0)
            {
                balance.BalanceAfter = scanNow.Documents.First().BalanceAfter;
                if (toTxt == fTxt)
                {
                    balance.BalanceBefore = scanNow.Documents.First().BalanceBefore;
                    return Task.FromResult(balance);
                }
            }
            else
            {
                var tempFromDate = toDate.AddDays(-35);
                var queryAfter = new SearchDescriptor<ReportAccountBalanceDay>();
                queryAfter.Index(ReportIndex.ReportAccountbalanceDayIndex).Query(q => q.Bool(b =>
                    b.Must(mu => mu.DateRange(r => r.Field(f => f.CreatedDay).GreaterThanOrEquals(tempFromDate.ToUniversalTime()).LessThanOrEquals(toDate.ToUniversalTime()))
                         , mu => mu.MatchPhrase(m => m.Field(f => f.CurrencyCode).Query("VND"))
                         , mu => mu.MatchPhrase(m => m.Field(f => f.AccountType).Query("CUSTOMER"))
                         , mu => mu.MatchPhrase(m => m.Field(f => f.AccountCode).Query(agentCode))
                    )
                ));
                queryAfter.From(0).Size(45).Scroll("3m");
                var scanAfter = _elasticClient.SearchAsync<ReportAccountBalanceDay>(queryAfter).Result;
                if (scanAfter != null && scanAfter.Documents.Count > 0)
                {
                    var f = scanAfter.Documents.OrderByDescending(c => c.CreatedDay).First();
                    balance.BalanceAfter = f.BalanceAfter;
                    if (f.TextDay == fTxt)
                    {
                        balance.BalanceBefore = f.BalanceBefore;
                        return Task.FromResult(balance);
                    }
                }
            }

            var queryBeforeNow = new SearchDescriptor<ReportAccountBalanceDay>();
            queryBeforeNow.Index(ReportIndex.ReportAccountbalanceDayIndex).Query(q => q.Bool(b =>
                b.Must(mu => mu.MatchPhrase(m => m.Field(f => f.CurrencyCode).Query("VND"))
                     , mu => mu.MatchPhrase(m => m.Field(f => f.AccountType).Query("CUSTOMER"))
                     , mu => mu.MatchPhrase(m => m.Field(f => f.TextDay).Query(fTxt))
                     , mu => mu.MatchPhrase(m => m.Field(f => f.AccountCode).Query(agentCode))
                )
            ));

            queryBeforeNow.From(0).Size(1).Scroll("3m");
            var scanBeforeNow = _elasticClient.SearchAsync<ReportAccountBalanceDay>(queryBeforeNow).Result;
            if (scanBeforeNow != null && scanBeforeNow.Documents.Count() > 0)
            {
                balance.BalanceBefore = scanBeforeNow.Documents.First().BalanceBefore;
                return Task.FromResult(balance);
            }
            else
            {
                var queryBeforeAfter = new SearchDescriptor<ReportAccountBalanceDay>();
                var tempFromDate = fromDate.AddDays(-35);
                queryBeforeAfter.Index(ReportIndex.ReportAccountbalanceDayIndex).Query(q => q.Bool(b =>
                    b.Must(mu => mu.DateRange(r => r.Field(f => f.CreatedDay).GreaterThanOrEquals(tempFromDate.ToUniversalTime()).LessThanOrEquals(fromDate.ToUniversalTime()))
                         , mu => mu.MatchPhrase(m => m.Field(f => f.CurrencyCode).Query("VND"))
                         , mu => mu.MatchPhrase(m => m.Field(f => f.AccountType).Query("CUSTOMER"))
                         , mu => mu.MatchPhrase(m => m.Field(f => f.AccountCode).Query(agentCode))
                    )
                ));
                queryBeforeAfter.From(0).Size(45).Scroll("5m");
                var scanBefore = _elasticClient.SearchAsync<ReportAccountBalanceDay>(queryBeforeAfter).Result;
                if (scanBefore != null && scanBefore.Documents.Count > 0)
                {
                    var f = scanBefore.Documents.OrderByDescending(c => c.CreatedDay).First();
                    balance.BalanceBefore = f.TextDay == fTxt ? f.BalanceBefore : f.BalanceAfter;
                    return Task.FromResult(balance);
                }
            }

            return Task.FromResult(balance);
        }

        public async Task<ReportCheckBalance> CheckReportBalanceAndHistory(DateTime date)
        {
            var keyCode = "CheckHistoryDetail_" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
            var keyCodeBalance = "CheckBalanceReport_" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
            var dateStart = DateTime.Now;

            try
            {
                var fromDate = date.Date.ToUniversalTime();
                var toDate = date.Date.AddDays(1).ToUniversalTime();
                var dateTemp = DateTime.Now;
                var searchData = new List<ReportBalanceHistories>();
                var searchDataBalance = new List<ReportAccountBalanceDay>();
                _logger.LogInformation($"KeyCode= {keyCode} StartUp SearchData ");
                var query = new SearchDescriptor<ReportBalanceHistories>();
                query.Index(ReportIndex.ReportBalanceHistoriesIndex).Query(q => q.Bool(b =>
                    b.Must(mu => mu.DateRange(r => r.Field(f => f.CreatedDate).GreaterThanOrEquals(fromDate).LessThan(toDate)))
                )).From(0).Size(10000).Scroll("5m");
                var scanResults = await _elasticClient.SearchAsync<ReportBalanceHistories>(query);
                ScrollBalanceHistories(scanResults, int.MaxValue, ref searchData);
                _logger.LogInformation($"KeyCode= {keyCode} .Lay xong du lieu Seconds: {DateTime.Now.Subtract(dateTemp).TotalSeconds}");

                _logger.LogInformation($"KeyCode= {keyCodeBalance} StartUp SearchData ");
                var queryBalance = new SearchDescriptor<ReportAccountBalanceDay>();
                queryBalance.Index(ReportIndex.ReportAccountbalanceDayIndex).Query(q => q.Bool(b =>
                   b.Must(mu => mu.DateRange(r => r.Field(f => f.CreatedDay).GreaterThanOrEquals(fromDate).LessThan(toDate))
                        , mu => mu.Match(m => m.Field(f => f.AccountType).Query("CUSTOMER"))
                   ))).From(0).Size(10000).Scroll("5m"); ;
                var scanResultBalances = await _elasticClient.SearchAsync<ReportAccountBalanceDay>(queryBalance);
                ScrollAccountBalanceDay(scanResultBalances, int.MaxValue, ref searchDataBalance);
                _logger.LogInformation($"KeyCode= {keyCodeBalance} .Lay xong du lieu Seconds: {DateTime.Now.Subtract(dateTemp).TotalSeconds}");

                #region 1.Phần xử lý dữ liệu lấy ra của lịch xử giao dịch

                var tempHistory = (from x in searchData
                                   where x.SrcAccountType == "CUSTOMER"
                                   select new ReportAccountBalanceDay
                                   {
                                       AccountCode = x.SrcAccountCode,
                                       CreatedDay = x.CreatedDate,
                                       BalanceBefore =x.SrcAccountBalanceBeforeTrans,
                                       BalanceAfter =x.SrcAccountBalanceAfterTrans,
                                       CurrencyCode = x.CurrencyCode,
                                       Debit =x.Amount,
                                       Credite = 0
                                   }).ToList();

                var tempL2 = (from x in searchData
                              where x.DesAccountType == "CUSTOMER"
                              select new ReportAccountBalanceDay
                              {
                                  AccountCode = x.DesAccountCode,
                                  CreatedDay = x.CreatedDate,
                                  BalanceBefore = x.DesAccountBalanceBeforeTrans,
                                  BalanceAfter = x.DesAccountBalanceAfterTrans,
                                  CurrencyCode = x.CurrencyCode,
                                  Credite =x.Amount,
                                  Debit = 0,
                              }).ToList();

                tempHistory.AddRange(tempL2);

                var listGroup = from x in tempHistory
                                group x by new { x.AccountCode, x.CurrencyCode } into g
                                select new ReportAccountBalanceDayTemp()
                                {
                                    AccountCode = g.Key.AccountCode,
                                    AccountInfo = g.Key.CurrencyCode,
                                    Credited = g.Sum(c => c.Credite),
                                    Debit = g.Sum(c => c.Debit),
                                    MaxDate = g.Max(c => c.CreatedDay),
                                    MinDate = g.Min(c => c.CreatedDay),
                                };


                var lstHistorys = (from g in listGroup
                                   join minG in tempHistory on g.AccountCode equals minG.AccountCode
                                   join maxG in tempHistory on g.AccountCode equals maxG.AccountCode
                                   where g.MinDate == minG.CreatedDay && g.MaxDate == maxG.CreatedDay
                                   && g.AccountInfo == minG.CurrencyCode && g.AccountInfo == maxG.CurrencyCode
                                   select new ReportCheckBalanceText()
                                   {
                                       AccountCode = g.AccountCode,
                                       CurrencyCode = g.AccountInfo,
                                       Credited = Math.Round(g.Credited, 0),
                                       Debit = Math.Round(g.Debit, 0),
                                       BalanceBefore = Math.Round(minG.BalanceBefore, 0),
                                       BalanceAfter = Math.Round(maxG.BalanceAfter, 0),
                                       TextDay = "",
                                   }).ToList();


                var lstBalances = (from x in searchDataBalance
                                   where x.AccountType == "CUSTOMER"
                                   select new ReportCheckBalanceText
                                   {
                                       AccountCode = x.AccountCode,
                                       AccountInfo = x.AccountInfo,
                                       CurrencyCode = x.CurrencyCode,
                                       BalanceBefore = x.BalanceBefore,
                                       BalanceAfter = x.BalanceAfter,
                                       Debit = x.Debit,
                                       Credited = x.Credite,
                                       TextDay = x.TextDay
                                   }).ToList();

                var listTemps = from x in lstHistorys
                                join b in lstBalances on x.AccountCode equals b.AccountCode
                                where x.CurrencyCode == b.CurrencyCode
                                && (x.BalanceAfter != b.BalanceAfter || x.BalanceBefore != b.BalanceBefore || x.Credited != b.Credited || x.Debit != b.Debit
                                || x.BalanceBefore + x.Credited - x.Debit - x.BalanceAfter != 0
                                || b.BalanceBefore + b.Credited - b.Debit - b.BalanceAfter != 0)
                                
                                select new { x, b };

                var lstUpdate = new List<ReportCheckBalanceText>();
                foreach (var item in listTemps)
                {
                    if ((item.x.BalanceBefore + item.x.Credited - item.x.Debit) == item.x.BalanceAfter)
                    {
                        lstUpdate.Add(new ReportCheckBalanceText()
                        {
                            BalanceBefore = item.x.BalanceBefore,
                            Credited = item.x.Credited,
                            Debit = item.x.Debit,
                            BalanceAfter = item.x.BalanceAfter,
                            CurrencyCode = item.b.CurrencyCode,
                            AccountCode = item.b.AccountCode,
                            TextDay = item.b.TextDay,
                        });
                    }
                }

                var lst = new ReportCheckBalance()
                {
                    Historys = (from x in lstHistorys
                                where (x.BalanceBefore + x.Credited - x.Debit) != x.BalanceAfter
                                select x).ToList(),
                    Balances = (from x in lstBalances
                                where (x.BalanceBefore + x.Credited - x.Debit) != x.BalanceAfter
                                select x).ToList(),
                    UpdateBalances = lstUpdate,
                    BalanceOtherHistory = (from x in listTemps
                                           select new ReportCheckBalanceText()
                                           {
                                               AccountCode = x.b.AccountCode,
                                               AccountInfo = x.b.AccountInfo,
                                               BalanceAfter = x.x.BalanceAfter - x.b.BalanceAfter,
                                               BalanceBefore = x.x.BalanceBefore - x.b.BalanceBefore,
                                               Debit = x.x.Debit - x.b.Debit,
                                               Credited = x.x.Credited - x.b.Credited
                                           }).ToList()
                };

                #endregion

                return lst;
            }
            catch (Exception e)
            {
                _logger.LogError($"CheckReportBalanceAndHistory error: {e}");
                _logger.LogInformation($"KeyCode= {keyCode} .Tong thoi gian den khi Exception Seconds: {DateTime.Now.Subtract(dateStart).TotalSeconds}");
                return new ReportCheckBalance();
            }
        }

        public async Task<List<ReportAccountBalanceDayInfo>> GetReportBalanceHistory(DateTime fDate, DateTime tDate, string accountCode)
        {
            var keyCode = "GetReportBalanceHistory_" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
            var dateStart = DateTime.Now;

            try
            {
                var fromDate = fDate.Date.ToUniversalTime();
                var toDate = tDate.Date.AddDays(1).ToUniversalTime();
                var dateTemp = DateTime.Now;
                var searchData = new List<ReportBalanceHistories>();
                _logger.LogInformation($"KeyCode= {keyCode} StartUp SearchData ");
                var query = new SearchDescriptor<ReportBalanceHistories>();
                query.Index(ReportIndex.ReportBalanceHistoriesIndex).Query(q => q.Bool(b =>
                    b.Must(mu => mu.DateRange(r => r.Field(f => f.CreatedDate).GreaterThanOrEquals(fromDate).LessThan(toDate))
                          , mu => mu.MultiMatch(m => m.Fields(f => f.Field(c => c.SrcAccountCode).Field(c => c.DesAccountCode)).Query(accountCode)))
                )).From(0).Size(10000).Scroll("5m");
                var scanResults = await _elasticClient.SearchAsync<ReportBalanceHistories>(query);
                ScrollBalanceHistories(scanResults, int.MaxValue, ref searchData);
                _logger.LogInformation($"KeyCode= {keyCode} .Lay xong du lieu Seconds: {DateTime.Now.Subtract(dateTemp).TotalSeconds}");
                #region 1.Phần xử lý dữ liệu lấy ra của lịch xử giao dịch

                var tempHistory = (from x in searchData
                                   where x.SrcAccountType == "CUSTOMER"
                                   select new ReportAccountBalanceDay
                                   {
                                       AccountCode = x.SrcAccountCode,
                                       CreatedDay = x.CreatedDate,
                                       BalanceBefore =x.SrcAccountBalanceBeforeTrans,
                                       BalanceAfter = x.SrcAccountBalanceAfterTrans,
                                       CurrencyCode = x.CurrencyCode,
                                       Debit =x.Amount,
                                       Credite = 0
                                   }).ToList();

                var tempL2 = (from x in searchData
                              where x.DesAccountType == "CUSTOMER"
                              select new ReportAccountBalanceDay
                              {
                                  AccountCode = x.DesAccountCode,
                                  CreatedDay = x.CreatedDate,
                                  BalanceBefore =x.DesAccountBalanceBeforeTrans,
                                  BalanceAfter =x.DesAccountBalanceAfterTrans,
                                  CurrencyCode = x.CurrencyCode,
                                  Credite = x.Amount,
                                  Debit = 0
                              }).ToList();

                tempHistory.AddRange(tempL2);

                var listGroup = from x in tempHistory
                                group x by new { x.AccountCode, x.CurrencyCode } into g
                                select new ReportAccountBalanceDayTemp()
                                {
                                    AccountCode = g.Key.AccountCode,
                                    AccountInfo = g.Key.CurrencyCode,
                                    Credited = g.Sum(c => c.Credite),
                                    Debit = g.Sum(c => c.Debit),
                                    MaxDate = g.Max(c => c.CreatedDay),
                                    MinDate = g.Min(c => c.CreatedDay),
                                };


                var lstHistorys = (from g in listGroup
                                   join minG in tempHistory on g.AccountCode equals minG.AccountCode
                                   join maxG in tempHistory on g.AccountCode equals maxG.AccountCode
                                   where g.MinDate == minG.CreatedDay && g.MaxDate == maxG.CreatedDay
                                   && g.AccountInfo == minG.CurrencyCode && g.AccountInfo == maxG.CurrencyCode
                                   select new ReportAccountBalanceDayInfo()
                                   {
                                       AccountCode = g.AccountCode,
                                       Credited = Math.Round(g.Credited, 0),
                                       Debit = Math.Round(g.Debit, 0),
                                       BalanceBefore = Math.Round(minG.BalanceBefore, 0),
                                       BalanceAfter = Math.Round(maxG.BalanceAfter, 0),
                                   }).ToList();




                #endregion

                return lstHistorys;
            }
            catch (Exception e)
            {
                _logger.LogError($"GetReportBalanceHistory error: {e}");
                _logger.LogInformation($"KeyCode= {keyCode} .Tong thoi gian den khi Exception Seconds: {DateTime.Now.Subtract(dateStart).TotalSeconds}");
                return new List<ReportAccountBalanceDayInfo>();
            }
        }

        public async Task<MessagePagedResponseBase> GetCardStockHistories(CardStockHistoriesRequest request)
        {
            try
            {
                if (request.ToDate != null) request.ToDate = request.ToDate.Value.Date.AddDays(1).AddSeconds(-1);
                var query = new SearchDescriptor<ReportCardStockHistories>();
                var fromDate = request.FromDate.Value.ToUniversalTime();
                var toDate = request.ToDate.Value.AddDays(1).ToUniversalTime();
                string storeCode = request.StockCode ?? string.Empty;
                string productCode = request.ProductCode ?? string.Empty;
                string categoryCode = request.CategoryCode ?? string.Empty;
                string cardValue = string.Empty;
                string stockType = request.StockType ?? string.Empty;

                string vendor = request.Vendor ?? string.Empty;
                if (request.CardValue > 0)
                    cardValue = request.CardValue.ToString();


                query.Index(ReportIndex.ReportCardstockHistoriesIndex).Query(q => q.Bool(b =>
                    b.Must(mu => mu.DateRange(r => r.Field(f => f.CreatedDate).GreaterThanOrEquals(fromDate).LessThan(toDate))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.CardValue).Query(cardValue))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.Vendor).Query(vendor))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.StockCode).Query(storeCode))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.CategoryCode).Query(categoryCode))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.ProductCode).Query(productCode))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.StockType).Query(stockType))
                    )
                ));

                query.From(0).Size(10000).Scroll("3m");
                var searchData = new List<ReportCardStockHistories>();
                var scanResults = await _elasticClient.SearchAsync<ReportCardStockHistories>(query);
                ScrollCardStockHistoriesByDate(scanResults, request.Offset + request.Limit, ref searchData);
                var total = int.Parse((await _elasticClient.SearchAsync<ReportCardStockHistories>(query)).Total.ToString());

                foreach (var item in searchData)
                    item.CreatedDate = _dateHepper.ConvertToUserTime(item.CreatedDate, DateTimeKind.Utc);

                return new MessagePagedResponseBase
                {
                    ResponseCode = ResponseCodeConst.Success,
                    ResponseMessage = "Thành công",
                    Total = (int)total,
                    Payload = searchData.OrderBy(x => x.CreatedDate).ThenBy(x => x.StockCode).ThenBy(x => x.Vendor)
                        .ThenBy(x => x.CardValue).ConvertTo<List<ReportCardStockHistoriesDto>>()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"GetCardStockHistories error {ex}");
                return new MessagePagedResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error
                };
            }
        }
    }
}