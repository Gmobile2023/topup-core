using System.Linq;
using MongoDbGenericRepository;

namespace Topup.Gw.Domain.Repositories;

public interface IPaygateMongoRepository : IBaseMongoRepository
{
    IQueryable<TDocument> GetQueryable<TDocument>();
}