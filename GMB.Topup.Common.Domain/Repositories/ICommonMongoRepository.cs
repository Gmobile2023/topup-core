using System.Linq;
using MongoDbGenericRepository;

namespace GMB.Topup.Common.Domain.Repositories;

public interface ICommonMongoRepository : IBaseMongoRepository
{
    IQueryable<TDocument> GetQueryable<TDocument>();
}