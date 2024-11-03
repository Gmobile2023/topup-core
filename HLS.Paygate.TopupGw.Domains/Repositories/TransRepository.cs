using System.Linq;
using HLS.Paygate.Shared.CacheManager;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoDbGenericRepository;

namespace HLS.Paygate.TopupGw.Domains.Repositories;

public class TransRepository : BaseMongoRepository, ITransRepository
{
    private readonly ICacheManager _cacheManager;
    private readonly ILogger<TransRepository> _logger; //LogManager.GetLogger("CardMongoRepository");

    public TransRepository(string connectionString, ICacheManager cacheManager, ILogger<TransRepository> logger,
        string databaseName = null) :
        base(connectionString,
            databaseName)
    {
        _cacheManager = cacheManager;
        _logger = logger;
    }

    public TransRepository(IMongoDbContext dbContext, ICacheManager cacheManager, ILogger<TransRepository> logger) :
        base(dbContext)
    {
        _cacheManager = cacheManager;
        _logger = logger;
    }

    public IQueryable<TDocument> GetQueryable<TDocument>()
    {
        return MongoDbContext.GetCollection<TDocument>().AsQueryable();
    }
}