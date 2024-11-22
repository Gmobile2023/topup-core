using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Topup.Report.Domain.Exporting;
using Topup.Report.Model.Dtos;
using Topup.Report.Model.Dtos.RequestDto;
using Topup.Report.Model.Dtos.ResponseDto;
using Topup.Shared;
using Topup.Shared.CacheManager;
using Topup.Shared.EsIndexs;
using Topup.Shared.Helpers;
using Microsoft.Extensions.Logging;
using Nest;
using ServiceStack;
using Topup.Report.Domain.Entities;

namespace Topup.Report.Domain.Repositories
{
    public partial class ElasticReportRepository : IElasticReportRepository
    {
        /// <summary>
        ///     6.Báo cáo chi tiết chuyển tiền
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<MessagePagedResponseBase> ReportTransferDetailGetList(ReportTransferDetailRequest request)
        {
            try
            {
                var services = new List<string>
            {
                ReportServiceCode.TRANSFER.ToLower()
            };

                var accountType = new int[0];
                var agentType = string.Empty;
                var status = new int[1] { 1 };

                var query = new SearchDescriptor<ReportItemDetail>();
                var fromDate = request.FromDate.Value.ToUniversalTime();
                var toDate = request.ToDate.Value.AddDays(1).ToUniversalTime();
                var agentTransferCode = "";
                var agentReceiveCode = "";
                if (!string.IsNullOrEmpty(request.AgentTransferCode))
                    agentTransferCode = request.AgentTransferCode;

                if (!string.IsNullOrEmpty(request.AgentReceiveCode))
                    agentReceiveCode = request.AgentReceiveCode;

                if (request.AccountType == 1)
                    accountType = new[] { 1, 2, 3 };
                else if (request.AccountType == 2)
                    accountType = new[] { 4 };

                if (request.AgentType > 0)
                    agentType = request.AgentType.ToString();

                query.Index(ReportIndex.ReportItemDetailIndex).Query(q => q.Bool(b =>
                    b.Must(mu =>
                            mu.DateRange(r => r.Field(f => f.CreatedTime).GreaterThanOrEquals(fromDate).LessThan(toDate))
                        , mu => mu.Match(m => m.Field(f => f.TransCode).Query(request.TransCode))
                        , mu => mu.Match(m => m.Field(f => f.PerformAccount).Query(agentTransferCode))
                        , mu => mu.Match(m => m.Field(f => f.AccountCode).Query(agentReceiveCode))
                        , mu => mu.Match(m => m.Field(f => f.AccountAgentType).Query(agentType))
                        , mu => mu.Terms(m => m.Field(f => f.ServiceCode).Terms(services))
                        , mu => mu.Terms(m => m.Field(f => f.AccountAccountType).Terms(accountType))
                        , mu => mu.Terms(m => m.Field(f => f.Status).Terms(status))
                    )
                ));

                var totalQuery = query;
                query.Aggregations(agg => agg.Sum("Price", s => s.Field(p => p.TotalPrice))).Sort(c => c.Descending(i => i.CreatedTime));

                if (request.Offset + request.Limit < 10000)
                    query.From(0).Size(request.Offset + request.Limit).Scroll("5m");
                else query.From(0).Size(10000).Scroll("5m");

                var searchData = new List<ReportItemDetail>();
                var scanResults = await _elasticClient.SearchAsync<ReportItemDetail>(query);
                var fPrice = scanResults.Aggregations.GetValueOrDefault("Price");
                var price = fPrice.ConvertTo<ValueTeam>();
                var sumData = new ReportTransferDetailDto
                {
                    Price = Convert.ToDecimal(price.Value)
                };

                ScrollReportDetail(scanResults, request.Offset + request.Limit, ref searchData);

                totalQuery.From(0).Size(1000).Scroll("3m");
                var total = int.Parse((await _elasticClient.SearchAsync<ReportItemDetail>(totalQuery)).Total.ToString());
                var listView = searchData.Skip(request.Offset).Take(request.Limit);

                var list = (from x in listView
                            select new ReportTransferDetailDto
                            {
                                AgentType = !string.IsNullOrEmpty(x.AccountInfo) ? x.AccountAgentType : 0,
                                AgentTypeName = GetAgenTypeName(x.AccountAgentType),
                                AgentReceiveCode = x.AccountCode,
                                AgentReceiveInfo = !string.IsNullOrEmpty(x.AccountInfo)
                                    ? x.AccountCode + " - " + x.AccountInfo : x.AccountCode,
                                AgentTransfer = x.PerformAccount,
                                AgentTransferInfo = !string.IsNullOrEmpty(x.PerformInfo)
                                    ? x.PerformAccount + " - " + x.PerformInfo : x.PerformAccount,
                                Price = Convert.ToDecimal(Math.Round(x.TotalPrice, 0)),
                                ServiceCode = x.ServiceCode,
                                ServiceName = x.ServiceName,
                                Messager = x.TransNote,
                                CreatedTime = _dateHepper.ConvertToUserTime(x.CreatedTime, DateTimeKind.Utc),
                                TransCode = string.IsNullOrEmpty(x.RequestRef) ? x.TransCode : x.RequestRef
                            }).ToList();

                return new MessagePagedResponseBase
                {
                    ResponseCode = ResponseCodeConst.Success,
                    ResponseMessage = "Thành công",
                    SumData = sumData,
                    Total = total,
                    Payload = list
                };
            }
            catch (Exception e)
            {
                _logger.LogError($"ReportTransferDetailGetList error: {e}");
                return new MessagePagedResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error
                };
            }
        }

        /// <summary>
        ///     7.Báo cáo tổng hợp doanh số theo tỉnh
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<MessagePagedResponseBase> ReportRevenueCityGetList(ReportRevenueCityRequest request)
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
                var status = new int[1] { 1 };

                var query = new SearchDescriptor<ReportItemDetail>();
                var fromDate = request.FromDate.Value.ToUniversalTime();
                var toDate = request.ToDate.Value.AddDays(1).ToUniversalTime();
                var city = "";
                var distrinct = "";
                var ward = "";
                var loginCode = "";
                var agentType = "";

                var serviceCode = new List<string>();
                var categoryCode = new List<string>();
                var productCode = new List<string>();
                if (request.ServiceCode != null && request.ServiceCode.Count > 0)
                    foreach (var a in request.ServiceCode)
                        if (!string.IsNullOrEmpty(a))
                            serviceCode.Add(a.ToLower());

                if (request.CategoryCode != null && request.CategoryCode.Count > 0)
                    foreach (var a in request.CategoryCode)
                        if (!string.IsNullOrEmpty(a))
                            categoryCode.Add(a.ToLower());

                if (request.ProductCode != null && request.ProductCode.Count > 0)
                    foreach (var a in request.ProductCode)
                        if (!string.IsNullOrEmpty(a))
                            productCode.Add(a.ToLower());


                if (request.AccountType > 0)
                    loginCode = request.LoginCode;
                if (request.CityId > 0)
                    city = request.CityId.ToString();
                if (request.DistrictId > 0)
                    distrinct = request.DistrictId.ToString();
                if (request.WardId > 0)
                    ward = request.WardId.ToString();
                if (request.AgentType > 0)
                    agentType = request.AgentType.ToString();

                query.Index(ReportIndex.ReportItemDetailIndex).Query(q => q.Bool(b =>
                    b.Must(mu =>mu.DateRange(r => r.Field(f => f.CreatedTime).GreaterThanOrEquals(fromDate).LessThan(toDate))
                        , mu => mu.Terms(m => m.Field(f => f.ServiceCode).Terms(services))
                        , mu => mu.Terms(m => m.Field(f => f.TransType).Terms(services))
                        , mu => mu.Terms(m => m.Field(f => f.ServiceCode).Terms(serviceCode.ToArray()))
                        , mu => mu.Terms(m => m.Field(f => f.CategoryCode).Terms(categoryCode.ToArray()))
                        , mu => mu.Terms(m => m.Field(f => f.ProductCode).Terms(productCode.ToArray()))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.SaleCode).Query(request.UserSaleCode))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.SaleLeaderCode).Query(request.UserSaleLeaderCode))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.AccountCityId).Query(city))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.AccountDistrictId).Query(distrinct))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.AccountWardId).Query(ward))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.AccountAgentType).Query(agentType))
                        , mu => mu.MultiMatch(m =>m.Fields(f => f.Field(c => c.AccountCode).Field(c => c.SaleCode).Field(c => c.SaleLeaderCode)).Query(loginCode))
                        , mu => mu.Terms(m => m.Field(f => f.Status).Terms(status))
                    )
                ));

                query.Size(0).Aggregations(a => a
                    .MultiTerms("Accounts", ts => ts.Terms(
                            s => s.Field(c => c.AccountCode.Suffix("keyword")),
                            s => s.Field(c => c.AccountInfo.Suffix("keyword"))
                        ).Size(10000)
                        .Aggregations(c => c
                            .Sum("Price", i => i.Field(v => v.TotalPrice))
                            .Sum("Fee", i => i.Field(v => v.Fee))
                            .Sum("Quantity", i => i.Field(v => v.Quantity))
                            .Sum("Discount", i => i.Field(v => v.Discount))))
                );

                var scanResults = await _elasticClient.SearchAsync<ReportItemDetail>(query);
                var searchData = new List<ReportRevenueCityDto>();
                var states = scanResults.Aggregations.MultiTerms("Accounts");
                foreach (var bucket in states.Buckets)
                {
                    var tempDiscount = bucket.GetValueOrDefault("Discount");
                    var tempPrice = bucket.GetValueOrDefault("Price");
                    var tempFee = bucket.GetValueOrDefault("Fee");
                    var tempQuantity = bucket.GetValueOrDefault("Quantity");
                    var _dis = tempDiscount.ConvertTo<ValueTeam>();
                    var _fee = tempFee.ConvertTo<ValueTeam>();
                    var _price = tempPrice.ConvertTo<ValueTeam>();
                    var _quantity = tempQuantity.ConvertTo<ValueTeam>();
                    var key = bucket.Key.ToList();
                    searchData.Add(new ReportRevenueCityDto
                    {
                        AccountCode = key[0],
                        Quantity = Convert.ToDecimal(_quantity.Value),
                        Discount = Convert.ToDecimal(_dis.Value),
                        Fee = Convert.ToDecimal(_fee.Value),
                        Price = Convert.ToDecimal(_price.Value)
                    });
                }

                var accountArrays = searchData.Select(c => c.AccountCode).Distinct().ToList();
                var listAllAccount = await GetAccountByArrays(accountArrays);
                var listGroupAccount = from x in searchData
                                       join y in listAllAccount on x.AccountCode equals y.AccountCode into g
                                       from u in g.DefaultIfEmpty()
                                       select new ReportRevenueCityDto
                                       {
                                           CityInfo = u != null ? u.CityName : "",
                                           CityId = u != null ? u.CityId : 0,
                                           DistrictInfo = u != null ? u.DistrictName : "",
                                           DistrictId = u != null ? u.DistrictId : 0,
                                           WardInfo = u != null ? u.WardName : "",
                                           WardId = u != null ? u.WardId : 0,
                                           QuantityAgent = 1,
                                           Discount = Math.Round(x.Discount, 0),
                                           Quantity = x.Quantity,
                                           Price = Math.Round(x.Price, 0),
                                           Fee = Math.Round(x.Fee, 0)
                                       };

                var listGroup = (from x in listGroupAccount
                                 group x by new { x.CityInfo, x.CityId, x.DistrictInfo, x.DistrictId, x.WardInfo, x.WardId } into g
                                 select new ReportRevenueCityDto
                                 {
                                     CityInfo = g.Key.CityInfo,
                                     DistrictInfo = g.Key.DistrictInfo,
                                     WardInfo = g.Key.WardInfo,
                                     CityId = g.Key.CityId,
                                     DistrictId = g.Key.DistrictId,
                                     WardId = g.Key.WardId,
                                     QuantityAgent = g.Count(),
                                     Discount = Math.Round(g.Sum(c => c.Discount), 0),
                                     Quantity = g.Sum(c => c.Quantity),
                                     Price = Math.Round(g.Sum(c => c.Price), 0),
                                     Fee = Math.Round(g.Sum(c => c.Fee), 0)
                                 }).OrderBy(c => c.CityInfo)
                    .OrderBy(c => c.DistrictInfo)
                    .OrderBy(c => c.WardInfo);

                var total = listGroup.Count();
                var sumtotal = new ReportRevenueCityDto
                {
                    QuantityAgent = listGroup.Sum(c => c.QuantityAgent),
                    Discount = Math.Round(listGroup.Sum(c => c.Discount), 0),
                    Quantity = listGroup.Sum(c => c.Quantity),
                    Price = Math.Round(listGroup.Sum(c => c.Price), 0),
                    Fee = Math.Round(listGroup.Sum(c => c.Fee), 0)
                };
                var lst = listGroup.Skip(request.Offset).Take(request.Limit).ToList();

                return new MessagePagedResponseBase
                {
                    ResponseCode = ResponseCodeConst.Success,
                    ResponseMessage = "Thành công",
                    Total = total,
                    SumData = sumtotal,
                    Payload = lst
                };
            }
            catch (Exception e)
            {
                _logger.LogError($"ReportRevenueCityGetList error: {e}");
                return new MessagePagedResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error
                };
            }
        }

        /// <summary>
        ///     8.Báo cáo cân đối số dư
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<MessagePagedResponseBase> ReportAgentBalanceGetList(ReportAgentBalanceRequest request)
        {
            try
            {
                var query = new SearchDescriptor<ReportAccountBalanceDay>();
                var fromDate = request.FromDate.Value.ToUniversalTime();
                var fromDateLimit = request.FromDate.Value.AddDays(-35).ToUniversalTime();
                var toDate = request.ToDate.Value.AddDays(1).ToUniversalTime();
                var agentType = string.Empty;
                string accountCode = string.Empty;
                string saleLeaderCode = string.Empty;
                string saleCode = string.Empty;
                if (request.AgentType > 0)
                    agentType = request.AgentType.ToString();

                if (request.AccountType > 0)
                {
                    if (request.AccountType == 1 || request.AccountType == 2 || request.AccountType == 3 || request.AccountType == 4)
                        accountCode = request.LoginCode;
                    else if (request.AccountType == 5)
                        saleLeaderCode = request.LoginCode;
                    else if (request.AccountType == 6)
                        saleCode = request.LoginCode;
                }

                query.Index(ReportIndex.ReportAccountbalanceDayIndex).Query(q => q.Bool(b =>
                    b.Must(mu => mu.Match(m => m.Field(f => f.AccountCode).Query(request.AgentCode))
                        , mu => mu.DateRange(r => r.Field(f => f.CreatedDay).GreaterThanOrEquals(fromDateLimit).LessThan(toDate))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.CurrencyCode).Query("VND"))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.AccountType).Query("CUSTOMER"))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.AccountCode).Query(accountCode))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.SaleCode).Query(saleCode))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.SaleLeaderCode).Query(saleLeaderCode))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.AccountCode).Query(request.AgentCode))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.AgentType).Query(agentType))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.SaleLeaderCode).Query(request.UserSaleLeaderCode))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.SaleCode).Query(request.UserSaleCode))
                    )
                ));

                query.From(0).Size(10000).Scroll("3m");
                var searchData = new List<ReportAccountBalanceDay>();
                var scanResults = await _elasticClient.SearchAsync<ReportAccountBalanceDay>(query);
                ScrollAccountBalanceDay(scanResults, int.MaxValue, ref searchData);

                #region 1.Đầu kỳ

                var listBefore = searchData.Where(c => c.CreatedDay < request.FromDate.Value.ToUniversalTime());

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
                                         BeforeAmount = yc.BalanceAfter
                                     };

                #endregion

                #region 2.Cuối kỳ

                var listGroupAfter = from x in searchData
                                     group x by new { x.AccountCode } into g
                                     select new ReportAgentBalanceTemp()
                                     {
                                         AgentCode = g.Key.AccountCode,
                                         MaxDate = g.Max(c => c.CreatedDay),
                                     };

                var listViewAfter = from x in listGroupAfter
                                    join yc in searchData on x.AgentCode equals yc.AccountCode
                                    where x.MaxDate == yc.CreatedDay
                                    select new ReportAgentBalanceTemp()
                                    {
                                        AgentType = yc.AgentType ?? 1,
                                        AgentCode = x.AgentCode,
                                        AfterAmount = yc.BalanceAfter,
                                    };

                #endregion

                var listKy = searchData.Where(c => c.CreatedDay >= request.FromDate.Value.ToUniversalTime());


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
                                select new ReportAgentBalanceTemp()
                                {
                                    AgentType = c.AgentType,
                                    AgentTypeName =GetAgenTypeName(c.AgentType),
                                    AgentCode = c.AgentCode,
                                    InputAmount = ky?.InputAmount ?? 0,
                                    AmountUp = ky?.AmountUp ?? 0,
                                    SaleAmount = ky?.SaleAmount ?? 0,
                                    AmountDown = ky?.AmountDown ?? 0,
                                    BeforeAmount = before?.BeforeAmount ?? 0,
                                    AfterAmount = c.AfterAmount,
                                }).ToList();


                var total = listView.Count;
                var sumTotal = new ReportAgentBalanceDto()
                {
                    BeforeAmount = Math.Round(listView.Sum(c => c.BeforeAmount), 0),
                    AfterAmount = Math.Round(listView.Sum(c => c.AfterAmount), 0),
                    InputAmount = listView.Sum(c => c.InputAmount),
                    AmountUp = listView.Sum(c => c.AmountUp),
                    SaleAmount = listView.Sum(c => c.SaleAmount),
                    AmountDown = listView.Sum(c => c.AmountDown),
                };

                var lst = listView.OrderBy(c => c.AgentCode).Skip(request.Offset).Take(request.Limit).ToList();

                var agentCodes = lst.Where(c => !string.IsNullOrEmpty(c.AgentCode)).Select(c => c.AgentCode).Distinct()
                    .ToList();

                var lstSysAgent = await GetAccountByArrays(agentCodes);

                var accountSale = lstSysAgent.Where(c => !string.IsNullOrEmpty(c.SaleCode)).Select(c => c.SaleCode)
                    .Distinct().ToList();
                var accountLeaderSale = lstSysAgent.Where(c => !string.IsNullOrEmpty(c.LeaderCode))
                    .Select(c => c.LeaderCode).Distinct().ToList();
                accountSale.AddRange(accountLeaderSale);

                var lstSysLeader = await GetAccountByArrays(accountSale);

                var list = (from item in lst
                            join a in lstSysAgent on item.AgentCode equals a.AccountCode into ag
                            from account in ag.DefaultIfEmpty()
                            select new ReportAgentBalanceDto
                            {
                                AgentType = item.AgentType,
                                AgentTypeName = item.AgentTypeName,
                                AgentCode = item.AgentCode,
                                AgentInfo = account != null ? account.AccountCode + " - " + account.Mobile + " - " + account.FullName : "",
                                SaleCode = account != null ? account.SaleCode : string.Empty,
                                SaleInfo = "",
                                SaleLeaderCode = account != null ? account.LeaderCode : string.Empty,
                                SaleLeaderInfo = "",
                                AfterAmount = Math.Round(item.AfterAmount, 0),
                                BeforeAmount = Math.Round(item.BeforeAmount, 0),
                                InputAmount = item.InputAmount,
                                AmountUp = item.AmountUp,
                                SaleAmount = item.SaleAmount,
                                AmountDown = item.AmountDown
                            }).OrderBy(c => c.AgentCode).ToList();


                var listViewData = (from item in list
                                    join s in lstSysLeader on item.SaleCode equals s.AccountCode into sg
                                    from sale in sg.DefaultIfEmpty()
                                    join l in lstSysLeader on item.SaleLeaderCode equals l.AccountCode into lg
                                    from lead in lg.DefaultIfEmpty()
                                    select new ReportAgentBalanceDto
                                    {
                                        AgentType = item.AgentType,
                                        AgentTypeName = item.AgentTypeName,
                                        AgentCode = item.AgentCode,
                                        AgentInfo = item.AgentInfo,
                                        SaleCode = item.SaleCode,
                                        SaleInfo = sale != null
                                            ? sale.UserName + " - " + sale.Mobile + " - " + sale.FullName
                                            : string.Empty,
                                        SaleLeaderCode = item.SaleLeaderCode,
                                        SaleLeaderInfo = lead != null
                                            ? lead.UserName + " - " + lead.Mobile + " - " + lead.FullName
                                            : string.Empty,
                                        AfterAmount = Math.Round(item.AfterAmount, 0),
                                        BeforeAmount = Math.Round(item.BeforeAmount, 0),
                                        InputAmount = item.InputAmount,
                                        AmountUp = item.AmountUp,
                                        SaleAmount = item.SaleAmount,
                                        AmountDown = item.AmountDown
                                    }).OrderBy(c => c.AgentCode).ToList();


                return new MessagePagedResponseBase
                {
                    ResponseCode = ResponseCodeConst.Success,
                    ResponseMessage = "Thành công",
                    Total = total,
                    SumData = sumTotal,
                    Payload = listViewData
                };
            }
            catch (Exception e)
            {
                _logger.LogError($"ReportAgentBalanceGetList error: {e}");
                return new MessagePagedResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error
                };
            }
        }

        /// <summary>
        ///     9.Báo cáo doanh số của đại lý
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<MessagePagedResponseBase> ReportRevenueAgentGetList(ReportRevenueAgentRequest request)
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
                var status = new int[1] { 1 };

                var query = new SearchDescriptor<ReportItemDetail>();
                var fromDate = request.FromDate.Value.ToUniversalTime();
                var toDate = request.ToDate.Value.AddDays(1).ToUniversalTime();
                var city = "";
                var loginCode = "";
                var agentType = "";

                if (request.AccountType > 0)
                    loginCode = request.LoginCode;
                if (request.CityId > 0)
                    city = request.CityId.ToString();
                if (request.AgentType > 0)
                    agentType = request.AgentType.ToString();

                var serviceCode = new List<string>();
                var categoryCode = new List<string>();
                var productCode = new List<string>();
                if (request.ServiceCode != null && request.ServiceCode.Count > 0)
                    foreach (var a in request.ServiceCode)
                        if (!string.IsNullOrEmpty(a))
                            serviceCode.Add(a.ToLower());

                if (request.CategoryCode != null && request.CategoryCode.Count > 0)
                    foreach (var a in request.CategoryCode)
                        if (!string.IsNullOrEmpty(a))
                            categoryCode.Add(a.ToLower());

                if (request.ProductCode != null && request.ProductCode.Count > 0)
                    foreach (var a in request.ProductCode)
                        if (!string.IsNullOrEmpty(a))
                            productCode.Add(a.ToLower());


                query.Index(ReportIndex.ReportItemDetailIndex).Query(q => q.Bool(b =>
                    b.Must(mu =>
                            mu.DateRange(r => r.Field(f => f.CreatedTime).GreaterThanOrEquals(fromDate).LessThan(toDate))
                        , mu => mu.Terms(m => m.Field(f => f.ServiceCode).Terms(services))
                        , mu => mu.Terms(m => m.Field(f => f.TransType).Terms(services))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.AccountCode).Query(request.AgentCode))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.SaleCode).Query(request.UserSaleCode))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.SaleLeaderCode).Query(request.UserSaleLeaderCode))
                        , mu => mu.Terms(m => m.Field(f => f.ServiceCode).Terms(serviceCode.ToArray()))
                        , mu => mu.Terms(m => m.Field(f => f.CategoryCode).Terms(categoryCode.ToArray()))
                        , mu => mu.Terms(m => m.Field(f => f.ProductCode).Terms(productCode.ToArray()))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.AccountCityId).Query(city))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.AccountAgentType).Query(agentType))
                        , mu => mu.MultiMatch(m => m.Fields(f => f.Field(c => c.AccountCode).Field(c => c.SaleCode).Field(c => c.SaleLeaderCode)).Query(loginCode))
                        , mu => mu.Terms(m => m.Field(f => f.Status).Terms(status))
                    )
                ));

                query.Size(0)
                    .Aggregations(cs => cs.MultiTerms("Accounts",
                        s => s.CollectMode(TermsAggregationCollectMode.BreadthFirst)
                            .Terms(t => t.Field(c => c.AccountCode.Suffix("keyword")),
                                t => t.Field(c => c.AccountInfo.Suffix("keyword"))
                            ).Size(1000)
                            .Aggregations(c => c
                                .Sum("Price", i => i.Field(v => v.TotalPrice))
                                .Sum("Fee", i => i.Field(v => v.Fee))
                                .Sum("Quantity", i => i.Field(v => v.Quantity))
                                .Sum("Discount", i => i.Field(v => v.Discount))))
                    );

                var scanResults = await _elasticClient.SearchAsync<ReportItemDetail>(query);
                var searchData = new List<ReportRevenueAgentDto>();
                var accounts = scanResults.Aggregations.MultiTerms("Accounts");
                foreach (var account in accounts.Buckets)
                {
                    var tempDiscount = account.GetValueOrDefault("Discount");
                    var tempPrice = account.GetValueOrDefault("Price");
                    var tempFee = account.GetValueOrDefault("Fee");
                    var tempQty = account.GetValueOrDefault("Quantity");
                    var _dis = tempDiscount.ConvertTo<ValueTeam>();
                    var _fee = tempFee.ConvertTo<ValueTeam>();
                    var _price = tempPrice.ConvertTo<ValueTeam>();
                    var _qty = tempQty.ConvertTo<ValueTeam>();
                    var key = account.Key.ToList();
                    searchData.Add(new ReportRevenueAgentDto
                    {
                        AgentCode = key[0],
                        AgentInfo = key[0] + " - " + key[1],
                        Discount = Convert.ToDecimal(_dis.Value),
                        Fee = Convert.ToDecimal(_fee.Value),
                        Price = Convert.ToDecimal(_price.Value),
                        Quantity = Convert.ToDecimal(_qty.Value)
                    });
                }

                var total = searchData.Count();
                var sumtotal = new ReportRevenueAgentDto
                {
                    Quantity = searchData.Sum(c => c.Quantity),
                    Discount = searchData.Sum(c => c.Discount),
                    Price = searchData.Sum(c => c.Price),
                    Fee = searchData.Sum(c => c.Fee)
                };

                var litViews = searchData.OrderBy(c => c.AgentCode).Skip(request.Offset).Take(request.Limit);
                var lstAccounts = await GetAccountByArrays(new List<string>());

                var listAgent = (from g in litViews
                                 join a in lstAccounts on g.AgentCode equals a.AccountCode into ga
                                 from user in ga.DefaultIfEmpty()
                                 select new ReportRevenueAgentDto
                                 {
                                     AgentCode = g.AgentCode,
                                     AgentInfo = g.AgentInfo,
                                     AgentName = user != null ? user.AgentName : "",
                                     AgentTypeName = GetAgenTypeName(user != null ? user.AgentType : 0),
                                     CityInfo = user != null ? user.CityName : "",
                                     DistrictInfo = user != null ? user.DistrictName : "",
                                     WardInfo = user != null ? user.WardName : "",
                                     CityId = user != null ? user.CityId : 0,
                                     DistrictId = user != null ? user.DistrictId : 0,
                                     WardId = user != null ? user.WardId : 0,
                                     SaleCode = user != null ? user.SaleCode : string.Empty,
                                     LeaderCode = user != null ? user.LeaderCode : string.Empty,
                                     Quantity = g.Quantity,
                                     Discount = g.Discount,
                                     Fee = g.Fee,
                                     Price = g.Price
                                 }).ToList();


                var list = (from g in listAgent
                            join sl in lstAccounts on g.LeaderCode equals sl.AccountCode into gsl
                            from uslead in gsl.DefaultIfEmpty()
                            join s in lstAccounts on g.SaleCode equals s.AccountCode into gs
                            from suser in gs.DefaultIfEmpty()
                            select new ReportRevenueAgentDto
                            {
                                AgentCode = g.AgentCode,
                                AgentInfo = g.AgentInfo,
                                AgentName = g.AgentName,
                                AgentTypeName = g.AgentTypeName,
                                CityInfo = g.CityInfo,
                                DistrictInfo = g.CityInfo,
                                WardInfo = g.WardInfo,
                                CityId = g.CityId,
                                DistrictId = g.DistrictId,
                                WardId = g.WardId,
                                SaleCode = g.SaleCode,
                                SaleInfo = suser != null ? suser.UserName + " - " + suser.Mobile + " - " + suser.FullName : "",
                                LeaderCode = g.LeaderCode,
                                SaleLeaderInfo = uslead != null
                                    ? uslead.UserName + " - " + uslead.Mobile + " - " + uslead.FullName
                                    : "",
                                Quantity = g.Quantity,
                                Discount = g.Discount,
                                Fee = g.Fee,
                                Price = g.Price
                            }).ToList();

                return new MessagePagedResponseBase
                {
                    ResponseCode = ResponseCodeConst.Success,
                    ResponseMessage = "Thành công",
                    Total = total,
                    SumData = sumtotal,
                    Payload = list
                };
            }
            catch (Exception e)
            {
                _logger.LogError($"ReportRevenueAgentGetList error: {e}");
                return new MessagePagedResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error
                };
            }
        }

        /// <summary>
        ///     Báo cáo về kích hoạt tài khoản
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<MessagePagedResponseBase> ReportRevenueActiveGetList(ReportRevenueActiveRequest request)
        {
            try
            {
                #region 1.Query với thông tin về giao dịch

                var services = new List<string>
            {
                ReportServiceCode.DEPOSIT.ToLower(),
                ReportServiceCode.TOPUP.ToLower(),
                ReportServiceCode.TOPUP_DATA.ToLower(),
                ReportServiceCode.PAY_BILL.ToLower(),
                ReportServiceCode.PIN_CODE.ToLower(),
                ReportServiceCode.PIN_DATA.ToLower(),
                ReportServiceCode.PIN_GAME.ToLower()
            };
                var status = new int[1] { 1 };

                var query = new SearchDescriptor<ReportItemDetail>();
                var queryDeposit = new SearchDescriptor<ReportItemDetail>();
                var queryAccount = new SearchDescriptor<ReportAccountDto>();
                var fromDate = request.FromDate.Value.ToUniversalTime();
                var toDate = request.ToDate.Value.AddDays(1).ToUniversalTime();
                var accountTypes = new[] { "1", "2", "3" };
                string agentType = string.Empty;
                string accountCode = string.Empty;
                var city = "";
                var distrinct = "";
                var ward = "";
                var loginCode = "";

                if (request.AccountType > 0)
                    loginCode = request.LoginCode;
                if (request.CityId > 0)
                    city = request.CityId.ToString();
                if (request.DistrictId > 0)
                    distrinct = request.DistrictId.ToString();
                if (request.WardId > 0)
                    ward = request.WardId.ToString();
                if (request.AgentType > 0)
                    agentType = request.AgentType.ToString();


                query.Index(ReportIndex.ReportItemDetailIndex).Query(q => q.Bool(b =>
                    b.Must(mu => mu.DateRange(r => r.Field(f => f.CreatedTime).GreaterThanOrEquals(fromDate).LessThan(toDate))
                        , mu => mu.Terms(m => m.Field(f => f.ServiceCode).Terms(services))
                        , mu => mu.Terms(m => m.Field(f => f.TransType).Terms(services))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.AccountCode).Query(request.AgentCode))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.AccountCityId).Query(city))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.AccountDistrictId).Query(distrinct))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.AccountWardId).Query(ward))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.AccountAgentType).Query(agentType))
                        , mu => mu.MultiMatch(m =>m.Fields(f => f.Field(c => c.AccountCode).Field(c => c.SaleCode).Field(c => c.SaleLeaderCode)).Query(loginCode))
                        , mu => mu.Terms(m => m.Field(f => f.Status).Terms(status))
                    )
                ));

                queryDeposit.Index(ReportIndex.ReportItemDetailIndex).Query(q => q.Bool(b =>
                    b.Must(mu => mu.DateRange(r => r.Field(f => f.CreatedTime).GreaterThanOrEquals(fromDate).LessThan(toDate))
                        , mu => mu.Terms(m => m.Field(f => f.ServiceCode).Terms(ReportServiceCode.DEPOSIT.ToLower()))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.AccountCode).Query(request.AgentCode))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.AccountCityId).Query(city))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.AccountDistrictId).Query(distrinct))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.AccountWardId).Query(ward))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.AccountAgentType).Query(agentType))
                        , mu => mu.MultiMatch(m => m.Fields(f => f.Field(c => c.AccountCode).Field(c => c.SaleCode).Field(c => c.SaleLeaderCode)).Query(loginCode))
                        , mu => mu.Terms(m => m.Field(f => f.Status).Terms(status))
                    )
                ));


                query.Size(0)
                    .Aggregations(cs => cs.MultiTerms("Accounts",
                        s => s.CollectMode(TermsAggregationCollectMode.BreadthFirst)
                            .Terms(t => t.Field(c => c.AccountCode.Suffix("keyword")),
                                t => t.Field(c => c.AccountInfo.Suffix("keyword"))
                            ).Size(1000)
                            .Aggregations(c => c
                                .Sum("Price", i => i.Field(v => v.TotalPrice))
                                .Sum("Quantity", i => i.Field(v => v.Quantity))
                            )));

                queryDeposit.Size(0)
                    .Aggregations(cs => cs.MultiTerms("Accounts",
                        s => s.CollectMode(TermsAggregationCollectMode.BreadthFirst)
                            .Terms(t => t.Field(c => c.AccountCode.Suffix("keyword")),
                                t => t.Field(c => c.AccountInfo.Suffix("keyword"))
                            ).Size(1000)
                            .Aggregations(c => c
                                .Sum("Price", i => i.Field(v => v.TotalPrice))
                            )));


                var scanResults = await _elasticClient.SearchAsync<ReportItemDetail>(query);
                var scanDepositResults = await _elasticClient.SearchAsync<ReportItemDetail>(queryDeposit);
                var searchDataAccount = new List<ReportAccountDto>();
                var searchData = new List<ReportRevenueActiveDto>();
                var searchDataDeposit = new List<ReportRevenueActiveDto>();
                var accounts = scanResults.Aggregations.MultiTerms("Accounts");
                var accountsDeposit = scanDepositResults.Aggregations.MultiTerms("Accounts");
                foreach (var account in accounts.Buckets)
                {
                    var tempPrice = account.GetValueOrDefault("Price");
                    var _price = tempPrice.ConvertTo<ValueTeam>();
                    var key = account.Key.ToList();
                    searchData.Add(new ReportRevenueActiveDto
                    {
                        AgentCode = key[0],
                        Sale = Convert.ToDecimal(_price.Value),
                        Deposit = 0
                    });
                }

                foreach (var account in accountsDeposit.Buckets)
                {
                    var tempPrice = account.GetValueOrDefault("Price");
                    var _price = tempPrice.ConvertTo<ValueTeam>();
                    var key = account.Key.ToList();
                    searchDataDeposit.Add(new ReportRevenueActiveDto
                    {
                        AgentCode = key[0],
                        Deposit = Convert.ToDecimal(_price.Value),
                        Sale = 0
                    });
                }

                #endregion

                #region 2.Lấy thông tin tài khoản

                queryAccount.Index(ReportIndex.ReportaccountdtosIndex).Query(q => q.Bool(b =>
                   b.Must(mu => mu.Terms(m => m.Field(f => f.AccountType).Terms(accountTypes))
                       , mu => mu.MatchPhrase(m => m.Field(f => f.AccountCode).Query(request.AgentCode))
                       , mu => mu.MatchPhrase(m => m.Field(f => f.CityId).Query(city))
                       , mu => mu.MatchPhrase(m => m.Field(f => f.DistrictId).Query(distrinct))
                       , mu => mu.MatchPhrase(m => m.Field(f => f.WardId).Query(ward))
                       , mu => mu.MatchPhrase(m => m.Field(f => f.AgentType).Query(agentType))
                       , mu => mu.MultiMatch(m => m.Fields(f => f.Field(c => c.AccountCode).Field(c => c.SaleCode).Field(c => c.LeaderCode)).Query(loginCode))
                   )));

                queryAccount.From(0).Size(10000).Scroll("5m");
                var scanResultsAccount = await _elasticClient.SearchAsync<ReportAccountDto>(queryAccount);
                ScrollAccountInfo(scanResultsAccount, int.MaxValue, ref searchDataAccount);
                #endregion

                #region 3.Mapping Query

                //_logger.LogInformation($"searchData: {searchData.ToJson()}");
                //_logger.LogInformation($"searchDataDeposit: {searchDataDeposit.ToJson()}");

                #region 3.1.Report

                var listMapTrans = (from x in searchData
                                    join y in searchDataDeposit on x.AgentCode equals y.AgentCode into g
                                    from d in g.DefaultIfEmpty()
                                    select new ReportRevenueActiveDto
                                    {
                                        AgentCode = x.AgentCode,
                                        Deposit = d != null ? d.Deposit : 0,
                                        Sale = x.Sale - (d != null ? d.Deposit : 0)
                                    }).ToList();

                #endregion

                var listAllCompare = from c in searchDataAccount
                                     join r in listMapTrans on c.AccountCode equals r.AgentCode into gr
                                     from sg in gr.DefaultIfEmpty()
                                     select new ReportRevenueActiveDto
                                     {
                                         AgentCode = c.AccountCode,
                                         AgentInfo = c.AccountCode + " - " + c.Mobile + " - " + c.FullName,
                                         AgentName = c.AgentName,
                                         AgentTypeName = GetAgenTypeName(c.AgentType),
                                         IdIdentity = c.IdIdentity,
                                         CityInfo = c.CityName,
                                         DistrictInfo = c.DistrictName,
                                         WardInfo = c.WardName,
                                         CityId = c.CityId,
                                         DistrictId = c.DistrictId,
                                         WardId = c.WardId,
                                         SaleInfo = c.SaleCode,
                                         SaleLeaderInfo = c.LeaderCode,
                                         Deposit = sg != null ? sg.Deposit : 0,
                                         Sale = sg != null ? sg.Sale : 0
                                     };


                //Lọc theo trạng thái
                if (request.Status == 1)
                    listAllCompare = listAllCompare.Where(c => c.Deposit > 0 && c.Sale > 0);
                else if (request.Status == 2)
                    listAllCompare = listAllCompare.Where(c => c.Deposit <= 0 || c.Sale <= 0);


                //Tinh tổng
                var total = listAllCompare.Count();
                var sumData = new ReportRevenueActiveDto
                {
                    Sale = Math.Round(listAllCompare.Sum(c => c.Sale), 0),
                    Deposit = Math.Round(listAllCompare.Sum(c => c.Deposit), 0)
                };

                //Lấy số bản ghi cần hiển thị
                var lst = listAllCompare.OrderBy(c => c.AgentCode).Skip(request.Offset).Take(request.Limit).ToList();

                //Lấy ra sale,saleLead
                var saleCodes = lst.Where(c => !string.IsNullOrEmpty(c.SaleInfo)).Select(c => c.SaleInfo).Distinct()
                    .ToList();
                var saleLeads = lst.Where(c => !string.IsNullOrEmpty(c.SaleLeaderInfo)).Select(c => c.SaleLeaderInfo)
                    .Distinct().ToList();
                saleCodes.AddRange(saleLeads);

                var listAccountSales = await GetAccountByArrays(saleCodes);

                //Map sale,saleLead để trả về dữ liệu
                var listViewReponse = (from x in lst
                                       join s in listAccountSales on x.SaleInfo equals s.AccountCode into saleg
                                       from sAccountSale in saleg.DefaultIfEmpty()
                                       join l in listAccountSales on x.SaleLeaderInfo equals l.AccountCode into leadg
                                       from sAccountLead in leadg.DefaultIfEmpty()
                                       select new ReportRevenueActiveDto
                                       {
                                           AgentCode = x.AgentCode,
                                           AgentInfo = x.AgentInfo,
                                           AgentName = x.AgentName,
                                           AgentTypeName = x.AgentTypeName,
                                           IdIdentity = x.IdIdentity,
                                           CityInfo = x.CityInfo,
                                           DistrictInfo = x.DistrictInfo,
                                           WardInfo = x.WardInfo,
                                           CityId = x.CityId,
                                           DistrictId = x.DistrictId,
                                           WardId = x.WardId,
                                           Deposit = x.Deposit,
                                           Sale = x.Sale,
                                           Status = x.Sale > 0 && x.Deposit > 0 ? "Đạt" : "Không đạt",
                                           SaleInfo = sAccountSale != null
                                               ? sAccountSale.UserName + " - " + sAccountSale.Mobile + " - " + sAccountSale.FullName
                                               : string.Empty,
                                           SaleLeaderInfo = sAccountLead != null
                                               ? sAccountLead.UserName + " - " + sAccountLead.Mobile + " - " + sAccountLead.FullName
                                               : string.Empty
                                       }).ToList();


                return new MessagePagedResponseBase
                {
                    ResponseCode = ResponseCodeConst.Success,
                    ResponseMessage = "Thành công",
                    Total = total,
                    SumData = sumData,
                    Payload = listViewReponse
                };

                #endregion
            }
            catch (Exception e)
            {
                _logger.LogError($"ReportRevenueActiveGetList error: {e}");
                return new MessagePagedResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error
                };
            }
        }

        /// <summary>
        ///     Tổng hợp bán thẻ theo đại lý
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<MessagePagedResponseBase> ReportTotalSaleAgentGetList(ReportTotalSaleAgentRequest request)
        {
            try
            {
                var services = new List<string>
            {
                ReportServiceCode.PIN_CODE.ToLower(),
                ReportServiceCode.PIN_DATA.ToLower(),
                ReportServiceCode.PIN_GAME.ToLower()
            };
                var status = new int[1] { 1 };

                var query = new SearchDescriptor<ReportItemDetail>();
                var fromDate = request.FromDate.Value.ToUniversalTime();
                var toDate = request.ToDate.Value.AddDays(1).ToUniversalTime();

                var city = "";
                var distrinct = "";
                var ward = "";
                var loginCode = "";
                var agentType = "";
                var serviceCode = new List<string>();
                var categoryCode = new List<string>();
                var productCode = new List<string>();
                if (request.ServiceCode != null && request.ServiceCode.Count > 0)
                    foreach (var a in request.ServiceCode)
                        if (!string.IsNullOrEmpty(a))
                            serviceCode.Add(a.ToLower());

                if (request.CategoryCode != null && request.CategoryCode.Count > 0)
                    foreach (var a in request.CategoryCode)
                        if (!string.IsNullOrEmpty(a))
                            categoryCode.Add(a.ToLower());

                if (request.ProductCode != null && request.ProductCode.Count > 0)
                    foreach (var a in request.ProductCode)
                        if (!string.IsNullOrEmpty(a))
                            productCode.Add(a.ToLower());

                if (request.AccountType > 0)
                    loginCode = request.LoginCode;
                if (request.CityId > 0)
                    city = request.CityId.ToString();
                if (request.DistrictId > 0)
                    distrinct = request.DistrictId.ToString();
                if (request.WardId > 0)
                    ward = request.WardId.ToString();
                if (request.AgentType > 0)
                    agentType = request.AgentType.ToString();


                query.Index(ReportIndex.ReportItemDetailIndex).Query(q => q.Bool(b =>
                    b.Must(mu => mu.DateRange(r => r.Field(f => f.CreatedTime).GreaterThanOrEquals(fromDate).LessThan(toDate))
                        , mu => mu.Terms(m => m.Field(f => f.ServiceCode).Terms(services))
                        , mu => mu.Terms(m => m.Field(f => f.TransType).Terms(services))
                        , mu => mu.Terms(m => m.Field(f => f.ServiceCode).Terms(serviceCode.ToArray()))
                        , mu => mu.Terms(m => m.Field(f => f.CategoryCode).Terms(categoryCode.ToArray()))
                        , mu => mu.Terms(m => m.Field(f => f.ProductCode).Terms(productCode.ToArray()))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.SaleCode).Query(request.UserSaleCode))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.SaleLeaderCode).Query(request.UserSaleLeaderCode))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.AccountCityId).Query(city))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.AccountDistrictId).Query(distrinct))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.AccountWardId).Query(ward))
                        , mu => mu.MatchPhrase(m => m.Field(f => f.AccountAgentType).Query(agentType))
                        , mu => mu.MultiMatch(m => m.Fields(f => f.Field(c => c.AccountCode).Field(c => c.SaleCode).Field(c => c.SaleLeaderCode)).Query(loginCode))
                        , mu => mu.Match(m => m.Field(f => f.AccountCode).Query(request.AgentCode))
                        , mu => mu.Terms(m => m.Field(f => f.Status).Terms(status))
                    )));


                query.Size(0)
                    .Aggregations(cs => cs.MultiTerms("Accounts",
                        s => s.CollectMode(TermsAggregationCollectMode.BreadthFirst)
                            .Terms(t => t.Field(c => c.AccountCode.Suffix("keyword")),
                                t => t.Field(c => c.AccountInfo.Suffix("keyword"))
                            ).Size(10000)
                            .Aggregations(c => c
                                .Sum("Price", i => i.Field(v => v.TotalPrice))
                                .Sum("Fee", i => i.Field(v => v.Fee))
                                .Sum("Quantity", i => i.Field(v => v.Quantity))
                                .Sum("Discount", i => i.Field(v => v.Discount))))
                    );


                var scanResults = await _elasticClient.SearchAsync<ReportItemDetail>(query);
                var searchData = new List<ReportTotalSaleAgentDto>();
                var accounts = scanResults.Aggregations.MultiTerms("Accounts");
                foreach (var account in accounts.Buckets)
                {
                    var tempDiscount = account.GetValueOrDefault("Discount");
                    var tempPrice = account.GetValueOrDefault("Price");
                    var tempFee = account.GetValueOrDefault("Fee");
                    var tempQty = account.GetValueOrDefault("Quantity");
                    var _dis = tempDiscount.ConvertTo<ValueTeam>();
                    var _fee = tempFee.ConvertTo<ValueTeam>();
                    var _price = tempPrice.ConvertTo<ValueTeam>();
                    var _qty = tempQty.ConvertTo<ValueTeam>();
                    var key = account.Key.ToList();
                    searchData.Add(new ReportTotalSaleAgentDto
                    {
                        AgentCode = key[0],
                        AgentInfo = key[0] + " - " + key[1],
                        Discount = Convert.ToDecimal(_dis.Value),
                        Fee = Convert.ToDecimal(_fee.Value),
                        Price = Convert.ToDecimal(_price.Value),
                        Quantity = Convert.ToInt32(_qty.Value)
                    });
                }

                var lstAccounts = await GetAccountByArrays(new List<string>());

                var list = from g in searchData
                           join u in lstAccounts on g.AgentCode equals u.AccountCode into gg
                           from user in gg.DefaultIfEmpty()
                           select new ReportTotalSaleAgentDto
                           {
                               AgentCode = g.AgentCode,
                               AgentInfo = g.AgentInfo,
                               AgentName = user != null ? user.AgentName : "",
                               AgentTypeName = GetAgenTypeName(user != null ? user.AgentType : 0),
                               CityInfo = user != null ? user.CityName : "",
                               DistrictInfo = user != null ? user.DistrictName : "",
                               WardInfo = user != null ? user.WardName : "",
                               WardId = user != null ? user.WardId : 0,
                               DistrictId = user != null ? user.DistrictId : 0,
                               CityId = user != null ? user.CityId : 0,
                               SaleCode = user != null ? user.SaleCode : string.Empty,
                               SaleInfo = "",
                               LeaderCode = user != null ? user.LeaderCode : string.Empty,
                               SaleLeaderInfo = string.Empty,
                               Quantity = g.Quantity,
                               Discount = g.Discount,
                               Fee = g.Fee,
                               Price = g.Price
                           };


                var total = list.Count();
                var sumtotal = new ReportTotalSaleAgentDto
                {
                    Quantity = list.Sum(c => c.Quantity),
                    Discount = Math.Round(list.Sum(c => c.Discount), 0),
                    Price = Math.Round(list.Sum(c => c.Price), 0),
                    Fee = Math.Round(list.Sum(c => c.Fee), 0)
                };
                var lst = list.OrderBy(c => c.AgentCode).Skip(request.Offset).Take(request.Limit).ToList();

                var lst1 = (from x in lst
                            join y in lstAccounts on x.SaleCode equals y.AccountCode into gs
                            from sale in gs.DefaultIfEmpty()
                            join l in lstAccounts on x.LeaderCode equals l.AccountCode into gl
                            from leader in gl.DefaultIfEmpty()
                            select new ReportTotalSaleAgentDto
                            {
                                AgentCode = x.AgentCode,
                                AgentInfo = x.AgentInfo,
                                AgentName = x.AgentName,
                                AgentTypeName = x.AgentTypeName,
                                CityInfo = x.CityInfo,
                                DistrictInfo = x.DistrictInfo,
                                WardInfo = x.WardInfo,
                                WardId = x.WardId,
                                DistrictId = x.DistrictId,
                                CityId = x.CityId,
                                SaleCode = x.SaleCode,
                                SaleInfo = sale != null ? sale.UserName + " - " + sale.Mobile + " - " + sale.FullName : "",
                                LeaderCode = x.LeaderCode,
                                SaleLeaderInfo = leader != null
                                    ? leader.UserName + " - " + leader.Mobile + " - " + leader.FullName
                                    : "",
                                Quantity = x.Quantity,
                                Discount = x.Discount,
                                Fee = x.Fee,
                                Price = x.Price
                            }).ToList();

                return new MessagePagedResponseBase
                {
                    ResponseCode = ResponseCodeConst.Success,
                    ResponseMessage = "Thành công",
                    Total = total,
                    SumData = sumtotal,
                    Payload = lst1
                };
            }
            catch (Exception e)
            {
                _logger.LogError($"ReportTotalSaleAgentGetList error: {e}");
                return new MessagePagedResponseBase
                {
                    ResponseCode = ResponseCodeConst.Error
                };
            }
        }


    }
}