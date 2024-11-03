using System;
using System.Threading.Tasks;
using GMB.Topup.Balance.Domain.Entities;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoDbGenericRepository;

namespace GMB.Topup.Balance.Domain.Repositories;

public class BalanceMongoRepository : BaseMongoRepository, IBalanceMongoRepository
{
    //private Logger _logger = LogManager.GetLogger("BalanceMongoRepository");
    private readonly ILogger<BalanceMongoRepository> _logger;

    public BalanceMongoRepository(IMongoDbContext dbContext, ILogger<BalanceMongoRepository> logger) : base(dbContext)
    {
        _logger = logger;
    }

    // public async Task<decimal> GetAccountBalanceMaxDateAsync(string accountCode, DateTime date)
    // {
    //     var s = MongoDbContext.GetCollection<BalanceHistories>().Find(p =>
    //             (p.SrcAccountCode == accountCode
    //              || p.DesAccountCode == accountCode)
    //             && p.CreatedDate <= date.ToUniversalTime())
    //         .Sort(Builders<BalanceHistories>.Sort.Descending("CreatedDate"));

    //     var item = await s.FirstOrDefaultAsync();
    //     if (item != null)
    //     {
    //         if (item.SrcAccountCode == accountCode) return item.SrcAccountBalance;
    //         if (item.DesAccountCode == accountCode) return item.DesAccountBalance;
    //     }

    //     return 0;
    // }

    public void DropTestCollection<TDocument>()
    {
        MongoDbContext.DropCollection<TDocument>();
    }

    public void DropTestCollection<TDocument>(string partitionKey)
    {
        MongoDbContext.DropCollection<TDocument>(partitionKey);
    }


    //        public IQueryable<SaleDiscountDto> GetSaleDiscountQueryable()
    //        {
    //            var query = from saleDiscount in MongoDbContext.GetCollection<SaleDiscount>().AsQueryable()
    //
    //                        select new SaleDiscountDto
    //                        {
    //                            DiscountAmount = saleDiscount.DiscountAmount,
    //                            CreatedDate = saleDiscount.CreatedDate,
    //                            OrderCode = saleDiscount.OrderCode,
    //                            CustomerCode = saleDiscount.CustomerCode,
    //                            TenantId = saleDiscount.TenantId,
    //                            DiscountRate = saleDiscount.DiscountRate,
    //                            SaleAmount = saleDiscount.SaleAmount,
    //                            Status = saleDiscount.Status,
    //                            TransCode = saleDiscount.TransCode,
    //                            Id = saleDiscount.Id,
    //                            TransRef = saleDiscount.TransRef,
    //                            SaleDiscountAndFeeType = saleDiscount.SaleDiscountAndFeeType,
    //                            SaleMonth = saleDiscount.SaleMonth
    //                        };
    //
    //            //var s = await query.Where(p => p.WorkerAppName =="").Take(1000).ToListAsync();
    //
    //            return query; //.Where(p => p.WorkerAppName ==).Take(1000).ToList();
    //
    //        }
    //
    //        public IQueryable<SaleSummaryDto> GetSaleSummaryQueryable()
    //        {
    //            var query = from saleDiscount in MongoDbContext.GetCollection<SaleSummary>().AsQueryable()
    //
    //                select new SaleSummaryDto
    //                {
    //                    CustomerCode = saleDiscount.CustomerCode,
    //                    TenantId = saleDiscount.TenantId,
    //                    SaleIndirectAccumulated = saleDiscount.SaleIndirectAccumulated,
    //                    SaleMonth = saleDiscount.SaleMonth,
    //                    Status = saleDiscount.Status,
    //                    CreatedDate = saleDiscount.CreatedDate,
    //                    SaleDirectAccumulated = saleDiscount.SaleDirectAccumulated,
    //                    SaleSystemAccumulated = saleDiscount.SaleSystemAccumulated,
    //                    CustomerName = saleDiscount.CustomerName,
    //                    Id = saleDiscount.Id,
    //                };
    //
    //            //var s = await query.Where(p => p.WorkerAppName =="").Take(1000).ToListAsync();
    //
    //            return query; //.Where(p => p.WorkerAppName ==).Take(1000).ToList();
    //
    //        }
    //
    //        public IQueryable<SalePointRecordDto> GetSalePointRecordQueryable()
    //        {
    //            var query = from saleDiscount in MongoDbContext.GetCollection<SalePointRecord>().AsQueryable()
    //                select new SalePointRecordDto
    //                {
    //                    CreatedDate = saleDiscount.CreatedDate,
    //                    OrderCode = saleDiscount.OrderCode,
    //                    Status = saleDiscount.Status,
    //                    CustomerCode = saleDiscount.CustomerCode,
    //                    Id = saleDiscount.Id,
    //                    Type = saleDiscount.Type,
    //                    Date = saleDiscount.Date,
    //                    TenantId = saleDiscount.TenantId,
    //                    SalePoint = saleDiscount.SalePoint
    //                };
    //
    //            return query;
    //        }
    //
    //        public async Task<bool> SummaryUpdateAsync<T>(T item, bool isUpsert = false) where T : IDocument
    //        {
    //            try
    //            {
    //                var filter = Builders<T>.Filter.Where(p => p.Id == item.Id);
    //                await MongoDbContext.GetCollection<T>().ReplaceOneAsync(filter, item, new UpdateOptions() {IsUpsert = isUpsert });
    //                return true;
    //            }
    //            catch (Exception e)
    //            {
    //                _logger.LogError("Update item error: " + e.Message);
    //                return false;
    //            }
    //        }
}