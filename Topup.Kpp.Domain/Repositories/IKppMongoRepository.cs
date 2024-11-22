using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDbGenericRepository;
using Topup.Kpp.Domain.Entities;

namespace Topup.Kpp.Domain.Repositories;

public interface IKppMongoRepository : IBaseMongoRepository
{
    IQueryable<TDocument> GetQueryable<TDocument>();
    Task<ReportRegisterInfo> GetRegisterInfo(string code);

    Task UpdateRegisterInfo(ReportRegisterInfo info);

    Task<List<AccountKppInfo>> GetAccountKppBalance(DateTime date);

    Task SysAccountKppBalance(List<AccountDto> accounts, DateTime date);
}