using System;
using System.Threading.Tasks;
using HLS.Paygate.Report.Domain.Entities;
using MongoDB.Driver;
using MongoDbGenericRepository;

namespace HLS.Paygate.Report.Domain.Repositories
{
    public class SimReportMongoRepository:BaseMongoRepository,ISimReportMongoRepository
    {
        public SimReportMongoRepository(IMongoDbContext dbContext) : base(dbContext)
        {
        }
        
        public async Task<SimBalanceByDate> GetSimBalanceByDate(string simNumber,
            DateTime date)
        {
            var s = MongoDbContext.GetCollection<SimBalanceByDate>().Find(p =>
                    p.SimNumber == simNumber
                    && p.ShortDate == date.ToShortDateString())
                .Sort(Builders<SimBalanceByDate>.Sort.Descending(x => x.CreatedDate));
            return await s.FirstOrDefaultAsync();
        }
    }
}