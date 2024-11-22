using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoDbGenericRepository;
using Topup.Kpp.Domain.Entities;

namespace Topup.Kpp.Domain.Repositories;

public class KppMongoRepository : BaseMongoRepository, IKppMongoRepository
{
    private readonly ILogger<KppMongoRepository> _logger;

    public KppMongoRepository(IMongoDbContext dbContext, ILogger<KppMongoRepository> logger) : base(dbContext)
    {
        _logger = logger;
    }

    public IQueryable<TDocument> GetQueryable<TDocument>()
    {
        return MongoDbContext.GetCollection<TDocument>().AsQueryable();
    }

    public async Task<ReportRegisterInfo> GetRegisterInfo(string code)
    {
        Expression<Func<ReportRegisterInfo, bool>> query = p => p.Code == code;
        var first = await GetOneAsync(query);
        return first;
    }

    public async Task UpdateRegisterInfo(ReportRegisterInfo info)
    {
        Expression<Func<ReportRegisterInfo, bool>> query = p => p.Code == info.Code;
        var first = await GetOneAsync(query);
        if (first != null)
        {
            first.AccountList = info.AccountList;
            first.Content = info.Content;
            first.EmailSend = info.EmailSend;
            first.EmailCC = info.EmailCC;
            first.Name = info.Name;
            first.IsAuto = info.IsAuto;
            first.Providers = info.Providers;
            await UpdateOneAsync(first);
        }
        else
        {
            await AddOneAsync(info);
        }
    }


    public Task<List<AccountKppInfo>> GetAccountKppBalance(DateTime date)
    {
        try
        {
            var fromDate = date.AddDays(-35);
            Expression<Func<AccountKppInfo, bool>> query = p =>
                p.CreatedDate >= fromDate.ToUniversalTime()
                && p.CreatedDate < date.ToUniversalTime();

            var listSouces = GetAll(query);

            #region Cuối kỳ

            var listGroupAfter = from x in listSouces
                group x by new {x.AccountCode}
                into g
                select new AccountKppInfo
                {
                    AccountCode = g.Key.AccountCode,
                    CreatedDate = g.Max(c => c.CreatedDate)
                };

            var listViewAfter = (from x in listGroupAfter
                join yc in listSouces on x.AccountCode equals yc.AccountCode
                where x.CreatedDate == yc.CreatedDate
                select new AccountKppInfo
                {
                    AccountCode = x.AccountCode,
                    Balance = yc.Balance
                }).ToList();

            return Task.FromResult(listViewAfter);

            #endregion
        }
        catch (Exception e)
        {
            _logger.LogError($"GetAccountKppBalance error: {e}");
            return Task.FromResult(new List<AccountKppInfo>());
        }
    }

    public async Task SysAccountKppBalance(List<AccountDto> accounts, DateTime date)
    {
        try
        {
            Expression<Func<AccountKppInfo, bool>> query = p =>
                p.DateText == date.ToString("yyyyMMdd");
            var listSouces = GetAll(query);

            foreach (var item in accounts)
            {
                var f = listSouces.FirstOrDefault(c => c.AccountCode == item.AccountCode);
                if (f != null)
                {
                    f.Balance = item.After;
                    await UpdateOneAsync(f);
                }
                else
                {
                    f = new AccountKppInfo
                    {
                        CreatedDate = date,
                        AccountCode = item.AccountCode,
                        DateText = date.ToString("yyyyMMdd"),
                        Balance = item.After
                    };
                    await AddOneAsync(f);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"SysAccountKppBalance error: {ex}");
        }
    }
}