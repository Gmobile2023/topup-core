using MongoDB.Driver;
using MongoDbGenericRepository;
using System;
using System.Linq;
using System.Threading.Tasks;
using GMB.Topup.Report.Domain.Entities;
using GMB.Topup.Shared;
using Microsoft.Extensions.Logging;

namespace GMB.Topup.Report.Domain.Repositories
{
    public class ReportMongoRepository : BaseMongoRepository, IReportMongoRepository
    {
        private readonly ILogger<ReportMongoRepository> _logger;

        public ReportMongoRepository(IMongoDbContext dbContext, ILogger<ReportMongoRepository> logger) : base(dbContext)
        {
            _logger = logger;
        }

        public IQueryable<TDocument> GetQueryable<TDocument>()
        {
            return MongoDbContext.GetCollection<TDocument>().AsQueryable();
        }

        public async Task<ReportAccountBalanceDay> GetReportAccountBalanceDayAsync(string accountCode,
            string currencyCode, DateTime date)
        {

            string textDate = accountCode + "_" + date.Date.ToString("yyyyMMdd");
            var fs = await GetReportAccountBalanceDayOpenAsync(accountCode, currencyCode, textDate);
            if (fs != null)
                return fs;
            else
            {
                var fromDate = date.Date.ToUniversalTime();
                var toDate = date.Date.AddDays(1).AddSeconds(-1).ToUniversalTime();
                var s = MongoDbContext.GetCollection<ReportAccountBalanceDay>().Find(p =>
                        p.AccountCode == accountCode
                        && p.CurrencyCode == currencyCode
                        && p.CreatedDay >= fromDate
                        && p.CreatedDay <= toDate)
                    .Sort(Builders<ReportAccountBalanceDay>.Sort.Descending("CreatedDay"));
                var item = await s.FirstOrDefaultAsync();

                return item;
            }
        }

        public async Task<ReportAccountBalanceDay> GetReportAccountBalanceDayOpenAsync(string accountCode,
            string currencyCode, string date)
        {
            var s = GetOneAsync<ReportAccountBalanceDay>(p =>
                    p.AccountCode == accountCode
                    && p.CurrencyCode == currencyCode
                    && p.TextDay == date);
            var item = await s;

            return item;
        }

        public async Task<ReportBalanceHistories> GetReportBalanceHistoriesByTransCode(string transCode)
        {
            return await GetOneAsync<ReportBalanceHistories>(p => p.TransCode == transCode);
        }

        public async Task<ReportAccountBalanceDay> GetReportAccountBalanceDayBy(string accountCode, DateTime date)
        {
            var fromDate = date.Date.ToUniversalTime();
            var toDate = date.Date.AddDays(1).AddSeconds(-1).ToUniversalTime();
            // var item = MongoDbContext.GetCollection<ReportAccountBalanceDay>().Find(p =>
            //     p.AccountCode == accountCode
            //     && p.CreatedDay >= fromDate
            //     && p.CreatedDay <= toDate).FirstOrDefault();
            var item = await GetOneAsync<ReportAccountBalanceDay>(p =>
                p.AccountCode == accountCode
                && p.CreatedDay >= fromDate
                && p.CreatedDay <= toDate);
            return item;
        }

        public async Task<ReportCardStockByDate> GetReportCardStockByDate(string stockType, string stockcode,
            string productcode,
            DateTime date)
        {
            _logger.LogInformation($"GetReportCardStockByDate: {stockType}-{stockcode}-{productcode}-{date.ToShortDateString()}");
            var s = MongoDbContext.GetCollection<ReportCardStockByDate>().Find(p =>
                    p.StockType == stockType
                    && p.ProductCode == productcode
                    && p.StockCode == stockcode
                    && p.ShortDate == date.ToShortDateString())
                .Sort(Builders<ReportCardStockByDate>.Sort.Descending(x => x.CreatedDate));
            return await s.FirstOrDefaultAsync();
        }

        public async Task<ReportCardStockProviderByDate> GetReportCardStockProviderByDate(string providerCode,
            string stockType, string stockcode, string productcode)
        {
            _logger.LogInformation($"GetReportCardStockProviderByDate: {providerCode}-{stockType}-{stockcode}-{productcode}");
            var s = MongoDbContext.GetCollection<ReportCardStockProviderByDate>().Find(p =>
                    p.StockType == stockType
                    && p.ProductCode == productcode
                    && p.StockCode == stockcode
                    && p.ProviderCode == providerCode)
                .Sort(Builders<ReportCardStockProviderByDate>.Sort.Descending(x => x.CreatedDate));
            return await s.FirstOrDefaultAsync();
        }

        public async Task<ReportItemDetail> GetReportItemByPaidTransCode(string transCode)
        {
            return await GetOneAsync<ReportItemDetail>(p => p.PaidTransCode == transCode);
        }
        public async Task<ReportItemDetail> GetReportItemByTransCode(string transCode)
        {
            int retry = 0;
            while (retry <= 3)
            {
                try
                {
                    return await GetOneAsync<ReportItemDetail>(p => p.TransCode == transCode);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"{transCode} GetReportItemByTransCode Exception: {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
                    retry = retry + 1;
                }
            }

            return null;
        }

        public async Task<ReportItemDetail> GetReportItemByTransSouce(string transCode)
        {
            return await GetOneAsync<ReportItemDetail>(p => p.TransTransSouce == transCode);
        }

        public async Task<ReportAccountDto> GetReportAccountByAccountCode(string accountCode)
        {
            int retry = 0;
            while (retry <= 3)
            {
                try
                {
                    return await GetOneAsync<ReportAccountDto>(p => p.AccountCode == accountCode);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"{accountCode} GetReportAccountByAccountCode Exception: {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
                    retry = retry + 1;
                }
            }
            return null;
        }

        public async Task<ReportAccountDto> GetReportAccountByUserId(int userId)
        {
            return await GetOneAsync<ReportAccountDto>(p => p.UserId == userId);
        }

        public async Task<ReportProductDto> GetReportProductByProductCode(string productCode)
        {
            return await GetOneAsync<ReportProductDto>(p => p.ProductCode == productCode);
        }

        public async Task<ReportProviderDto> GetReportProviderByProviderCode(string providerCode)
        {
            return await GetOneAsync<ReportProviderDto>(p => p.ProviderCode == providerCode);
        }

        public async Task<ReportServiceDto> GetReportServiceByServiceCode(string serviceCode)
        {
            return await GetOneAsync<ReportServiceDto>(p => p.ServiceCode == serviceCode);
        }

        public async Task<ReportVenderDto> GetReportVenderByVenderCode(string venderCode)
        {
            return await GetOneAsync<ReportVenderDto>(p => p.VenderCode == venderCode);
        }

        public async Task<ReportServiceDto> GetReportServiceByServiceId(int serviceId)
        {
            return await GetOneAsync<ReportServiceDto>(p => p.ServiceId == serviceId);
        }
        public async Task UpdateReportStatus(string transCode, ReportStatus status)
        {
            try
            {
                var s = await GetOneAsync<ReportItemDetail>(p => p.TransCode == transCode);
                if (s != null)
                {
                    s.Status = status;
                    await UpdateOneAsync(s);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"{transCode}|{status} UpdateReportStatus Exception: {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
            }
        }

        public async Task UpdateProduct(ReportProductDto product)
        {
            try
            {
                var _product = await GetReportProductByProductCode(product.ProductCode);
                await (_product != null ? UpdateOneAsync(product) : AddOneAsync(product));
            }
            catch (Exception ex)
            {
                _logger.LogError($"{product.ProductCode} UpdateProduct Exception: {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
            }
        }

        public async Task UpdateAccount(ReportAccountDto account)
        {
            try
            {
                var _acccount = await GetReportAccountByAccountCode(account.AccountCode);
                await (_acccount != null ? UpdateOneAsync(account) : AddOneAsync(account));

            }
            catch (Exception ex)
            {
                _logger.LogError($"{account.AccountCode} UpdateAccount Exception: {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
            }
        }

        public async Task UpdateService(ReportServiceDto service)
        {
            try
            {
                var _service = await GetReportServiceByServiceCode(service.ServiceCode);
                await (_service != null ? UpdateOneAsync(service) : AddOneAsync(service));

            }
            catch (Exception ex)
            {
                _logger.LogError($"{service.ServiceCode} UpdateService Exception: {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
            }
        }

        public async Task UpdateVender(ReportVenderDto venderDto)
        {
            try
            {
                var _vender = await GetReportServiceByServiceCode(venderDto.VenderCode);
                await (_vender != null ? UpdateOneAsync(venderDto) : AddOneAsync(venderDto));

            }
            catch (Exception ex)
            {
                _logger.LogError($"{venderDto.VenderCode} UpdateVender Exception: {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
            }
        }

        public async Task UpdateProvider(ReportProviderDto provider)
        {
            try
            {
                var _provider = await GetReportProviderByProviderCode(provider.ProviderCode);
                await (_provider != null ? UpdateOneAsync(_provider) : AddOneAsync(provider));
            }
            catch (Exception ex)
            {
                _logger.LogError($"{provider.ProviderCode} UpdateProvider Exception: {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
            }
        }

        public async Task InsertWaringInfo(ReportItemWarning info)
        {
            try
            {
                info.Status = 0;
                info.CreatedDate = DateTime.Now;
                info.TextDay = DateTime.Now.ToString("yyyyMMdd");
                await this.AddOneAsync(info);
            }
            catch (Exception ex)
            {
                _logger.LogError($"{info.TransCode}|{info.TransType} InsertWaringInfo Exception: {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
            }
        }

        public async Task<bool> UpdateWaringInfo(ReportItemWarning info)
        {
            try
            {
                await UpdateOneAsync(info);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"{info.TransCode}|{info.TransType} UpdateWaringInfo Exception: {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
                return false;
            }
        }

        public async Task InsertFileFptInfo(ReportFileFpt info)
        {
            try
            {
                await this.AddOneAsync(info);
            }
            catch (Exception ex)
            {

            }
        }
    }
}
