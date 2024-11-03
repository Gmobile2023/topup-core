using MongoDbGenericRepository;
using System;
using System.Linq;
using System.Threading.Tasks;
using HLS.Paygate.Report.Domain.Entities;

namespace HLS.Paygate.Report.Domain.Repositories
{
    public interface ISimReportMongoRepository
    {
        Task<SimBalanceByDate> GetSimBalanceByDate(string simNumber,
            DateTime date);
    }
}