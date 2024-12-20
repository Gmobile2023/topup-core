﻿using System.Threading.Tasks;
using Topup.Common.Model.Dtos.RequestDto;
using Microsoft.Extensions.Logging;
using ServiceStack;

namespace Topup.Common.Interface.Services;

public partial class CommonService
{
    public async Task<object> GetAsync(GetAccountActivityHistoryRequest request)
    {
        _logger.LogInformation($"GetAccountActivityHistoryRequest:{request.ToJson()}");//update build
        return await _auditLog.GetAccountActivityHistories(request);
    }
}