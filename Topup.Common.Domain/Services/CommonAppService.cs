using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Topup.Common.Model.Dtos;
using Topup.Common.Model.Dtos.RequestDto;
using Topup.Common.Model.Dtos.ResponseDto;
using Topup.Shared;
using Topup.Shared.ConfigDtos;
using Topup.Shared.Dtos;
using Topup.Shared.Emailing;
using Topup.Shared.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Topup.Contracts.Requests.Commons;
using Topup.Discovery.Requests.Balance;
using Topup.Discovery.Requests.Workers;
using ServiceStack;
using Topup.Common.Domain.Entities;
using Topup.Common.Domain.Repositories;

namespace Topup.Common.Domain.Services;

public class CommonAppService : ICommonAppService
{
    private readonly IBotMessageService _bot;
    private readonly IConfiguration _configuration;

    private readonly IDateTimeHelper _dateHepper;

    private readonly IEmailSender _emailSender;

    //private readonly IServiceGateway _gateway; gunner
    private readonly ILogger<CommonAppService> _logger;
    private readonly GrpcClientHepper _grpcClient;
    private readonly INotificationSevice _notification;
    private readonly ICommonMongoRepository _commonRepository;

    public CommonAppService(IConfiguration configuration, ICommonMongoRepository commonRepository,
        ILogger<CommonAppService> logger, IDateTimeHelper dateHepper,
        INotificationSevice notification,
        IBotMessageService bot, IEmailSender emailSender, GrpcClientHepper grpcClient)
    {
        _configuration = configuration;
        _commonRepository = commonRepository;
        _logger = logger;
        _dateHepper = dateHepper;
        //_billQueryRequestClient = billQueryRequestClient;
        _notification = notification;
        _bot = bot;
        _emailSender = emailSender;
        _grpcClient = grpcClient;
        //_gateway = HostContext.AppHost.GetServiceGateway(); gunner
    }

    public async Task<bool> SavePayBill(SavePayBillRequest request)
    {
        try
        {
            var check = await _commonRepository.GetOneAsync<PayBillAccounts>(x =>
                x.AccountCode == request.AccountCode && x.ProductCode == request.ProductCode &&
                x.InvoiceCode == request.InvoiceCode);
            if (check == null)
            {
                var item = request.ConvertTo<PayBillAccounts>();
                item.CreatedDate = DateTime.UtcNow;
                item.IsQueryBill = true;
                item.Status = PayBillCustomerStatus.Paid;
                item.ServiceCode = "PAY_BILL";
                await _commonRepository.AddOneAsync(item);
            }
            else
            {
                check.Status = PayBillCustomerStatus.Paid;
                if (!string.IsNullOrEmpty(request.InvoiceInfo) && request.InvoiceInfo != check.InvoiceInfo)
                    check.InvoiceInfo = request.InvoiceInfo;

                if (!string.IsNullOrEmpty(request.LastProviderCode) &&
                    request.LastProviderCode != check.LastProviderCode)
                    check.LastProviderCode = request.LastProviderCode;

                if (!string.IsNullOrEmpty(request.ProductName) && request.ProductName != check.ProductName)
                    check.ProductName = request.ProductName;

                if (!string.IsNullOrEmpty(request.LastTransCode) && request.LastProviderCode != check.LastTransCode)
                    check.LastTransCode = request.LastTransCode;

                check.LastTransDate = DateTime.UtcNow;
                await _commonRepository.UpdateOneAsync(check);
            }

            return true;
        }
        catch (Exception e)
        {
            _logger.LogError($"SavePayBill error:{e}");
            return false;
        }
    }

    public async Task<bool> RemoveSavePayBill(RemoveSavePayBillRequest request)
    {
        try
        {
            var check = await _commonRepository.GetOneAsync<PayBillAccounts>(x =>
                x.AccountCode == request.AccountCode && x.ProductCode == request.ProductCode &&
                x.InvoiceCode == request.InvoiceCode);
            if (check == null) return false;
            await _commonRepository.DeleteOneAsync(check);
            return true;
        }
        catch (Exception e)
        {
            return false;
        }
    }

    public async Task<List<PayBillAccountsDto>> GetSavePayBill(GetSavePayBillRequest request)
    {
        Expression<Func<PayBillAccounts, bool>> query = p => p.AccountCode == request.AccountCode;
        if (!string.IsNullOrEmpty(request.ProductCode))
        {
            Expression<Func<PayBillAccounts, bool>> newQuery = p =>
                p.ProductCode == request.ProductCode;
            query = query.And(newQuery);
        }

        if (!string.IsNullOrEmpty(request.Search))
        {
            Expression<Func<PayBillAccounts, bool>> newQuery = p =>
                p.ProductName.ToLower().Contains(request.Search.ToLower());
            query = query.And(newQuery);
        }

        if (request.Status != PayBillCustomerStatus.Default)
        {
            Expression<Func<PayBillAccounts, bool>> newQuery = p =>
                p.Status == request.Status;
            query = query.And(newQuery);
        }

        var lst = await _commonRepository.GetAllAsync(query);
        var listBill = lst.ConvertTo<List<PayBillAccountsDto>>();
        foreach (var item in listBill)
        {
            var info = item.InvoiceInfo.FromJson<InvoiceDto>();
            item.CreatedDate = _dateHepper.ConvertToUserTime(item.CreatedDate, DateTimeKind.Utc);
            if (item.ModifiedDate != null)
                item.ModifiedDate = _dateHepper.ConvertToUserTime(item.ModifiedDate.Value, DateTimeKind.Utc);
            if (item.LastTransDate != null)
                item.LastTransDate = _dateHepper.ConvertToUserTime(item.LastTransDate.Value, DateTimeKind.Utc);
            item.ProductName = info?.ProductName;
        }

        return listBill;
    }

    public async Task<long> GetTotalWaitingBill(GetTotalWaitingBillRequest request)
    {
        return await _commonRepository.CountAsync<PayBillAccounts>(x =>
            x.AccountCode == request.AccountCode && x.Status == PayBillCustomerStatus.Unpaid);
    }

    public async Task AutoCheckPayBill()
    {
        _logger.LogInformation("Begin_AutoCheckPayBill");
        var listPay = new List<PayBillAccounts>();
        var list = await _commonRepository.GetAllAsync<PayBillAccounts>(x =>
            x.Status == PayBillCustomerStatus.Paid && x.IsQueryBill);
        if (list == null || !list.Any())
            return;
        if (bool.Parse(_configuration["Hangfire:AutoQueryBill:IsTest"]))
        {
            listPay.AddRange(list);
        }
        else
        {
            //var retryConfig = int.Parse(_configuration["Hangfire:AutoQueryBill:RetryCount"]);
            var date = DateTime.UtcNow;
            foreach (var item in list)
                if (date.Year > item.CreatedDate.Year)
                    listPay.Add(item);
                else if (date.Month > item.CreatedDate.Month)
                    listPay.Add(item);
        }

        if (!listPay.Any())
            return;

        foreach (var item in listPay)
        {
            _logger.LogInformation($"ProcessQueryBill:{item.ToJson()}");
            var response = await _grpcClient.GetClientCluster(GrpcServiceName.Worker).SendAsync(
                new WorkerBillQueryRequest
                {
                    CategoryCode = item.CategoryCode,
                    ReceiverInfo = item.InvoiceCode,
                    ServiceCode = ServiceCodes.QUERY_BILL,
                    ProductCode = item.ProductCode,
                    TransCode = DateTime.Now.ToString("ddMMyyyyhhmmss")
                });
            _logger.LogInformation($"ProcessQueryBillResponse:{response.ToJson()}");
            if (response.ResponseStatus.ErrorCode == ResponseCodeConst.Success)
            {
                var info = response.Results;
                item.Status = PayBillCustomerStatus.Unpaid;
                item.InvoiceQueryInfo = info.ToJson();
                item.PaymentAmount = info.Amount;
                await SendNotifi(item);
            }
            else
            {
                item.RetryCount++;
                _logger.LogInformation($"ProcessQueryBillError:{response.ToJson()}");
            }

            item.ResponseQuery = response.ToJson();
            item.LastQueryDate = DateTime.UtcNow;
            await _commonRepository.UpdateOneAsync(item);
        }
    }

    public async Task WarningBalance()
    {
        _logger.LogInformation("WarningBalance processing");
        try
        {
            var list = await _commonRepository.GetAllAsync<AlarmBalanceConfig>(x => x.IsRun == true);
            if (list == null || !list.Any())
            {
                _logger.LogInformation("No account to check");
            }

            foreach (var account in list)
            {
                _logger.LogInformation(
                    $"CheckBalance account: {account.AccountName}-{account.AccountCode}-{account.TeleChatId}");
                var rs = await _grpcClient.GetClientCluster(GrpcServiceName.Balance).SendAsync(
                    new AccountBalanceCheckRequest
                    {
                        AccountCode = account.AccountCode,
                        CurrencyCode = !string.IsNullOrEmpty(account.CurrencyCode)
                            ? account.CurrencyCode
                            : CurrencyCode.VND.ToString("G")
                    });
                _logger.LogInformation(
                    $"CheckBalance: {account.AccountName}-{account.AccountCode} return: {rs.ToJson()}");

                var balanceAccount = rs.Result;
                if (balanceAccount <= account.MinBalance)
                    await _bot.SendAlarmMessage(new SendAlarmMessageInput
                    {
                        Title =
                            $"{balanceAccount.ToFormat("đ")} là số dư hiện tại của TK: {account.AccountName}-{account.AccountCode}",
                        Message = "Vui lòng nạp tiền để đảm bảo dịch vụ. Trân trọng cám ơn!",
                        Module = "Balance",
                        MessageType = BotMessageType.Wraning,
                        ChatId = account.TeleChatId
                    });
            }
        }
        catch (Exception e)
        {
            _logger.LogInformation($"WarningBalance_Error:{e}");
        }
    }

    private async Task SendNotifi(PayBillAccounts item)
    {
        try
        {
            _logger.LogInformation($"SendNotifiAuto:{item.ToJson()}");
            var notificationData = new NotificationAppData
            {
                Properties = new SendNotificationData
                {
                    Amount = item.PaymentAmount,
                    CategoryCode = item.CategoryCode,
                    PartnerCode = item.AccountCode,
                    ProductCode = item.ProductCode,
                    ServiceCode = string.IsNullOrEmpty(item.ServiceCode) ? "PAY_BILL" : item.ServiceCode,
                    TransCode = item.LastTransCode,
                    TransType = "PAYMENT",
                    Payload = item.InvoiceInfo
                },
                Type = "App.WaitingPayBill"
            };
            await _notification.SendNotification(new SendNotificationRequest
            {
                Title = "Hóa đơn đã đến kỳ thanh toán",
                Body =
                    $"Đã đến kỳ thanh toán hóa đơn: {item.ProductName}, mã hóa đơn: {item.InvoiceCode}, số tiền: {item.PaymentAmount.ToFormat("đ")}. Vui lòng bỏ qua nếu bạn đã thanh toán",
                AccountCode = item.AccountCode,
                Severity = NotificationSeverity.Info.ToString("G"),
                AppNotificationName = "App.WaitingPayBill",
                Data = notificationData.ToJson()
            });
        }
        catch (Exception e)
        {
            _logger.LogError($"SendNotifiAutoError:{e}");
        }
    }

    public async Task<bool> AlarmBalanceCreateAsync(AlarmBalanceConfigDto request)
    {
        try
        {
            var dto = request.ConvertTo<AlarmBalanceConfig>();
            dto.CreatedDate = DateTime.Now;
            await _commonRepository.AddOneAsync(dto);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError("AlarmBalanceCreateAsync error:" + ex);
            return false;
        }
    }

    public async Task<bool> AlarmBalanceUpdateAsync(AlarmBalanceConfigDto request)
    {
        try
        {
            var dto = request.ConvertTo<AlarmBalanceConfig>();
            dto.ModifiedDate = DateTime.Now;
            return await _commonRepository.UpdateOneAsync(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError("AlarmBalanceUpdateAsync error:" + ex);
            return false;
        }
    }

    public async Task<AlarmBalanceConfigDto> AlarmBalanceGetAsync(string accountCode, string currencycode)
    {
        try
        {
            var dto = await _commonRepository.GetOneAsync<AlarmBalanceConfig>(x => x.AccountCode == accountCode);
            return dto?.ConvertTo<AlarmBalanceConfigDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError("AlarmBalanceUpdateAsync error:" + ex);
            return null;
        }
    }

    public async Task<MessagePagedResponseBase> GetListAlarmBalanceGetAsync(GetAllAlarmBalanceRequest request)
    {
        Expression<Func<AlarmBalanceConfig, bool>> query = p => true;
        if (!string.IsNullOrEmpty(request.AccountCode))
        {
            Expression<Func<AlarmBalanceConfig, bool>> newQuery = p =>
                p.AccountCode == request.AccountCode;
            query = query.And(newQuery);
        }

        if (!string.IsNullOrEmpty(request.CurrencyCode))
        {
            Expression<Func<AlarmBalanceConfig, bool>> newQuery = p =>
                p.CurrencyCode == request.CurrencyCode;
            query = query.And(newQuery);
        }

        var total = await _commonRepository.CountAsync(query);
        var lst = await _commonRepository.GetSortedPaginatedAsync<AlarmBalanceConfig, Guid>(query,
            s => s.CreatedDate, false,
            request.Offset, request.Limit);
        foreach (var item in lst) item.CreatedDate = _dateHepper.ConvertToUserTime(item.CreatedDate, DateTimeKind.Utc);
        return new MessagePagedResponseBase
        {
            ResponseCode = ResponseCodeConst.Success,
            ResponseMessage = "Thành công",
            Total = (int)total,
            Payload = lst.ConvertTo<List<AlarmBalanceConfigDto>>()
        };
    }
}