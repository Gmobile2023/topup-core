using System.Linq;
using MongoDbGenericRepository;

namespace HLS.Paygate.Common.Domain.Repositories;

public interface ICommonMongoRepository : IBaseMongoRepository
{
    IQueryable<TDocument> GetQueryable<TDocument>();
}