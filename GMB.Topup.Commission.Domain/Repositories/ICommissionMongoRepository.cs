using System.Linq;
using MongoDbGenericRepository;

namespace GMB.Topup.Commission.Domain.Repositories;

public interface ICommissionMongoRepository : IBaseMongoRepository
{
    IQueryable<TDocument> GetQueryable<TDocument>();
}