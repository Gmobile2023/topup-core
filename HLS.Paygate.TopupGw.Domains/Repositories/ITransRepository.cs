using System.Linq;
using MongoDbGenericRepository;

// using CardRequest = HLS.Paygate.Stock.Domains.Entities.CardRequest;

namespace HLS.Paygate.TopupGw.Domains.Repositories;

public interface ITransRepository : IBaseMongoRepository
{
    IQueryable<TDocument> GetQueryable<TDocument>();
}