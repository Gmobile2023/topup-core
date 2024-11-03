using System.Linq;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoDbGenericRepository;

namespace HLS.Paygate.Common.Domain.Repositories;

public class CommonMongoRepository : BaseMongoRepository, ICommonMongoRepository
{
    private readonly ILogger<CommonMongoRepository> _logger;

    public CommonMongoRepository(IMongoDbContext dbContext, ILogger<CommonMongoRepository> logger) : base(dbContext)
    {
        _logger = logger;
    }

    public IQueryable<TDocument> GetQueryable<TDocument>()
    {
        return MongoDbContext.GetCollection<TDocument>().AsQueryable();
    }
}