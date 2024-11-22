using System.Linq;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDbGenericRepository;

namespace Topup.Gw.Domain.Repositories;

public class PaygateMongoRepository : BaseMongoRepository, IPaygateMongoRepository
{
    private readonly ILogger<PaygateMongoRepository> _logger;

    public PaygateMongoRepository(IMongoDbContext dbContext, ILogger<PaygateMongoRepository> logger) : base(dbContext)
    {
        _logger = logger;
    }

    public IQueryable<TDocument> GetQueryable<TDocument>()
    {
        return MongoDbContext.GetCollection<TDocument>().AsQueryable();
    }


   

}


public interface ICustomMongoDbContext: IMongoDbContext
{

}
public class CustomMongoDbContext : MongoDbContext, ICustomMongoDbContext
{
    public CustomMongoDbContext(IMongoDatabase mongoDatabase) : base(mongoDatabase)
    {
    }
}
// public interface ITopupGateMongoRepository : IBaseMongoRepository
// {
//     IQueryable<TDocument> GetQueryable<TDocument>();
// }
// public class TopupGateMongoRepository : BaseMongoRepository, ITopupGateMongoRepository
// {
//     private readonly ILogger<TopupGateMongoRepository> _logger;
//
//     public TopupGateMongoRepository(ICustomMongoDbContext dbContext, ILogger<TopupGateMongoRepository> logger) : base(dbContext)
//     {
//         _logger = logger;
//     }
//
//     //public TopupGateMongoRepository(string connectionString, string databaseName = null) :
//     //           base(connectionString, databaseName)
//     //{
//     //    //  Console.WriteLine(connectionString);
//     //    // Console.WriteLine(databaseName);
//
//     //}
//
//
//
//     public IQueryable<TDocument> GetQueryable<TDocument>()
//     {
//         return MongoDbContext.GetCollection<TDocument>().AsQueryable();
//     }
//
//
// }