using System;
using System.Threading.Tasks;
using Topup.Shared;
using Topup.Shared.UniqueIdGenerator;
using Microsoft.Extensions.Logging;
using Topup.Commission.Domain.Entities;

namespace Topup.Commission.Domain.Repositories;

public class CommissionRepository : ICommissionRepository
{
    private readonly ICommissionMongoRepository _commissionMongoRepository;
    private readonly ILogger<CommissionRepository> _logger;
    private readonly ITransCodeGenerator _transCodeGenerator;

    public CommissionRepository(ICommissionMongoRepository commissionMongoRepository,
        ILogger<CommissionRepository> logger, ITransCodeGenerator transCodeGenerator)
    {
        _commissionMongoRepository = commissionMongoRepository;
        _logger = logger;
        _transCodeGenerator = transCodeGenerator;
    }

    public async Task<CommissionTransaction> CommissionInsertAsync(CommissionTransaction item)
    {
        try
        {
            item.TransCode = await _transCodeGenerator.TransCodeGeneratorAsync("P");
            item.CreatedDate = DateTime.UtcNow;
            await _commissionMongoRepository.AddOneAsync(item);
            return item;
        }
        catch (Exception e)
        {
            _logger.LogError($"CommissionInsertAsync error:{e}");
            return null;
        }
    }

    public async Task<bool> CommissionUpdateAsync(CommissionTransaction item)
    {
        try
        {
            await _commissionMongoRepository.UpdateOneAsync(item);
            return true;
        }
        catch (Exception e)
        {
            _logger.LogError($"CommissionUpdateAsync error:{e}");
            return false;
        }
    }

    public async Task<bool> CommissionUpdateStatusAsync(string transcode, CommissionTransactionStatus status)
    {
        try
        {
            var check = await _commissionMongoRepository.GetOneAsync<CommissionTransaction>(x =>
                x.TransCode == transcode);
            if (check == null) return false;
            check.Status = status;
            check.ModifiedDate = DateTime.UtcNow;
            await _commissionMongoRepository.UpdateOneAsync(check);
            return true;
        }
        catch (Exception e)
        {
            _logger.LogError($"CommissionUpdateStatusAsync error:{e}");
            return false;
        }
    }

    public async Task<CommissionTransaction> GetCommissionByRef(string transRef)
    {
        return await _commissionMongoRepository.GetOneAsync<CommissionTransaction>(x => x.TransRef == transRef);
    }
}