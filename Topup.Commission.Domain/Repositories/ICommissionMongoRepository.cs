using System.Linq;
using MongoDbGenericRepository;

namespace Topup.Commission.Domain.Repositories;

public interface ICommissionMongoRepository : IBaseMongoRepository
{
    IQueryable<TDocument> GetQueryable<TDocument>();
}