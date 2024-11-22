using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Topup.Common.Model.Dtos;
using Topup.Common.Model.Dtos.RequestDto;
using Topup.Shared;
using Topup.Shared.Helpers;
using Microsoft.Extensions.Logging;
using ServiceStack;
using Topup.Common.Domain.Entities;
using Topup.Common.Domain.Repositories;

namespace Topup.Common.Domain.Services;

public class AuditLogService : IAuditLogService
{
    private readonly IDateTimeHelper _dateHepper;
    private readonly ILogger<AuditLogService> _logger;
    private readonly ICommonMongoRepository _reportMongoRepository;

    public AuditLogService(IDateTimeHelper dateHepper, ICommonMongoRepository reportMongoRepository,
        ILogger<AuditLogService> logger)
    {
        _dateHepper = dateHepper;
        _reportMongoRepository = reportMongoRepository;
        _logger = logger;
    }

    public async Task<bool> AddAccountActivityHistory(AccountActivityHistoryRequest request)
    {
        try
        {
            _logger.LogInformation($"AddAccountActivityHistory request:{request.ToJson()}");
            var item = request.ConvertTo<AuditAccountActivities>();
            item.CreatedDate = DateTime.UtcNow;
            item.UserCreated = request.UserName;
            await _reportMongoRepository.AddOneAsync(item);
            return true;
        }
        catch (Exception e)
        {
            _logger.LogInformation($"AddAccountActivityHistory error:{e}");
            return false;
        }
    }

    public async Task<MessagePagedResponseBase> GetAccountActivityHistories(
        GetAccountActivityHistoryRequest request)
    {
        Expression<Func<AuditAccountActivities, bool>> query = p => true;
        if (request.AccountActivityType != AccountActivityType.Default)
        {
            Expression<Func<AuditAccountActivities, bool>> newQuery = p =>
                p.AccountActivityType == request.AccountActivityType;
            query = query.And(newQuery);
        }

        if (!string.IsNullOrEmpty(request.AccountCode))
        {
            Expression<Func<AuditAccountActivities, bool>> newQuery = p =>
                p.AccountCode == request.AccountCode;
            query = query.And(newQuery);
        }

        if (!string.IsNullOrEmpty(request.PhoneNumber))
        {
            Expression<Func<AuditAccountActivities, bool>> newQuery = p =>
                p.PhoneNumber == request.PhoneNumber;
            query = query.And(newQuery);
        }

        if (!string.IsNullOrEmpty(request.UserName))
        {
            Expression<Func<AuditAccountActivities, bool>> newQuery = p =>
                p.UserCreated == request.UserName;
            query = query.And(newQuery);
        }

        if (!string.IsNullOrEmpty(request.Note))
        {
            Expression<Func<AuditAccountActivities, bool>> newQuery = p =>
                p.Note == request.Note;
            query = query.And(newQuery);
        }

        if (request.AccountType != 0)
        {
            Expression<Func<AuditAccountActivities, bool>> newQuery = p =>
                p.AccountType == request.AccountType;
            query = query.And(newQuery);
        }

        if (request.AgentType != 0)
        {
            Expression<Func<AuditAccountActivities, bool>> newQuery = p =>
                p.AgentType == request.AgentType;
            query = query.And(newQuery);
        }

        if (request.FromDate != null && request.FromDate.Value != DateTime.MinValue)
        {
            Expression<Func<AuditAccountActivities, bool>> newQuery = p =>
                p.CreatedDate >= request.FromDate.Value.ToUniversalTime();
            query = query.And(newQuery);
        }

        if (request.ToDate != null && request.ToDate.Value != DateTime.MinValue)
        {
            Expression<Func<AuditAccountActivities, bool>> newQuery = p =>
                p.CreatedDate <= request.ToDate.Value.ToUniversalTime();
            query = query.And(newQuery);
        }

        var total = await _reportMongoRepository.CountAsync(query);
        var lst = await _reportMongoRepository.GetSortedPaginatedAsync<AuditAccountActivities, Guid>(query,
            s => s.CreatedDate, false,
            request.Offset, request.Limit);
        foreach (var item in lst) item.CreatedDate = _dateHepper.ConvertToUserTime(item.CreatedDate, DateTimeKind.Utc);

        return new MessagePagedResponseBase
        {
            ResponseCode = ResponseCodeConst.Success,
            ResponseMessage = "Thành công",
            Total = (int) total,
            Payload = lst.ConvertTo<List<AuditAccountActivityDto>>()
        };
    }
}