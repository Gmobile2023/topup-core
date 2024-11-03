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
        #region B.=>Hàm dành cho đồng bộ và test

        public virtual async Task AddOrUpdateAsync<T>(string indexName, T model) where T : class
        {
            try
            {
                var exis = _elasticClient.DocumentExists(DocumentPath<T>.Id(new Id(model)), dd => dd.Index(indexName));

                if (exis.Exists)
                {
                    var result = await _elasticClient.UpdateAsync(DocumentPath<T>.Id(new Id(model)),
                        ss => ss.Index(indexName).Doc(model).RetryOnConflict(3));

                    if (result.ServerError == null) return;
                    _logger.LogDebug($"Update Document failed at index {indexName} :" + result.ServerError.Error.Reason);
                }
                else
                {
                    var result = await _elasticClient.IndexAsync(model, ss => ss.Index(indexName));
                    if (result.ServerError == null) return;
                    _logger.LogDebug($"Insert Docuemnt failed at index {indexName} :" + result.ServerError.Error.Reason);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(
                    $"{indexName} AddOrUpdateAsync Exception: {ex.Message}|{ex.StackTrace}|{ex.InnerException}");
            }
        }

        public async Task<MessagePagedResponseBase> GetReportItemDetail(ReportDetailRequest request)
        {
            try
            {
                var searchResponse = _elasticClient.Search<ReportItemDetail>(s => s.Index(ReportIndex.ReportItemDetailIndex)
                    .Query(q => q
                        .Match(m => m
                            .Field(f => f.TransCode)
                            .Query("")
                        )
                    ).From(0).Size(100)
                );


                var p = searchResponse.Documents.Take(100);
                var test = await _elasticClient.SearchAsync<ReportItemDetail>(x =>
                    x.Index(ReportIndex.ReportItemDetailIndex).From(0).Size(100).Query(y => y.MatchAll()));
                var lst = test.Documents.Take(1000).ToList();
                return null;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public async Task AddReportItemDetail(string indexName, ReportItemDetail item)
        {
            await AddOrUpdateAsync(indexName, item);
        }

        public async Task AddReportItemHistory(string indexName, ReportBalanceHistories item)
        {
            await AddOrUpdateAsync(indexName, item);
        }

        public async Task AddReportItemAccount(string indexName, ReportAccountDto item)
        {
            await AddOrUpdateAsync(indexName, item);
        }

        public async Task<bool> CheckPaidTransCode(string paidTransCode)
        {
            try
            {
                var searchResponse = _elasticClient.Search<ReportItemDetail>(s => s.Index(ReportIndex.ReportItemDetailIndex)
                    .Query(q => q
                        .MatchPhrase(m => m
                            .Field(f => f.PaidTransCode)
                            .Query(paidTransCode)
                        )
                    ).From(0).Size(1000)
                );

                var tranItem = searchResponse.Documents.Take(1);
                return tranItem.Count() > 0;
            }
            catch (Exception e)
            {
                return true;
            }
        }

        public async Task<List<string>> GetTransPaidList(DateTime date, int typeTransCode)
        {
            try
            {
                var query = new SearchDescriptor<ReportItemDetail>();
                var fromDate = date.ToUniversalTime();
                var toDate = date.AddDays(1).ToUniversalTime();

                query.Index(ReportIndex.ReportItemDetailIndex).Query(q => q.Bool(b =>
                    b.Must(mu =>
                        mu.DateRange(r => r.Field(f => f.CreatedTime).GreaterThanOrEquals(fromDate).LessThan(toDate))
                    )
                ));

                query.From(0)
                    .Size(10000)
                    .SearchType(Elasticsearch.Net.SearchType.QueryThenFetch)
                    .Scroll("4m");

                var searchData = new List<ReportItemDetail>();
                var scanResults = _elasticClient.Search<ReportItemDetail>(query);
                ScrollReportDetail(scanResults, int.MaxValue, ref searchData);

                var listCodes = new List<string>();
                if (typeTransCode == 1)
                    listCodes = searchData.Where(c => !string.IsNullOrEmpty(c.TransCode)).Select(c => c.TransCode).ToList();
                else if (typeTransCode == 2)
                    listCodes = searchData.Where(c => !string.IsNullOrEmpty(c.PaidTransCode)).Select(c => c.PaidTransCode)
                        .ToList();

                return listCodes.ToList();
            }
            catch (Exception e)
            {
                _logger.LogError($"GetTransPaidList error: {e}");
                return new List<string>();
            }
        }

        public async Task<List<string>> GetTransPaidListEmtry(DateTime date, int typeTransCode)
        {
            try
            {
                var query = new SearchDescriptor<ReportItemDetail>();
                var fromDate = date.ToUniversalTime();
                var toDate = date.AddDays(1).ToUniversalTime();

                query.Index(ReportIndex.ReportItemDetailIndex).Query(q => q.Bool(b =>
                    b.Must(mu =>
                        mu.DateRange(r => r.Field(f => f.CreatedTime).GreaterThanOrEquals(fromDate).LessThan(toDate))
                    )
                ));

                query.From(0).Size(10000).SearchType(Elasticsearch.Net.SearchType.QueryThenFetch).Scroll("4m");

                var searchData = new List<ReportItemDetail>();
                var scanResults = _elasticClient.Search<ReportItemDetail>(query);
                ScrollReportDetail(scanResults, int.MaxValue, ref searchData);

                var vlist = searchData.Where(c => (string.IsNullOrEmpty(c.ServiceName) && !string.IsNullOrEmpty(c.ServiceCode))
                   || (string.IsNullOrEmpty(c.ProductName) && !string.IsNullOrEmpty(c.ProductCode))
                   || (string.IsNullOrEmpty(c.CategoryName) && !string.IsNullOrEmpty(c.CategoryCode))
                   || (string.IsNullOrEmpty(c.VenderName) && string.IsNullOrEmpty(c.VenderCode)));


                var listCodes = new List<string>();
                if (typeTransCode == 1)
                    listCodes = vlist.Where(c => !string.IsNullOrEmpty(c.TransCode)).Select(c => c.TransCode).ToList();
                else if (typeTransCode == 2)
                    listCodes = vlist.Where(c => !string.IsNullOrEmpty(c.PaidTransCode)).Select(c => c.PaidTransCode)
                        .ToList();

                return listCodes.ToList();
            }
            catch (Exception e)
            {
                _logger.LogError($"GetTransPaidList error: {e}");
                return new List<string>();
            }
        }

        public async Task<List<string>> GetAccountIndexList(string accountCode)
        {
            try
            {
                var searchResponse = _elasticClient.Search<ReportAccountDto>(s => s.Index(ReportIndex.ReportaccountdtosIndex)
                    .Query(q => q.Match(m => m
                            .Field(f => f.AccountCode)
                            .Query(accountCode)
                        )
                    ).From(0).Size(10000)
                );

                var lst = searchResponse.Documents.ToList();
                return lst.Select(c => c.AccountCode).ToList();
            }
            catch (Exception e)
            {
                _logger.LogError($"GetAccountIndexList error: {e}");
                return new List<string>();
            }
        }

        public async Task<List<ReportAccountBalanceDay>> GetAccountBalanceDayList(DateTime date, string currencyCode, string accountCode)
        {
            try
            {
                var query = new SearchDescriptor<ReportAccountBalanceDay>();
                var fromDate = date.ToUniversalTime();
                var toDate = date.AddDays(1).ToUniversalTime();

                query.Index(ReportIndex.ReportAccountbalanceDayIndex).Query(q => q.Bool(b =>
                    b.Must(mu =>
                        mu.DateRange(r => r.Field(f => f.CreatedDay).GreaterThanOrEquals(fromDate).LessThan(toDate))
                      , mu => mu.MatchPhrase(m => m.Field(f => f.CurrencyCode).Query(currencyCode))
                    )
                ));

                query.From(0).Size(10000).SearchType(Elasticsearch.Net.SearchType.QueryThenFetch).Scroll("5m");
                var searchData = new List<ReportAccountBalanceDay>();
                var scanResults = _elasticClient.Search<ReportAccountBalanceDay>(query);
                ScrollAccountBalanceDay(scanResults, int.MaxValue, ref searchData);
                return searchData;
            }
            catch (Exception e)
            {
                _logger.LogError($"GetAccountBalanceDayList error: {e}");
                return new List<ReportAccountBalanceDay>();
            }
        }

        public async Task<List<string>> GetStaffDetailList(DateTime date, string transCode)
        {
            try
            {
                var query = new SearchDescriptor<ReportStaffDetail>();
                var fromDate = date.ToUniversalTime();
                var toDate = date.AddDays(1).ToUniversalTime();

                query.Index(ReportIndex.ReportStaffdetailsIndex).Query(q => q.Bool(b =>
                    b.Must(mu =>
                        mu.DateRange(r => r.Field(f => f.CreatedTime).GreaterThanOrEquals(fromDate).LessThan(toDate))
                      , mu => mu.MatchPhrase(m => m.Field(f => f.TransCode).Query(transCode))
                    )
                ));

                query.From(0)
                    .Size(10000)
                    .SearchType(Elasticsearch.Net.SearchType.QueryThenFetch)
                    .Scroll("4m");

                var searchData = new List<ReportStaffDetail>();
                var scanResults = _elasticClient.Search<ReportStaffDetail>(query);
                ScrollStaffDetail(scanResults, int.MaxValue, ref searchData);

                return searchData.Select(c => c.TransCode).ToList();
            }
            catch (Exception e)
            {
                _logger.LogError($"GetStaffDetailList error: {e}");
                return new List<string>();
            }
        }

        public async Task<List<ReportCardStockByDate>> GetCardStockByDateList(DateTime date)
        {
            try
            {
                var query = new SearchDescriptor<ReportCardStockByDate>();
                var fromDate = date.ToUniversalTime();
                var toDate = date.AddDays(1).ToUniversalTime();

                query.Index(ReportIndex.ReportCardstockbydatesIndex).Query(q => q.Bool(b =>
                    b.Must(mu =>
                        mu.DateRange(r => r.Field(f => f.CreatedDate).GreaterThanOrEquals(fromDate).LessThan(toDate))
                    )
                ));

                query.From(0)
                    .Size(10000)
                    .SearchType(Elasticsearch.Net.SearchType.QueryThenFetch)
                    .Scroll("4m");

                var searchData = new List<ReportCardStockByDate>();
                var scanResults = _elasticClient.Search<ReportCardStockByDate>(query);
                ScrollCardStockByDate(scanResults, int.MaxValue, ref searchData);
                return searchData;
            }
            catch (Exception e)
            {
                _logger.LogError($"GetCardStockByDateList error: {e}");
                return new List<ReportCardStockByDate>();
            }
        }

        public async Task<List<ReportCardStockProviderByDate>> GetCardStockProviderByDateList(DateTime date)
        {
            try
            {
                var query = new SearchDescriptor<ReportCardStockProviderByDate>();
                var fromDate = date.ToUniversalTime();
                var toDate = date.AddDays(1).ToUniversalTime();

                query.Index(ReportIndex.ReportCardstockproviderbydates).Query(q => q.Bool(b =>
                    b.Must(mu =>
                        mu.DateRange(r => r.Field(f => f.CreatedDate).GreaterThanOrEquals(fromDate).LessThan(toDate))
                    )
                ));

                query.From(0)
                    .Size(10000)
                    .SearchType(Elasticsearch.Net.SearchType.QueryThenFetch)
                    .Scroll("5m");

                var searchData = new List<ReportCardStockProviderByDate>();
                var scanResults = _elasticClient.Search<ReportCardStockProviderByDate>(query);
                ScrollCardStockProviderByDate(scanResults, int.MaxValue, ref searchData);
                return searchData;
            }
            catch (Exception e)
            {
                _logger.LogError($"GetCardStockProviderByDateList error: {e}");
                return new List<ReportCardStockProviderByDate>();
            }
        }

        public async Task<List<ReportCardStockDayDto>> CardStockDateAuto(CardStockAutoRequest request)
        {
            try
            {
                var query = new SearchDescriptor<ReportCardStockByDate>();
                var dateNow = DateTime.Now.Date.ToUniversalTime();

                query.Index(ReportIndex.ReportCardstockbydatesIndex).Query(q => q.Bool(b =>
                    b.Must(mu => mu.DateRange(r => r.Field(f => f.CreatedDate).LessThanOrEquals(dateNow)))
                ));
                query.From(0).Size(10000).Scroll("5m");

                var searchData = new List<ReportCardStockByDate>();
                var scanResults = _elasticClient.Search<ReportCardStockByDate>(query);
                ScrollCardStockByDate(scanResults, int.MaxValue, ref searchData);
                var listSale = await new ConvertDataRepository().FillterStock(searchData.Where(c => c.StockCode == "STOCK_SALE").ToList(), request.FromDate, request.ToDate);
                var listTemp = await new ConvertDataRepository().FillterStock(searchData.Where(c => c.StockCode == "STOCK_TEMP").ToList(), request.FromDate, request.ToDate);
                var productCodes = searchData.Select(c => (c.ProductCode ?? string.Empty).ToLower()).Distinct().ToList();
                var lstProduct = await GetProductByArrays(productCodes);
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
                _logger.LogInformation($"CardStockDateAuto error {ex}");
                return new List<ReportCardStockDayDto>();
            }
        }

        public async Task<List<ReportCardStockProviderByDate>> CardStockProviderDateAuto(CardStockAutoRequest request)
        {
            try
            {
                var query = new SearchDescriptor<ReportCardStockProviderByDate>();
                var dateNow = DateTime.Now.ToUniversalTime();

                query.Index(ReportIndex.ReportCardstockproviderbydates).Query(q => q.Bool(b =>
                    b.Must(mu => mu.DateRange(r => r.Field(f => f.CreatedDate).LessThanOrEquals(dateNow)))
                ));
                query.From(0).Size(10000).SearchType(Elasticsearch.Net.SearchType.QueryThenFetch).Scroll("5m");

                var searchData = new List<ReportCardStockProviderByDate>();
                var scanResults = _elasticClient.Search<ReportCardStockProviderByDate>(query);
                ScrollCardStockProviderByDate(scanResults, int.MaxValue, ref searchData);
                var mView = await new ConvertDataRepository().FillterStockProvider(searchData, request.FromDate, request.ToDate);
                return mView.ToList();
            }
            catch (Exception ex)
            {
                //_logger.Log($"CardStockProviderDateAuto error {ex}");
                return new List<ReportCardStockProviderByDate>();
            }
        }

        public async Task<List<string>> GetHistoryTempList(DateTime date)
        {
            try
            {
                var query = new SearchDescriptor<ReportBalanceHistories>();
                var fromDate = date.ToUniversalTime();
                var toDate = date.AddDays(1).ToUniversalTime();

                query.Index(ReportIndex.ReportBalanceHistoriesIndex).Query(q => q.Bool(b =>
                    b.Must(mu =>
                        mu.DateRange(r => r.Field(f => f.CreatedDate).GreaterThanOrEquals(fromDate).LessThan(toDate))
                    )
                ));

                query.From(0)
                    .Size(10000)
                    .SearchType(Elasticsearch.Net.SearchType.QueryThenFetch)
                    .Scroll("4m");

                var searchData = new List<ReportBalanceHistories>();
                var scanResults = _elasticClient.Search<ReportBalanceHistories>(query);
                ScrollBalanceHistories(scanResults, int.MaxValue, ref searchData);
                return searchData.Select(c => c.TransCode).ToList();
            }
            catch (Exception e)
            {
                _logger.LogError($"GetHistoryTempList error: {e}");
                return new List<string>();
            }
        }

        private decimal OrderValue(string serviceCode, string productCode)
        {
            try
            {
                if (serviceCode == ReportServiceCode.TOPUP
                    || serviceCode == ReportServiceCode.TOPUP_DATA
                    || serviceCode == ReportServiceCode.PIN_CODE
                    || serviceCode == ReportServiceCode.PIN_GAME
                    || serviceCode == ReportServiceCode.PIN_GAME)
                {
                    var p = productCode.Split('_');
                    return Convert.ToDecimal(p[p.Length - 1]);
                }

                return 0;
            }
            catch (Exception ex)
            {
                return 0;
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

        private List<DateTime> getArrayDate(DateTime fromDate, DateTime toDate)
        {
            var ls = new List<DateTime>();
            while (fromDate <= toDate)
            {
                ls.Add(fromDate);
                fromDate = fromDate.AddDays(1);
            }

            return ls;
        }

        public async Task AddReportAccountBalanceDay(string indexName, ReportAccountBalanceDay item)
        {
            await AddOrUpdateAsync(indexName, item);
        }

        public async Task AddReportStaffDetail(string indexName, ReportStaffDetail item)
        {
            await AddOrUpdateAsync(indexName, item);
        }

        public async Task AddReportCardStockByDate(string indexName, ReportCardStockByDate item)
        {
            await AddOrUpdateAsync(indexName, item);
        }

        public async Task AddReportCardStockProviderByDate(string indexName, ReportCardStockProviderByDate item)
        {
            await AddOrUpdateAsync(indexName, item);
        }

        private async Task<List<ReportAccountDto>> GetAccountByArrays(List<string> arrays)
        {
            try
            {
                var arraysConvert = arrays.Select(c => c.ToLower()).ToList();
                var query = new SearchDescriptor<ReportAccountDto>();
                query.Index(ReportIndex.ReportaccountdtosIndex).Query(q => q.Bool(b =>
                b.Must(mu => mu.Terms(m => m.Field(f => f.AccountCode).Terms(arraysConvert.ToArray())))));
                query.From(0).Size(10000).Scroll("3m");
                var searchData = new List<ReportAccountDto>();
                var scanResults = await _elasticClient.SearchAsync<ReportAccountDto>(query);
                int limit = int.MaxValue;
                ScrollAccountInfo(scanResults, limit, ref searchData);
                return searchData;
            }
            catch (Exception e)
            {
                _logger.LogError($"GetAccountByArrays error: {e}");
                return new List<ReportAccountDto>();
            }
        }

        private async Task<List<ReportProductDto>> GetProductByArrays(List<string> arrays)
        {
            try
            {
                //Expression<Func<ReportProductDto, bool>> queryProduct = p => arrays.Contains(p.ProductCode);
                //var lstProduct = await _reportMongoRepository.GetAllAsync(queryProduct);
                //return lstProduct;
                #region 

                var query = new SearchDescriptor<ReportProductDto>();
                query.Index(ReportIndex.ReportproductdtosIndex).Query(q => q.Bool(b =>
                b.Must(mu => mu.Terms(m => m.Field(f => f.ProductCode).Terms(arrays.ToArray())))));
                query.From(0).Size(10000).Scroll("3m");
                var searchData = new List<ReportProductDto>();
                var scanResults = await _elasticClient.SearchAsync<ReportProductDto>(query);
                int limit = int.MaxValue;
                if (scanResults.Documents.Count >= 10000)
                {
                    searchData.AddRange(scanResults.Documents);
                    if (searchData.Count < limit)
                        while (scanResults.Documents.Any())
                        {
                            scanResults = _elasticClient.Scroll<ReportProductDto>("3m", scanResults.ScrollId);
                            var items = scanResults.Documents;
                            searchData.AddRange(items);
                            if (searchData.Count >= limit)
                                break;
                        }
                }
                else
                {
                    var items = scanResults.Documents;
                    searchData.AddRange(items);
                }

                return searchData;

                #endregion
            }
            catch (Exception e)
            {
                _logger.LogError($"GetProductByArrays error: {e}");
                return new List<ReportProductDto>();
            }
        }

        private void ScrollAccountInfo(ISearchResponse<ReportAccountDto> scanResults, int limit, ref List<ReportAccountDto> searchData)
        {
            if (scanResults.Documents.Count >= 10000)
            {
                searchData.AddRange(scanResults.Documents);
                if (searchData.Count < limit)
                    while (scanResults.Documents.Any())
                    {
                        scanResults = _elasticClient.Scroll<ReportAccountDto>("5m", scanResults.ScrollId);
                        var items = scanResults.Documents;
                        searchData.AddRange(items);
                        if (searchData.Count >= limit)
                            break;
                    }
            }
            else
            {
                var items = scanResults.Documents;
                searchData.AddRange(items);
            }
        }
        private void ScrollReportDetail(ISearchResponse<ReportItemDetail> scanResults, int limit, ref List<ReportItemDetail> searchData)
        {
            if (scanResults.Documents.Count >= 10000)
            {
                searchData.AddRange(scanResults.Documents);
                if (searchData.Count < limit)
                    while (scanResults.Documents.Any())
                    {
                        scanResults = _elasticClient.Scroll<ReportItemDetail>("5m", scanResults.ScrollId);
                        var items = scanResults.Documents;
                        searchData.AddRange(items);
                        if (searchData.Count >= limit)
                            break;
                    }
            }
            else
            {
                var items = scanResults.Documents;
                searchData.AddRange(items);
            }
        }

        private void ScrollAccountBalanceDay(ISearchResponse<ReportAccountBalanceDay> scanResults, int limit, ref List<ReportAccountBalanceDay> searchData)
        {
            if (scanResults.Documents.Count >= 10000)
            {
                searchData.AddRange(scanResults.Documents);
                if (searchData.Count < limit)
                    while (scanResults.Documents.Any())
                    {
                        scanResults = _elasticClient.Scroll<ReportAccountBalanceDay>("3m", scanResults.ScrollId);
                        var items = scanResults.Documents;
                        searchData.AddRange(items);
                        if (searchData.Count >= limit)
                            break;
                    }
            }
            else
            {
                var items = scanResults.Documents;
                searchData.AddRange(items);
            }
        }

        private void ScrollBalanceHistories(ISearchResponse<ReportBalanceHistories> scanResults, int limit, ref List<ReportBalanceHistories> searchData)
        {
            if (scanResults.Documents.Count >= 10000)
            {
                searchData.AddRange(scanResults.Documents);
                if (searchData.Count < limit)
                    while (scanResults.Documents.Any())
                    {
                        scanResults = _elasticClient.Scroll<ReportBalanceHistories>("5m", scanResults.ScrollId);
                        var items = scanResults.Documents;
                        searchData.AddRange(items);
                        if (searchData.Count >= limit)
                            break;
                    }
            }
            else
            {
                var items = scanResults.Documents;
                searchData.AddRange(items);
            }
        }

        private void ScrollStaffDetail(ISearchResponse<ReportStaffDetail> scanResults, int limit, ref List<ReportStaffDetail> searchData)
        {
            if (scanResults.Documents.Count >= 10000)
            {
                searchData.AddRange(scanResults.Documents);
                if (searchData.Count < limit)
                    while (scanResults.Documents.Any())
                    {
                        scanResults = _elasticClient.Scroll<ReportStaffDetail>("5m", scanResults.ScrollId);
                        var items = scanResults.Documents;
                        searchData.AddRange(items);
                        if (searchData.Count >= limit)
                            break;
                    }
            }
            else
            {
                var items = scanResults.Documents;
                searchData.AddRange(items);
            }
        }

        private void ScrollCardStockByDate(ISearchResponse<ReportCardStockByDate> scanResults, int limit, ref List<ReportCardStockByDate> searchData)
        {
            if (scanResults.Documents.Count >= 10000)
            {
                searchData.AddRange(scanResults.Documents);
                if (searchData.Count < limit)
                    while (scanResults.Documents.Any())
                    {
                        scanResults = _elasticClient.Scroll<ReportCardStockByDate>("5m", scanResults.ScrollId);
                        var items = scanResults.Documents;
                        searchData.AddRange(items);
                        if (searchData.Count >= limit)
                            break;
                    }
            }
            else
            {
                var items = scanResults.Documents;
                searchData.AddRange(items);
            }
        }

        private void ScrollCardStockProviderByDate(ISearchResponse<ReportCardStockProviderByDate> scanResults, int limit, ref List<ReportCardStockProviderByDate> searchData)
        {
            if (scanResults.Documents.Count >= 10000)
            {
                searchData.AddRange(scanResults.Documents);
                if (searchData.Count < limit)
                    while (scanResults.Documents.Any())
                    {
                        scanResults = _elasticClient.Scroll<ReportCardStockProviderByDate>("5m", scanResults.ScrollId);
                        var items = scanResults.Documents;
                        searchData.AddRange(items);
                        if (searchData.Count >= limit)
                            break;
                    }
            }
            else
            {
                var items = scanResults.Documents;
                searchData.AddRange(items);
            }
        }


        private void ScrollCardStockHistoriesByDate(ISearchResponse<ReportCardStockHistories> scanResults, int limit, ref List<ReportCardStockHistories> searchData)
        {
            if (scanResults.Documents.Count >= 10000)
            {
                searchData.AddRange(scanResults.Documents);
                if (searchData.Count < limit)
                    while (scanResults.Documents.Any())
                    {
                        scanResults = _elasticClient.Scroll<ReportCardStockHistories>("5m", scanResults.ScrollId);
                        var items = scanResults.Documents;
                        searchData.AddRange(items);
                        if (searchData.Count >= limit)
                            break;
                    }
            }
            else
            {
                var items = scanResults.Documents;
                searchData.AddRange(items);
            }
        }

        private string getServiceNameDetail(string x, string accountCode, string desAccountCode, string srcAccountCode)
        {
            return x == "REFUND" ? "Hoàn tiền"
                       : x == "Payment" ? "Thanh toán"
                       : x == ReportServiceCode.TOPUP ? "Nạp tiền điện thoại"
                       : x == ReportServiceCode.TOPUP_DATA ? "Nạp data"
                       : x == ReportServiceCode.PAY_BILL ? "Thanh toán hóa đơn"
                       : x == ReportServiceCode.PIN_DATA ? "Mua thẻ Data"
                       : x == ReportServiceCode.PIN_GAME ? "Mua thẻ Game"
                       : x == ReportServiceCode.PIN_CODE ? "Mua mã thẻ"
                       : x == "CORRECT_UP" ? "Điều chỉnh tăng"
                       : x == "CORRECT_DOWN" ? "Điều chỉnh giảm"
                       : x == ReportServiceCode.PAYBATCH ? "Trả thưởng"
                       : x == "PAYCOMMISSION" ? "Hoa hồng"
                       : x == "TRANSFER" && accountCode == desAccountCode ? "Nhận tiền đại lý"
                       : x == "TRANSFER" && accountCode == srcAccountCode ? "Chuyển tiền đại lý"
                       : x == ReportServiceCode.DEPOSIT ? "Nạp tiền" : "";
        }

        private void ScrollReportTopupRequestLog(ISearchResponse<TopupRequestLog> scanResults, int limit, ref List<TopupRequestLog> searchData)
        {
            if (scanResults.Documents.Count >= 10000)
            {
                searchData.AddRange(scanResults.Documents);
                if (searchData.Count < limit)
                    while (scanResults.Documents.Any())
                    {
                        scanResults = _elasticClient.Scroll<TopupRequestLog>("5m", scanResults.ScrollId);
                        var items = scanResults.Documents;
                        searchData.AddRange(items);
                        if (searchData.Count >= limit)
                            break;
                    }
            }
            else
            {
                var items = scanResults.Documents;
                searchData.AddRange(items);
            }
        }

        #endregion
    }
}