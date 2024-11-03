using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HLS.Paygate.Report.Domain.Entities;
using HLS.Paygate.Report.Model.Dtos;
using HLS.Paygate.Report.Model.Dtos.RequestDto;
using HLS.Paygate.Report.Model.Dtos.ResponseDto;
using HLS.Paygate.Shared;
using HLS.Paygate.Shared.EsIndexs;
using Microsoft.Extensions.Logging;
using Nest;
using ServiceStack;

namespace HLS.Paygate.Report.Domain.Repositories;

public partial class ElasticReportRepository
{
    public async Task<MessagePagedResponseBase> ReportCommissionDetailGetList(ReportCommissionDetailRequest request)
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

            var status = new string[1] { "1" };
            var statusCommisstion = new string[0];

            var agentType = "";
            var transCode = request.TransCode ?? string.Empty;
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

            var agentCode = request.AgentCode ?? string.Empty;
            var agentCodeSum = request.AgentCodeSum ?? string.Empty;

            if (request.Status >= 0)
            {
                if (request.Status == 0)
                    statusCommisstion = new[] { "0" };
                else if (request.Status == 1)
                    statusCommisstion = new[] { "1" };
            }

            var query = new SearchDescriptor<ReportItemDetail>();
            var fromDate = request.FromDate.Value.ToUniversalTime();
            var toDate = request.ToDate.Value.AddDays(1).ToUniversalTime();

            query.Index(ReportIndex.ReportItemDetailIndex).Query(q => q.Bool(b =>
                b.Must(mu => mu.Match(m => m.Field(f => f.AccountCode).Query(agentCode))
                    , mu => mu.DateRange(
                        r => r.Field(f => f.CreatedTime).GreaterThanOrEquals(fromDate).LessThan(toDate))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.ParentCode).Query(agentCodeSum))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.AccountAgentType).Query(agentType))
                    , mu => mu.MultiMatch(m =>
                        m.Fields(f =>
                                f.Field(c => c.TransCode).Field(c => c.RequestRef).Field(c => c.CommissionPaidCode))
                            .Query(transCode))
                    , mu => mu.MultiMatch(m =>
                        m.Fields(f =>
                                f.Field(c => c.TransCode).Field(c => c.RequestRef).Field(c => c.CommissionPaidCode))
                            .Query(request.Filter))
                    , mu => mu.Terms(m => m.Field(f => f.ServiceCode).Terms(services))
                    , mu => mu.Terms(m => m.Field(f => f.TransType).Terms(services))
                    , mu => mu.Terms(m => m.Field(f => f.ServiceCode).Terms(serviceCode.ToArray()))
                    , mu => mu.Terms(m => m.Field(f => f.CategoryCode).Terms(categoryCode.ToArray()))
                    , mu => mu.Terms(m => m.Field(f => f.ProductCode).Terms(productCode.ToArray()))
                    , mu => mu.Terms(m => m.Field(f => f.Status).Terms(status))
                    , mu => mu.Match(m => m.Field(f => f.AccountAgentType).Query("5"))
                    , mu => mu.Terms(m => m.Field(f => f.CommissionStatus).Terms(statusCommisstion))
                )
            ));


            var totalQuery = query;
            query.Aggregations(agg => agg
                .Sum("Commission", s => s.Field(p => p.CommissionAmount))
            ).Sort(c => c.Descending(i => i.CreatedTime));

            if (request.Offset + request.Limit <= 10000)
                query.From(0).Size(request.Offset + request.Limit).Scroll("5s");
            else query.From(0).Size(10000).Scroll("5s");

            var searchData = new List<ReportItemDetail>();
            var scanResults = await _elasticClient.SearchAsync<ReportItemDetail>(query);
            var fAmount = scanResults.Aggregations.GetValueOrDefault("Commission");
            var _fAmount = fAmount.ConvertTo<ValueTeam>();
            var sumData = new ReportCommissionDetailDto
            {
                CommissionAmount = _fAmount.Value
            };

            ScrollReportDetail(scanResults, request.Offset + request.Limit, ref searchData);

            totalQuery.From(0).Size(10000).Scroll("5s");
            var total = int.Parse((await _elasticClient.SearchAsync<ReportItemDetail>(totalQuery)).Total.ToString());

            var listView = searchData.Skip(request.Offset).Take(request.Limit);

            var list = from g in listView.ToList()
                       select new ReportCommissionDetailDto
                       {
                           AgentSumCode = g.ParentCode,
                           AgentSumInfo = g.ParentName,
                           CommissionAmount = g.CommissionAmount ?? 0,
                           CommissionCode = g.CommissionPaidCode,
                           StatusName = g.CommissionStatus == 1 ? "Đã trả" : "Chưa trả",
                           Status = g.CommissionStatus ?? 0,
                           AgentCode = g.AccountCode,
                           AgentInfo = !string.IsNullOrEmpty(g.AccountInfo) ? g.AccountInfo : "",
                           RequestRef = g.RequestRef,
                           TransCode = g.TransCode,
                           ServiceCode = g.ServiceCode,
                           ServiceName = g.ServiceName,
                           CategoryName = g.CategoryName,
                           ProductName = g.ProductName,
                           CreateDate = g.CreatedTime,
                           PayDate = g.CommissionDate
                       };

            return new MessagePagedResponseBase
            {
                ResponseCode = "01",
                ResponseMessage = "Thành công",
                SumData = sumData,
                Total = total,
                Payload = list
            };
        }
        catch (Exception e)
        {
            _logger.LogError($"ReportCommissionDetailGetList error: {e}");
            return new MessagePagedResponseBase
            {
                ResponseCode = "00"
            };
        }
    }

    public async Task<MessagePagedResponseBase> ReportCommissionTotalGetList(ReportCommissionTotalRequest request)
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

            query.Index(ReportIndex.ReportItemDetailIndex).Query(q => q.Bool(b =>
                b.Must(mu => mu.DateRange(r =>
                        r.Field(f => f.CreatedTime).GreaterThanOrEquals(fromDate).LessThanOrEquals(toDate))
                    , mu => mu.Terms(m => m.Field(f => f.ServiceCode).Terms(services))
                    , mu => mu.Terms(m => m.Field(f => f.TransType).Terms(services))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.ParentCode).Query(request.AgentCode))
                    , mu => mu.Match(m => m.Field(f => f.AccountAgentType).Query("5"))
                    , mu => mu.Terms(m => m.Field(f => f.Status).Terms(status))
                )
            ));

            query.Size(0)
                .Aggregations(cs => cs.MultiTerms("Commissions",
                    s => s.CollectMode(TermsAggregationCollectMode.BreadthFirst)
                        .Terms(t => t.Field(c => c.ParentCode.Suffix("keyword")),
                            t => t.Field(c => c.ParentName.Suffix("keyword")),
                            t => t.Field(c => c.CommissionStatus)
                        ).Size(1000)
                        .Aggregations(c => c
                            .Sum("Commission", i => i.Field(v => v.CommissionAmount ?? 0))
                            .Sum("Quantity", i => i.Field(v => v.Quantity))))
                );

            var scanResults = await _elasticClient.SearchAsync<ReportItemDetail>(query);
            var searchData = new List<ReportCommissionTotalDto>();
            var states = scanResults.Aggregations.MultiTerms("Commissions");
            foreach (var bucket in states.Buckets)
            {
                var tempAmount = bucket.GetValueOrDefault("Commission");
                var tempQuantity = bucket.GetValueOrDefault("Quantity");
                var _qty = tempQuantity.ConvertTo<ValueTeam>();
                var _amount = tempAmount.ConvertTo<ValueTeam>();
                var key = bucket.Key.ToList();
                searchData.Add(new ReportCommissionTotalDto
                {
                    AgentCode = key[0],
                    AgentName = key[1],
                    Commission = key[2],
                    Quantity = Convert.ToInt32(_qty.Value),
                    CommissionAmount = _amount.Value
                });
            }

            var total = searchData.Count();
            var group = (from x in searchData
                         group x by new { x.AgentCode, x.AgentName }
                into g
                         select new ReportCommissionTotalDto
                         {
                             AgentCode = g.Key.AgentCode,
                             AgentName = g.Key.AgentName,
                             Quantity = g.Sum(c => c.Quantity),
                             CommissionAmount = g.Sum(c => c.CommissionAmount),
                             Payment = g.Sum(c => c.Commission == "1" ? c.CommissionAmount : 0),
                             UnPayment = g.Sum(c => c.Commission == "0" ? c.CommissionAmount : 0)
                         }).OrderBy(c => c.AgentCode).ToList();

            var sumData = new ReportCommissionTotalDto
            {
                Quantity = group.Sum(c => c.Quantity),
                CommissionAmount = group.Sum(c => c.CommissionAmount),
                Payment = group.Sum(c => c.Payment),
                UnPayment = group.Sum(c => c.UnPayment)
            };

            var lstOrder = new List<ReportCommissionTotalDto>();

            var listView = group.Skip(request.Offset).Take(request.Limit);

            return new MessagePagedResponseBase
            {
                ResponseCode = "01",
                ResponseMessage = "Thành công",
                Total = total,
                SumData = sumData,
                Payload = listView
            };
        }
        catch (Exception e)
        {
            _logger.LogError($"ReportCommissionTotalGetList error: {e}");
            return new MessagePagedResponseBase
            {
                ResponseCode = "00"
            };
        }
    }

    public async Task<MessagePagedResponseBase> ReportCommissionAgentDetailGetList(
        ReportCommissionAgentDetailRequest request)
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

            var status = new string[4] { "1", "2", "0", "3" };
            var statusCommisstion = new string[2] { "0", "1" };
            var transCode = request.TransCode ?? string.Empty;
            var agentCode = request.AgentCode ?? string.Empty;

            if (request.Status == 1)
                status = new[] { "1" };
            else if (request.Status == 3)
                status = new[] { "3" };
            else if (request.Status == 2)
                status = new[] { "2", "0" };

            if (request.StatusPayment == 0)
                statusCommisstion = new[] { "0" };
            else if (request.StatusPayment == 1)
                statusCommisstion = new[] { "1" };

            var query = new SearchDescriptor<ReportItemDetail>();
            var fromDate = request.FromDate.Value.ToUniversalTime();
            var toDate = request.ToDate.Value.AddDays(1).ToUniversalTime();

            query.Index(ReportIndex.ReportItemDetailIndex).Query(q => q.Bool(b =>
                b.Must(mu => mu.Match(m => m.Field(f => f.AccountCode).Query(agentCode))
                    , mu => mu.DateRange(
                        r => r.Field(f => f.CreatedTime).GreaterThanOrEquals(fromDate).LessThan(toDate))
                    //, mu => mu.MatchPhrase(m => m.Field(f => f.AccountAgentType).Query(agentType))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.ParentCode).Query(request.LoginCode))
                    , mu => mu.MultiMatch(m =>
                        m.Fields(f =>
                                f.Field(c => c.TransCode).Field(c => c.RequestRef).Field(c => c.CommissionPaidCode))
                            .Query(transCode))
                    , mu => mu.Terms(m => m.Field(f => f.ServiceCode).Terms(services))
                    , mu => mu.Terms(m => m.Field(f => f.TransType).Terms(services))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.ServiceCode).Query(request.ServiceCode))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.CategoryCode).Query(request.CategoryCode))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.ProductCode).Query(request.ProductCode))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.AccountAgentType).Query("5"))
                    , mu => mu.Terms(m => m.Field(f => f.Status).Terms(status))
                    , mu => mu.Terms(m => m.Field(f => f.CommissionStatus).Terms(statusCommisstion))
                )
            ));


            var totalQuery = query;
            query.Aggregations(agg => agg
                .Sum("Amount", s => s.Field(p => p.Amount))
                .Sum("Commission", s => s.Field(p => p.CommissionAmount))
                .Sum("Quantity", s => s.Field(p => p.Quantity))
                .Sum("TotalPrice", s => s.Field(p => p.TotalPrice))
            ).Sort(c => c.Descending(i => i.CreatedTime));

            query.From(0).Size(1000).Scroll("5s");

            var searchData = new List<ReportItemDetail>();
            var scanResults = await _elasticClient.SearchAsync<ReportItemDetail>(query);
            var fAmount = scanResults.Aggregations.GetValueOrDefault("Amount");
            var fCommission = scanResults.Aggregations.GetValueOrDefault("Commission");
            var fQuantity = scanResults.Aggregations.GetValueOrDefault("Quantity");
            var fTotalPrice = scanResults.Aggregations.GetValueOrDefault("TotalPrice");
            var _fAmount = fAmount.ConvertTo<ValueTeam>();
            var _fCommission = fCommission.ConvertTo<ValueTeam>();
            var _fQuantity = fQuantity.ConvertTo<ValueTeam>();
            var _fPrice = fTotalPrice.ConvertTo<ValueTeam>();

            var sumData = new ReportCommissionAgentDetailDto
            {
                Amount = _fAmount.Value,
                CommissionAmount = _fCommission.Value,
                Quantity = Convert.ToInt32(_fQuantity.Value),
                TotalPrice = _fPrice.Value
            };
            ScrollReportDetail(scanResults, request.Offset + request.Limit, ref searchData);

            totalQuery.From(0).Size(10000).Scroll("5s");
            var total = int.Parse((await _elasticClient.SearchAsync<ReportItemDetail>(totalQuery)).Total.ToString());

            var listView = searchData.Skip(request.Offset).Take(request.Limit);

            var list = from g in listView.ToList()
                       select new ReportCommissionAgentDetailDto
                       {
                           CommissionAmount = g.CommissionAmount ?? 0,
                           CommissionCode = g.CommissionPaidCode,
                           Status = g.Status == ReportStatus.Success
                               ? 1
                               : g.Status == ReportStatus.TimeOut || g.Status == ReportStatus.Process
                                   ? 2
                                   : 3,
                           StatusName = g.Status == ReportStatus.Success
                               ? "Thành công"
                               : g.Status == ReportStatus.TimeOut || g.Status == ReportStatus.Process
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
                           TotalPrice = g.TotalPrice,
                           Quantity = g.Quantity,
                           StatusPayment = g.CommissionStatus ?? 0,
                           StatusPaymentName = g.CommissionStatus == 1 ? "Đã trả" : "Chưa trả"
                       };

            return new MessagePagedResponseBase
            {
                ResponseCode = "01",
                ResponseMessage = "Thành công",
                SumData = sumData,
                Total = total,
                Payload = list
            };
        }
        catch (Exception e)
        {
            _logger.LogError($"ReportCommissionDetailGetList error: {e}");
            return new MessagePagedResponseBase
            {
                ResponseCode = "00"
            };
        }
    }

    public async Task<MessagePagedResponseBase> ReportRevenueDashBoardDayGetList(
        ReportRevenueDashBoardDayRequest request)
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
            var toDate = request.ToDate.Value.Date.AddDays(1).ToUniversalTime();

            var loginCode = "";
            if (request.AccountType > 0)
                loginCode = request.LoginCode;

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
                b.Must(mu => mu.DateRange(r => r.Field(f => f.CreatedTime).GreaterThanOrEquals(fromDate).LessThanOrEquals(toDate))
                    , mu => mu.Terms(m => m.Field(f => f.ServiceCode).Terms(services))
                    , mu => mu.Terms(m => m.Field(f => f.TransType).Terms(services))
                    , mu => mu.Terms(m => m.Field(f => f.ServiceCode).Terms(serviceCode.ToArray()))
                    , mu => mu.Terms(m => m.Field(f => f.CategoryCode).Terms(categoryCode.ToArray()))
                    , mu => mu.Terms(m => m.Field(f => f.ProductCode).Terms(productCode.ToArray()))
                    , mu => mu.MultiMatch(m =>
                        m.Fields(f => f.Field(c => c.AccountCode).Field(c => c.PerformAccount)).Query(loginCode))
                    , mu => mu.Terms(m => m.Field(f => f.Status).Terms(status))
                )
            ));

            query.Size(0)
                .Aggregations(cs => cs.MultiTerms("Products",
                    s => s.CollectMode(TermsAggregationCollectMode.BreadthFirst)
                        .Terms(t => t.Field(c => c.TextDay.Suffix("keyword")),
                            t => t.Field(c => c.AccountCode.Suffix("keyword"))
                        ).Size(1000)
                        .Aggregations(c => c
                            .Sum("Amount", i => i.Field(v => v.Amount))
                            .Sum("Discount", i => i.Field(v => v.Discount))
                        ))
                );

            var scanResults = await _elasticClient.SearchAsync<ReportItemDetail>(query);
            var list = new List<ReportRevenueDashboardDay>();
            var states = scanResults.Aggregations.MultiTerms("Products");
            foreach (var bucket in states.Buckets)
            {
                var tempAmount = bucket.GetValueOrDefault("Amount");
                var tempDiscount = bucket.GetValueOrDefault("Discount");
                var _amount = tempAmount.ConvertTo<ValueTeam>();
                var _dis = tempDiscount.ConvertTo<ValueTeam>();
                var key = bucket.Key.ToList();
                list.Add(new ReportRevenueDashboardDay
                {
                    CreatedDay = new DateTime(Convert.ToInt32(key[0].Substring(0, 4)),
                        Convert.ToInt32(key[0].Substring(4, 2)), Convert.ToInt32(key[0].Substring(6, 2))),
                    DayText = key[0].Substring(6, 2) + "-" + key[0].Substring(4, 2) + "-" + key[0].Substring(0, 4),
                    Discount = Convert.ToDecimal(_dis != null ? _dis.Value : 0),
                    Revenue = Convert.ToDecimal(_amount != null ? _amount.Value : 0)
                });
            }

            var tempDate = request.FromDate.Value.Date;
            var maxDate = request.ToDate.Value.Date;
            while (tempDate <= maxDate)
            {
                if (list.Where(c => c.CreatedDay == tempDate).Count() == 0)
                    list.Add(new ReportRevenueDashboardDay
                    {
                        CreatedDay = tempDate,
                        DayText = tempDate.ToString("dd-MM-yyyy"),
                        Discount = 0,
                        Revenue = 0
                    });
                tempDate = tempDate.AddDays(1);
            }

            var total = list.Count();
            var sumtotal = new ReportRevenueDashboardDay
            {
                Revenue = list.Sum(c => c.Revenue),
                Discount = list.Sum(c => c.Discount)
            };

            var lst = list.OrderByDescending(c => c.CreatedDay).Skip(request.Offset).Take(request.Limit).ToList();

            return new MessagePagedResponseBase
            {
                ResponseCode = "01",
                ResponseMessage = "Thành công",
                Total = total,
                SumData = sumtotal,
                Payload = lst
            };
        }
        catch (Exception e)
        {
            _logger.LogError($"ReportRevenueDashBoardDayGetList error: {e}");
            return new MessagePagedResponseBase
            {
                ResponseCode = "00"
            };
        }
    }

    public async Task<MessagePagedResponseBase> ReportAgentGeneralDayGetDash(ReportAgentGeneralDashRequest request)
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

            query.Index(ReportIndex.ReportItemDetailIndex).Query(q => q.Bool(b =>
                b.Must(mu => mu.DateRange(r =>
                        r.Field(f => f.CreatedTime).GreaterThanOrEquals(fromDate).LessThanOrEquals(toDate))
                    , mu => mu.Terms(m => m.Field(f => f.ServiceCode).Terms(services))
                    , mu => mu.Terms(m => m.Field(f => f.TransType).Terms(services))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.ParentCode).Query(request.LoginCode))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.AccountCode).Query(request.AgentCode))
                    , mu => mu.Match(m => m.Field(f => f.AccountAgentType).Query("5"))
                    , mu => mu.Terms(m => m.Field(f => f.Status).Terms(status))
                )
            ));

            query.Size(0)
                .Aggregations(cs => cs.MultiTerms("Commissions",
                    s => s.CollectMode(TermsAggregationCollectMode.BreadthFirst)
                        .Terms(t => t.Field(c => c.TextDay.Suffix("keyword")),
                            t => t.Field(c => c.ParentCode.Suffix("keyword"))
                        ).Size(1000)
                        .Aggregations(c => c
                            .Sum("Amount", i => i.Field(v => v.Amount))
                            .Sum("Commission", i => i.Field(v => v.CommissionAmount ?? 0))))
                );

            var scanResults = await _elasticClient.SearchAsync<ReportItemDetail>(query);
            var list = new List<ReportRevenueCommistionDashDay>();
            var states = scanResults.Aggregations.MultiTerms("Commissions");
            foreach (var bucket in states.Buckets)
            {
                var tempCommission = bucket.GetValueOrDefault("Commission");
                var tempAmount = bucket.GetValueOrDefault("Amount");

                var _amount = tempAmount.ConvertTo<ValueTeam>();
                var _commission = tempCommission.ConvertTo<ValueTeam>();
                var key = bucket.Key.ToList();
                list.Add(new ReportRevenueCommistionDashDay
                {
                    CreatedDay = new DateTime(Convert.ToInt32(key[0].Substring(0, 4)),
                        Convert.ToInt32(key[0].Substring(4, 2)), Convert.ToInt32(key[0].Substring(6, 2))),
                    DayText = key[0].Substring(6, 2) + "-" + key[0].Substring(4, 2) + "-" + key[0].Substring(0, 4),
                    Commission = Convert.ToDecimal(_commission != null ? _commission.Value : 0),
                    Revenue = Convert.ToDecimal(_amount != null ? _amount.Value : 0)
                });
            }

            var tempDate = request.FromDate.Value.Date;

            while (tempDate < toDate)
            {
                if (list.Where(c => c.CreatedDay == tempDate).Count() == 0)
                    list.Add(new ReportRevenueCommistionDashDay
                    {
                        CreatedDay = tempDate,
                        DayText = tempDate.ToString("dd-MM-yyyy"),
                        Commission = 0,
                        Revenue = 0
                    });

                tempDate = tempDate.AddDays(1);
            }

            var total = list.Count();
            var sumtotal = new ReportRevenueCommistionDashDay
            {
                Revenue = list.Sum(c => c.Revenue),
                Commission = list.Sum(c => c.Commission)
            };

            var lst = list.OrderByDescending(c => c.CreatedDay).Skip(request.Offset).Take(request.Limit).ToList();


            return new MessagePagedResponseBase
            {
                ResponseCode = "01",
                ResponseMessage = "Thành công",
                Total = total,
                SumData = sumtotal,
                Payload = lst
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


    /// <summary>
    /// Báo cáo tổng hợp theo laoij tài khoản 5
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public async Task<MessagePagedResponseBase> ReportCommissionAgentTotalGetList(ReportCommissionAgentTotalRequest request)
    {
        try
        {
            var query = new SearchDescriptor<ReportAccountBalanceDay>();
            var fromDate = request.FromDate.Value.ToUniversalTime();
            var toDate = request.ToDate.Value.AddDays(1).ToUniversalTime();
            string agentCode = string.Empty;

            query.Index(ReportIndex.ReportAccountbalanceDayIndex).Query(q => q.Bool(b =>
                b.Must(mu => mu.DateRange(r => r.Field(f => f.CreatedDay).GreaterThanOrEquals(fromDate).LessThan(toDate))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.CurrencyCode).Query("VND"))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.AccountType).Query("CUSTOMER"))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.AccountCode).Query(agentCode))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.AgentType).Query("5"))
                )
            ));

            query.From(0).Size(10000).Scroll("5m");
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
                                     BeforeAmount = yc.BalanceAfter,
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
                                    AgentCode = x.AgentCode,
                                    AgentInfo = yc.AccountInfo,
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

            return new MessagePagedResponseBase
            {
                ResponseCode = "01",
                ResponseMessage = "Thành công",
                Total = total,
                SumData = sumTotal,
                Payload = lst,
            };


        }
        catch (Exception ex)
        {
            _logger.LogError($"ReportCommissionAgentTotalGetList error {ex}");
            return new MessagePagedResponseBase
            {
                ResponseCode = "00"
            };
        }
    }

    /// <summary>
    /// Phần lấy dữ liệu nạp tiền vào tài khoản chi tiết
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public async Task<MessagePagedResponseBase> ReportDepositDetailGetList(ReportDepositDetailRequest request)
    {
        try
        {
            var query = new SearchDescriptor<ReportItemDetail>();
            var fromDate = request.FromDate.Value.ToUniversalTime();
            var toDate = request.ToDate.Value.AddDays(1).ToUniversalTime();
            string agentCode = request.AgentCode ?? "";
            string transCode = request.TransCode ?? "";

            query.Index(ReportIndex.ReportItemDetailIndex).Query(q => q.Bool(b =>
                b.Must(mu => mu.DateRange(r => r.Field(f => f.CreatedTime).GreaterThanOrEquals(fromDate).LessThan(toDate))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.ServiceCode).Query("DEPOSIT"))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.TransType).Query("DEPOSIT"))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.AccountCode).Query(agentCode))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.TransCode).Query(transCode))
                    , mu => mu.MatchPhrase(m => m.Field(f => f.Status).Query("1"))
                )
            ));

            query.Aggregations(agg => agg
              .Sum("Price", s => s.Field(p => p.TotalPrice))
            ).Sort(c => c.Descending(i => i.CreatedTime));

            if (request.Limit + request.Offset <= 10000)
                query.From(0).Size(request.Limit + request.Offset).Scroll("5m");
            else query.From(0).Size(10000).Scroll("5m");

            var searchData = new List<ReportItemDetail>();
            var scanResults = await _elasticClient.SearchAsync<ReportItemDetail>(query);

            var fPrice = scanResults.Aggregations.GetValueOrDefault("Price");
            var price = fPrice.ConvertTo<ValueTeam>();
            var sumTotal = new ReportTransferDetailDto
            {
                Price = Convert.ToDecimal(price != null ? price.Value : 0),
            };

            ScrollReportDetail(scanResults, request.Offset + request.Limit, ref searchData);

            var list = (from x in searchData
                        select new ReportDepositDetailDto()
                        {
                            AgentType = x.AccountAgentType,
                            AgentTypeName = GetAgenTypeName(x.AccountAgentType),
                            AgentCode = x.AccountCode,
                            AgentInfo = !string.IsNullOrEmpty(x.AccountInfo)
                                ? x.AccountCode + " - " + x.AccountInfo : x.AccountCode,
                            CreatedTime = x.CreatedTime,
                            Price = Convert.ToDecimal(Math.Round(x.TotalPrice, 0)),
                            ServiceCode = x.ServiceCode,
                            ServiceName = x.ServiceName,
                            TransCode = x.TransCode,
                            Messager = x.TransNote,
                        }).ToList();

            var total = int.Parse((await _elasticClient.SearchAsync<ReportItemDetail>(query)).Total.ToString());

            return new MessagePagedResponseBase
            {
                ResponseCode = "01",
                ResponseMessage = "Thành công",
                SumData = sumTotal,
                Total = (int)total,
                Payload = list,
            };

        }
        catch (Exception ex)
        {
            _logger.LogError($"ReportDepositDetailGetList error {ex}");
            return new MessagePagedResponseBase
            {
                ResponseCode = "00"
            };
        }
    }
}