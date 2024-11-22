using System.Threading.Tasks;
using Topup.Kpp.Domain.Entities;
using Topup.Kpp.Domain.Repositories;
using Topup.Kpp.Domain.Services;
using Topup.Shared;
using Microsoft.Extensions.Logging;
using ServiceStack;
using Topup.Kpp.Interface.Dto;

namespace Topup.Kpp.Interface.Services;

public class KppService : Service
{
    private readonly IExportingService _kppPosgre;
    private readonly ILogger<KppService> _logger;
    private readonly IKppMongoRepository _mongoPosgre;

    public KppService(IExportingService kppPosgre, ILogger<KppService> logger,
        IKppMongoRepository mongoPosgre)
    {
        _kppPosgre = kppPosgre;
        _mongoPosgre = mongoPosgre;
        _logger = logger;
    }

    public async Task<object> PostAsync(KppPaymentRequest request)
    {
        //_logger.LogInformation($"KppPaymentRequest request: {request.ToJson()}");
        var rs = await _kppPosgre.KppFilePayment(request.AccountCode, request.ExportDate);
        //_logger.LogInformation($"KppPaymentRequest return: {rs.ResponseCode} - {rs.ResponseMessage}");
        return rs;
    }

    public async Task<object> PostAsync(KppTransferRequest request)
    {
        //_logger.LogInformation($"KppTransferRequest request: {request.ToJson()}");
        var rs = await _kppPosgre.KppFileTransfer(request.AccountCode, request.ExportDate);
        //_logger.LogInformation($"KppTransferRequest return: {rs.ResponseCode} - {rs.ResponseMessage}");
        return rs;
    }

    public async Task<object> PostAsync(KppAccountRequest request)
    {
        //_logger.LogInformation($"KppAccountRequest request: {request.ToJson()}");
        var rs = await _kppPosgre.KppFileAccount(request.AccountCode, request.ExportDate);
        //_logger.LogInformation($"KppAccountRequest return: {rs.ResponseCode} - {rs.ResponseMessage}");
        return rs;
    }

    public async Task<object> PostAsync(ExportFileRequest request)
    {
        await _kppPosgre.ProcessKppFile(request.date);
        return true;
    }

    public async Task<object> GetAsync(GetAccountInfoRequest request)
    {
        var rs = await _kppPosgre.KppAccount(request.AccountCode);
        return rs;
    }

    public async Task<object> PostAsync(RegisterRequest request)
    {
        var message = request.ConvertTo<ReportRegisterInfo>();
        await _mongoPosgre.UpdateRegisterInfo(message);
        return true;
    }

    public async Task<object> GetAsync(GetRegisterRequest request)
    {
        return await _mongoPosgre.GetRegisterInfo(request.Code);
    }

    public async Task<object> GetAsync(PingRouteRequest request)
    {
        return await Task.FromResult("OK");
    }
}