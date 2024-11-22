using System.Linq;
using MongoDbGenericRepository;

// using CardRequest = Topup.Stock.Domains.Entities.CardRequest;

namespace Topup.TopupGw.Domains.Repositories;

public interface ITransRepository : IBaseMongoRepository
{
    IQueryable<TDocument> GetQueryable<TDocument>();
}