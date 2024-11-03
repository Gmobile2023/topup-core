using System;
using System.Threading.Tasks;
using MongoDbGenericRepository;

namespace GMB.Topup.Balance.Domain.Repositories;

public interface IBalanceMongoRepository : IBaseMongoRepository
{
    // Task<decimal> GetAccountBalanceMaxDateAsync(string accountCode, DateTime date);
}