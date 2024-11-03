using System.Linq;
using MongoDbGenericRepository;

namespace HLS.Paygate.Commission.Domain.Repositories;

public interface ICommissionMongoRepository : IBaseMongoRepository
{
    IQueryable<TDocument> GetQueryable<TDocument>();
}