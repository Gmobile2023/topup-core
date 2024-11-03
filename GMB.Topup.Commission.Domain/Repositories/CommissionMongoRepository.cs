using System.Linq;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoDbGenericRepository;

namespace GMB.Topup.Commission.Domain.Repositories;

public class CommissionMongoRepository : BaseMongoRepository, ICommissionMongoRepository
{
    private readonly ILogger<CommissionMongoRepository> _logger;

    public CommissionMongoRepository(IMongoDbContext dbContext, ILogger<CommissionMongoRepository> logger) :
        base(dbContext)
    {
        _logger = logger;
    }

    public IQueryable<TDocument> GetQueryable<TDocument>()
    {
        return MongoDbContext.GetCollection<TDocument>().AsQueryable();
    }
}