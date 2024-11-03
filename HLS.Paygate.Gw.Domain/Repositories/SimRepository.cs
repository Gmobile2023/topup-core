using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using HLS.Paygate.Gw.Domain.Entities;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using MongoDbGenericRepository;
using NLog;
using ServiceStack;
using HLS.Paygate.Gw.Model.Enums;
using HLS.Paygate.Shared;

namespace HLS.Paygate.Gw.Domain.Repositories
{
    public class SimRepository : BaseMongoRepository, ISimRepository
    {
        private readonly Logger _logger = LogManager.GetLogger("SimRepository");
        private readonly IConfiguration _configuration;

        public SimRepository(IMongoDbContext dbContext, IConfiguration configuration) : base(dbContext)
        {
            _configuration = configuration;
        }

        public async Task<Sim> GetSimForMappingAsync(string stockType, SimAppType simType,
            SimMappingType simMappingType, int cardvalue)
        {
            _logger.LogInformation($"GetSimForMapping request: {stockType}-{simMappingType}-{simType}");
            using (var session =
                await MongoDbContext.Client.StartSessionAsync(new ClientSessionOptions {CausalConsistency = true}))
            {
                var sims = MongoDbContext.GetCollection<Sim>();
                try
                {
                    session.StartTransaction(new TransactionOptions(
                        readConcern: ReadConcern.Majority,
                        writeConcern: WriteConcern.WMajority
                    ));
                    var sort = Builders<Sim>.Sort.Ascending(x => x.LastTransTime);

                    var options = new FindOneAndUpdateOptions<Sim, Sim>
                    {
                        IsUpsert = false,
                        ReturnDocument = ReturnDocument.After,
                        Sort = sort
                    };
                    Expression<Func<Sim, bool>> query = p =>
                        p.Status == SimStatus.Active && p.StockType == stockType && p.SimAppType == simType &&
                        p.IsInprogress == false;
                    var maxTransAmount = decimal.Parse(_configuration["Topup:Mapping:SimMaxTransferAmount"]);
                    switch (simMappingType)
                    {
                        case SimMappingType.Topup:
                        {
                            Expression<Func<Sim, bool>> newQuery = p =>
                                p.LastTransTime < DateTime.Now.Date ||
                                (p.TransTimesInDay <= 3 && p.LastTransTime > DateTime.Now.Date);
                            query = query.And(newQuery);
                            break;
                        }
                        case SimMappingType.TopupTKC:
                        {
                            var checkBalance = cardvalue; //Chỗ này lấy fee từ client luôn + GetFeeTransfer(cardvalue);
                            Expression<Func<Sim, bool>> newQuery = p =>
                                p.IsSimPostpaid == false && p.SimBalance >= checkBalance &&
                                (p.LastTransTime < DateTime.Now.Date || p.TotalTransAmount < maxTransAmount);
                            query = query.And(newQuery);
                            break;
                        }
                        case SimMappingType.DepositSim:
                        {
                            //chỗ này xem thêm lại điều kiện
                            Expression<Func<Sim, bool>> newQuery = p =>
                                p.IsSimPostpaid == false &&
                                (p.SimBalance < maxTransAmount);
                            query = query.And(newQuery);
                            break;
                        }
                        default:
                            throw new ArgumentOutOfRangeException(nameof(simMappingType), simMappingType, null);
                    }

                    var update = Builders<Sim>.Update.Set(x => x.IsInprogress, true);

                    var result = await sims.FindOneAndUpdateAsync(session, query, update, options);
                    if (result == null)
                    {
                        await session.AbortTransactionAsync();
                        return null;
                    }

                    _logger.LogInformation($"GetSimForMapping return: {result.ToJson()}");
                    if (result.LastTransTime.Date < DateTime.Now.Date)
                    {
                        await MongoDbContext.GetCollection<Sim>().UpdateOneAsync(session,
                            Builders<Sim>.Filter.Eq(x => x.Id, result.Id),
                            Builders<Sim>.Update.Set(x => x.TotalTransAmount, 0)
                                .Set(x => x.TransTimesInDay, 0),
                            new UpdateOptions {IsUpsert = false}
                        );
                    }

                    await session.CommitTransactionAsync();
                    return result;
                }
                catch (Exception e)
                {
                    _logger.LogError($"GetSimForMapping error: {e}");
                    await session.AbortTransactionAsync();
                    Console.WriteLine(e);
                    return null;
                }
            }
        }


        private decimal GetFeeTransfer(int cardvalue)
        {
            if (cardvalue == 10000)
                return 500;
            if (cardvalue == 20000)
                return 1000;
            if (cardvalue == 30000)
                return 6000;
            if (cardvalue == 50000)
                return 10000;
            return 0;
        }
    }
}
