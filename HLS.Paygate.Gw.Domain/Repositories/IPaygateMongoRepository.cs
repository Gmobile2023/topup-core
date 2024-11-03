using System.Linq;
using MongoDbGenericRepository;

namespace HLS.Paygate.Gw.Domain.Repositories;

public interface IPaygateMongoRepository : IBaseMongoRepository
{
    IQueryable<TDocument> GetQueryable<TDocument>();
}