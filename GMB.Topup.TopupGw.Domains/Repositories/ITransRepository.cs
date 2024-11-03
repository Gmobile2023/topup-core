using System.Linq;
using MongoDbGenericRepository;

// using CardRequest = GMB.Topup.Stock.Domains.Entities.CardRequest;

namespace GMB.Topup.TopupGw.Domains.Repositories;

public interface ITransRepository : IBaseMongoRepository
{
    IQueryable<TDocument> GetQueryable<TDocument>();
}