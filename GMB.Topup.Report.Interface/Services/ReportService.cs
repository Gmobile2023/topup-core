using System;
using System.Threading.Tasks;
using GMB.Topup.Contracts.Requests.Commons;
using GMB.Topup.Discovery.Requests.Commons;
using GMB.Topup.Gw.Model.Events;
using GMB.Topup.Report.Domain.Entities;
using GMB.Topup.Report.Domain.Repositories;
using GMB.Topup.Report.Domain.Services;
using GMB.Topup.Report.Model.Dtos;
using GMB.Topup.Report.Model.Dtos.RequestDto;
using GMB.Topup.Shared;
using GMB.Topup.Shared.Emailing;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ServiceStack;

namespace GMB.Topup.Report.Interface.Services;

public partial class ReportService : Service
{
    private readonly IBalanceReportService _balanceReportService;
    private readonly IBusControl _bus;
    private readonly ICardStockReportService _cardStockReportService;
    private readonly ICompareService _compareService;
    private readonly IElasticReportRepository _elasticReportService;
    private readonly IEmailSender _emailSender;
    private readonly IExportingService _exportingService;
    private readonly ILogger<ReportService> _logger;
    private readonly IAutoReportService _autoService;
    private readonly bool _searchElastich;
    private readonly IFileUploadRepository _uploadFile;

    private readonly IServiceGateway _serviceGateway;
    public ReportService(IBalanceReportService balanceReportService,
        ICardStockReportService cardStockReportService,
        IExportingService exportingService,
        ICompareService compareService,
        ILogger<ReportService> logger,
        IEmailSender emailSender,

        IBusControl bus, IConfiguration configuration,
        IElasticReportRepository elasticReportService,
        IAutoReportService autoService,
        IFileUploadRepository uploadFile)
    {
        _balanceReportService = balanceReportService;
        _cardStockReportService = cardStockReportService;
        _compareService = compareService;
        _exportingService = exportingService;

        _serviceGateway = HostContext.AppHost.GetServiceGateway();
        _emailSender = emailSender;
        _logger = logger;
        _bus = bus;
        _configuration = configuration;
        _elasticReportService = elasticReportService;
        _autoService = autoService;
        var isSearch = _configuration["ElasticSearch:IsSearch"];
        if (isSearch == null)
        {
            _searchElastich = false;
        }
        else
        {
            if (Convert.ToBoolean(isSearch)) _searchElastich = true;
            else _searchElastich = false;
        }

        _uploadFile = uploadFile;
    }

    private IConfiguration _configuration { get; }

    public async Task<object> Get(ReportDetailRequest request)
    {
        _logger.LogInformation($"ReportDetailRequest request: {request.ToJson()}");
        var rs = _searchElastich
            ? await _elasticReportService.ReportDetailGetList(request)
            : await _balanceReportService.ReportDetailGetList(request);
        _logger.LogInformation($"ReportDetailRequest return: {rs.ResponseCode} - {rs.ResponseMessage}");
        return rs;
    }

    public async Task<object> Get(TestSendTele request)
    {
        _serviceGateway.Publish(new CommonSendMessageTeleRequest
        {
            BotType = BotType.Sale,
            Module = "Report",
            Title = "Test send tele",
            Message = "Send tele msg Ok",
            MessageType = BotMessageType.Wraning
        });

        return new ResponseMessageApi<object>()
        {
            Success = true,
        };
    }

    public async Task<object> Get(RevenueInDayRequest request)
    {
        _logger.LogInformation($"RevenueInDayRequest request: {request.ToJson()}");
        var rs = _searchElastich
            ? await _elasticReportService.ReportRevenueInDayQuery(request)
            : await _balanceReportService.ReportRevenueInDayQuery(request);
        _logger.LogInformation($"RevenueInDayRequest return: {rs}");
        return rs;
    }

    public async Task<object> Get(TransDetailByTransCodeRequest request)
    {
        _logger.LogInformation($"TransDetailByTransCodeRequest request: {request.ToJson()}");
        var rs = _searchElastich
           ? await _elasticReportService.ReportTransDetailQuery(request)
           : await _balanceReportService.ReportTransDetailQuery(request);
        _logger.LogInformation($"TransDetailByTransCodeRequest return: {rs}");
        return rs;
    }

    public async Task<object> Get(ReportTransDetailRequest request)
    {
        _logger.LogInformation($"ReportTransDetailRequest request: {request.ToJson()}");
        var rs = _searchElastich
            ? await _elasticReportService.ReportTransDetailGetList(request)
            : await _balanceReportService.ReportTransDetailGetList(request);
        _logger.LogInformation($"ReportTransDetailRequest return: {rs.ResponseCode} - {rs.ResponseMessage}");
        return rs;
    }

    public async Task<object> Get(ReportDebtDetailRequest request)
    {
        _logger.LogInformation($"ReportDebtDetailRequest request: {request.ToJson()}");
        var rs = _searchElastich
            ? await _elasticReportService.ReportDebtDetailGetList(request)
            : await _balanceReportService.ReportDebtDetailGetList(request);
        _logger.LogInformation($"ReportDebtDetailRequest return: {rs.ResponseCode} - {rs.ResponseMessage}");
        return rs;
    }

    public async Task<object> Get(ReportTotalDebtRequest request)
    {
        _logger.LogInformation($"ReportTotalDebtRequest request: {request.ToJson()}");
        var rs = _searchElastich
            ? await _elasticReportService.ReportTotalDebtGetList(request)
            : await _balanceReportService.ReportTotalDebtGetList(request);
        _logger.LogInformation($"ReportTotalDebtRequest return: {rs.ResponseCode} - {rs.ResponseMessage}");
        return rs;
    }

    public async Task<object> Get(ReportTotalDayRequest request)
    {
        _logger.LogInformation($"ReportTotalDayRequest request: {request.ToJson()}");
        var rs = _searchElastich
            ? await _elasticReportService.ReportTotalDayGetList(request)
            : await _balanceReportService.ReportTotalDayGetList(request);
        _logger.LogInformation($"ReportTotalDayRequest return: {rs.ResponseCode} - {rs.ResponseMessage}");
        return rs;
    }

    public async Task<object> Get(ReportRefundDetailRequest request)
    {
        _logger.LogInformation($"ReportRefundDetailRequest request: {request.ToJson()}");
        var rs = _searchElastich
            ? await _elasticReportService.ReportRefundDetailGetList(request)
            : await _balanceReportService.ReportRefundDetailGetList(request);
        _logger.LogInformation($"ReportRefundDetailRequest return: {rs.ResponseCode} - {rs.ResponseMessage}");
        return rs;
    }

    public async Task<object> Get(ReportTransferDetailRequest request)
    {
        _logger.LogInformation($"ReportTransferDetailRequest request: {request.ToJson()}");
        var rs = _searchElastich
            ? await _elasticReportService.ReportTransferDetailGetList(request)
            : await _balanceReportService.ReportTransferDetailGetList(request);
        _logger.LogInformation($"ReportTransferDetailRequest return: {rs.ResponseCode} - {rs.ResponseMessage}");
        return rs;
    }

    public async Task<object> Get(ReportServiceDetailRequest request)
    {
        _logger.LogInformation($"ReportServiceDetailRequest request: {request.ToJson()}");
        var rs = _searchElastich
            ? await _elasticReportService.ReportServiceDetailGetList(request)
            : await _balanceReportService.ReportServiceDetailGetList(request);

        _logger.LogInformation($"ReportServiceDetailRequest return: {rs.ResponseCode} - {rs.ResponseMessage}");
        return rs;
    }

    public async Task<object> Get(ReportServiceTotalRequest request)
    {
        _logger.LogInformation($"ReportServiceTotalRequest request: {request.ToJson()}");
        var rs = _searchElastich
            ? await _elasticReportService.ReportServiceTotalGetList(request)
            : await _balanceReportService.ReportServiceTotalGetList(request);
        _logger.LogInformation($"ReportServiceTotalRequest return: {rs.ResponseCode} - {rs.ResponseMessage}");
        return rs;
    }

    public async Task<object> Get(ReportServiceProviderRequest request)
    {
        _logger.LogInformation($"ReportServiceProviderRequest request: {request.ToJson()}");
        var rs = await _elasticReportService.ReportServiceProviderGetList(request);
        _logger.LogInformation($"ReportServiceProviderRequest return: {rs.ResponseCode} - {rs.ResponseMessage}");
        return rs;
    }

    public async Task<object> Get(ReportAgentBalanceRequest request)
    {
        _logger.LogInformation($"ReportAgentBalanceRequest request: {request.ToJson()}");
        var rs = _searchElastich
            ? await _elasticReportService.ReportAgentBalanceGetList(request)
            : await _balanceReportService.ReportAgentBalanceGetList(request);
        _logger.LogInformation($"ReportAgentBalanceRequest return: {rs.ResponseCode} - {rs.ResponseMessage}");
        return rs;
    }

    public async Task<object> Get(ReportRevenueAgentRequest request)
    {
        _logger.LogInformation($"ReportRevenueAgentRequest request: {request.ToJson()}");
        var rs = _searchElastich
            ? await _elasticReportService.ReportRevenueAgentGetList(request)
            : await _balanceReportService.ReportRevenueAgentGetList(request);

        _logger.LogInformation($"ReportRevenueAgentRequest return: {rs.ResponseCode} - {rs.ResponseMessage}");
        return rs;
    }

    public async Task<object> Get(ReportRevenueCityRequest request)
    {
        _logger.LogInformation($"ReportRevenueCityRequest request: {request.ToJson()}");
        var rs = _searchElastich
            ? await _elasticReportService.ReportRevenueCityGetList(request)
            : await _balanceReportService.ReportRevenueCityGetList(request);
        _logger.LogInformation($"ReportRevenueCityRequest return: {rs.ResponseCode} - {rs.ResponseMessage}");
        return rs;
    }

    public async Task<object> Get(ReportTotalSaleAgentRequest request)
    {
        _logger.LogInformation($"ReportTotalSaleAgentRequest request: {request.ToJson()}");
        var rs = _searchElastich
            ? await _elasticReportService.ReportTotalSaleAgentGetList(request)
            : await _balanceReportService.ReportTotalSaleAgentGetList(request);
        _logger.LogInformation($"ReportTotalSaleAgentRequest return: {rs.ResponseCode} - {rs.ResponseMessage}");
        return rs;
    }

    public async Task<object> Get(ReportRevenueActiveRequest request)
    {
        _logger.LogInformation($"ReportRevenueActiveRequest request: {request.ToJson()}");
        var rs = _searchElastich
            ? await _elasticReportService.ReportRevenueActiveGetList(request)
            : await _balanceReportService.ReportRevenueActiveGetList(request);
        _logger.LogInformation($"ReportRevenueActiveRequest return: {rs.ResponseCode} - {rs.ResponseMessage}");
        return rs;
    }

    public async Task<object> Post(SyncAccountRequest request)
    {
        if (request.AccountCode != "1")
        {
            var message = request.ConvertTo<ReportSyncAccounMessage>();
            await _bus.Publish(message);
        }

        return true;
    }

    public async Task<object> Get(CheckBalanceSupplierRequest request)
    {
        return await _balanceReportService.CheckTopupBalance(request.Providercode);
    }

    public async Task<object> Get(GetRegisterRequest request)
    {
        return await _balanceReportService.GetRegisterInfo(request.Code);
    }

    public async Task<object> Post(SyncRegisterRequest request)
    {
        var message = request.ConvertTo<ReportRegisterInfo>();
        await _balanceReportService.UpdateRegisterInfo(message);
        return true;
    }

    public async Task<object> Post(RemoveKeyRequest request)
    {
        _logger.LogInformation($"RemoveKeyRequest request: {request.ToJson()}");
        var rs = await _elasticReportService.RemoveKeyData(request.Key);
        _logger.LogInformation($"RemoveKeyRequest return: {rs.ToJson()}");
        return rs;
    }

    public async Task<object> Get(SyncNXTProviderRequest request)
    {
        _logger.LogInformation($"SyncNXTProviderRequest request: {request.ToJson()}");
        var rs = await _balanceReportService.ReportSyncNxtProviderRequest(request);
        _logger.LogInformation($"SyncNXTProviderRequest return: {rs.ResponseCode} - {rs.ResponseMessage}");
        return rs;
    }

    public async Task<object> Get(BalanceTotalRequest request)
    {
        _logger.LogInformation($"BalanceTotalRequest request: {request.ToJson()}");
        var rs = _searchElastich
            ? await _elasticReportService.ReportBalanceTotalGetList(request)
            : await _balanceReportService.ReportBalanceTotalGetList(request);
        _logger.LogInformation($"BalanceTotalRequest return: {rs.ResponseCode} - {rs.ResponseMessage}");
        return rs;
    }

    public async Task<object> Get(BalanceGroupTotalRequest request)
    {
        _logger.LogInformation($"BalanceGroupTotalRequest request: {request.ToJson()}");
        var rs = _searchElastich
            ? await _elasticReportService.ReportBalanceGroupTotalGetList(request)
            : await _balanceReportService.ReportBalanceGroupTotalGetList(request);
        _logger.LogInformation($"BalanceGroupTotalRequest return: {rs.ResponseCode} - {rs.ResponseMessage}");
        return rs;
    }

    public async Task<object> Get(CardStockHistoriesRequest request)
    {
        _logger.LogInformation($"CardStockHistoriesRequest request: {request.ToJson()}");
        var rs = _searchElastich
            ? await _elasticReportService.GetCardStockHistories(request)
            : await _cardStockReportService.CardStockHistories(request);

        _logger.LogInformation($"CardStockHistoriesRequest return: {rs}");
        return rs;
    }

    public async Task<object> Get(CardStockImExPortRequest request)
    {
        _logger.LogInformation($"CardStockImExPortRequest request: {request.ToJson()}");
        var rs = _searchElastich
            ? await _elasticReportService.CardStockImExPort(request)
            : await _cardStockReportService.CardStockImExPort(request);
        _logger.LogInformation($"CardStockImExPortRequest return: {rs}");
        return rs;
    }

    public async Task<object> Get(CardStockImExPortProviderRequest request)
    {
        _logger.LogInformation($"CardStockImExPortProviderRequest request: {request.ToJson()}");
        var rs = _searchElastich
            ? await _elasticReportService.CardStockImExPortProvider(request)
            : await _cardStockReportService.CardStockImExPortProvider(request);
        _logger.LogInformation($"CardStockImExPortProviderRequest return: {rs}");
        return rs;
    }

    public async Task<object> Get(CardStockInventoryRequest request)
    {
        _logger.LogInformation($"CardStockInventoryRequest request: {request.ToJson()}");
        var rs = _searchElastich
            ? await _elasticReportService.CardStockInventory(request)
            : await _cardStockReportService.CardStockInventory(request);
        _logger.LogInformation($"CardStockInventoryRequest return: {rs}");
        return rs;
    }

    public async Task<object> Post(SmsMessageRequest request)
    {
        _logger.LogInformation($"SmsMessageRequest request: {request.ToJson()}");
        var rs = await _balanceReportService.InsertSmsMessage(request);
        _logger.LogInformation($"SmsMessageRequest return: {rs}");
        return rs;
    }

    public async Task<object> Get(ReportCompareListRequest request)
    {
        _logger.LogInformation($"ReportCompareListRequest request: {request.ToJson()}");
        var rs = await _compareService.ReportCompareGetList(request);
        _logger.LogInformation($"ReportCompareListRequest return: {rs.ResponseCode} - {rs.ResponseMessage}");
        return rs;
    }

    public async Task<object> Get(ReportCheckCompareRequest request)
    {
        _logger.LogInformation($"{request.ProviderCode} ReportCheckCompareRequest request: {request.ToJson()}");
        var rs = await _compareService.ReportCheckCompareGet(request);
        _logger.LogInformation($"{request.ProviderCode} ReportCheckCompareRequest return: {rs.ResponseCode} - {rs.ResponseMessage}-{rs.Payload}");
        return rs;
    }

    public async Task<object> Get(ReportCompareRefundDetailRequest request)
    {
        _logger.LogInformation($"{request.ProviderCode} ReportCompareRefundDetailRequest request: {request.ToJson()}");
        var rs = await _compareService.ReportCompareRefundDetail(request);
        _logger.LogInformation($"{request.ProviderCode} ReportCompareRefundDetailRequest return: {rs.ResponseCode} - {rs.ResponseMessage}");
        return rs;
    }

    public async Task<object> Get(ReportCompareRefundRequest request)
    {
        _logger.LogInformation($"{request.ProviderCode} ReportCompareRefundRequest request: {request.ToJson()}");
        var rs = await _compareService.ReportCompareRefundList(request);
        _logger.LogInformation($"{request.ProviderCode} ReportCompareRefundRequest return: {rs.ResponseCode} - {rs.ResponseMessage}");
        return rs;
    }

    public async Task<object> Get(ReportCompareRefundSingleRequest request)
    {
        _logger.LogInformation($"{request.ProviderCode} ReportCompareRefundSingleRequest request: {request.ToJson()}");
        var rs = await _compareService.ReportCompareRefundSingle(request);
        _logger.LogInformation($"{request.ProviderCode} ReportCompareRefundSingleRequest return: {rs.ResponseCode} - {rs.ResponseMessage}");
        return rs;
    }

    public async Task<object> Get(ReportCompareReonseRequest request)
    {
        _logger.LogInformation($"{request.ProviderCode} ReportCompareReonseRequest request: {request.ToJson()}");
        var rs = await _compareService.ReportCompareReonseList(request);
        _logger.LogInformation($"{request.ProviderCode} ReportCompareReonseRequest return: {rs.ResponseCode} - {rs.ResponseMessage}");
        return rs;
    }

    public async Task<object> Get(ReportCompareDetailReonseRequest request)
    {
        _logger.LogInformation($"{request.ProviderCode} ReportCompareDetailReonseRequest request: {request.ToJson()}");
        var rs = await _compareService.ReportCompareDetailReonseList(request);
        _logger.LogInformation($"{request.ProviderCode} ReportCompareDetailReonseRequest return: {rs.ResponseCode} - {rs.ResponseMessage}");
        return rs;
    }

    public async Task<object> Get(ReportRevenueDashBoardDayRequest request)
    {
        _logger.LogInformation($"{request.LoginCode} ReportRevenueDashBoardDayRequest request: {request.ToJson()}");
        var rs = _searchElastich
            ? await _elasticReportService.ReportRevenueDashBoardDayGetList(request)
            : await _balanceReportService.ReportRevenueDashBoardDayGetList(request);
        _logger.LogInformation($"{request.LoginCode} ReportRevenueDashBoardDayRequest return: {rs.ResponseCode} - {rs.ResponseMessage}");
        return rs;
    }

    public async Task<object> Get(ReportAgentGeneralDashRequest request)
    {
        _logger.LogInformation($"{request.LoginCode} ReportAgentGeneralDashRequest request: {request.ToJson()}");
        var rs = _searchElastich
            ? await _elasticReportService.ReportAgentGeneralDayGetDash(request)
            : await _balanceReportService.ReportAgentGeneralDayGetDash(request);
        _logger.LogInformation($"{request.LoginCode} ReportAgentGeneralDashRequest return: {rs.ResponseCode} - {rs.ResponseMessage}");
        return rs;
    }

    public async Task<object> Post(CompareProviderRequest request)
    {
        _logger.LogInformation($"{request.ProviderCode} CompareProviderRequest request: {request.Items.Count.ToJson()}");
        var rs = await _compareService.CompareProviderData(request);
        _logger.LogInformation($"{request.ProviderCode} CompareProviderRequest return: {rs.ResponseCode} - {rs.ResponseMessage}");
        return rs;
    }

    public async Task<object> Post(CompareRefundCompareRequest request)
    {
        _logger.LogInformation($"{request.ProviderCode} CompareRefundCompareRequest request: {request.ToJson()}");
        var rs = await _compareService.RefundCompareData(request);
        _logger.LogInformation($"{request.ProviderCode} CompareRefundCompareRequest return: {rs.ResponseCode} - {rs.ResponseMessage}");
        return rs;
    }

    public async Task<object> Post(TestFptRequest request)
    {
        //_uploadFile.UploadFileToDataServerPullFpt(request.ProviderCode, null, "xuanpt.csv");
        await _autoService.SysJobTest();
        return true;
    }

    public async Task<object> Get(PingRouteRequest request)
    {
        return await Task.FromResult("OK");
    }

    public async Task<object> Get(ReportTopupRequestLogs request)
    {
        _logger.LogInformation($"ReportTopupRequestLogs request: {request.ToJson()}");
        var rs = _searchElastich
            ? await _elasticReportService.ReportTopupRequestLogGetList(request)
            : await _balanceReportService.ReportTopupRequestLogGetList(request);
        _logger.LogInformation($"ReportTopupRequestLogs return: {rs.ResponseCode} - {rs.ResponseMessage}");
        return rs;
    }

    public async Task<object> Get(CheckMobileTransRequest request)
    {
        _logger.LogInformation($"CheckMobileTransRequest request: {request.ToJson()}");
        var register = await _balanceReportService.GetRegisterInfo(ReportRegisterType.CHECK_REVENUE, isCache: true);
        if (register != null)
        {
            var rs = await _elasticReportService.QueryMobileTransData(register, request);
            _logger.LogInformation($"CheckMobileTransRequest return: {rs.ToJson()}");
            return rs;
        }

        return new NewMessageResponseBase<ItemMobileCheckDto>()
        {
            ResponseStatus=new ResponseStatusApi(ResponseCodeConst.Error, "Truy vấn thất bại"),
        };     
    }   
}