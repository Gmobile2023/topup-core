using System;
using System.Threading.Tasks;
using Topup.Commission.Model.Dtos;
using Topup.Shared;
using Topup.Shared.AbpConnector;
using Topup.Shared.ConfigDtos;
using Topup.Shared.Helpers;
using MassTransit;
using Microsoft.Extensions.Logging;
using Topup.Contracts.Commands.Commissions;
using Topup.Contracts.Commands.Commons;
using Topup.Contracts.Requests.Commons;
using Topup.Discovery.Requests.Balance;
using Topup.Discovery.Requests.Reports;
using ServiceStack;
using Topup.Commission.Domain.Entities;
using Topup.Commission.Domain.Repositories;

namespace Topup.Commission.Domain.Services;

public class CommissionService : ICommissionService
{
    private readonly IBus _bus;
    private readonly ICommissionRepository _commissionRepository;
    private readonly ExternalServiceConnector _externalServiceConnector;
    //private readonly IServiceGateway _gateway; gunner
    private readonly ILogger<CommissionService> _logger;
            private readonly GrpcClientHepper _grpcClient;

    public CommissionService(ICommissionRepository commissionRepository, ILogger<CommissionService> logger,
        ExternalServiceConnector externalServiceConnector,
        IBus bus, GrpcClientHepper grpcClient)
    {
        _commissionRepository = commissionRepository;
        _logger = logger;
        _externalServiceConnector = externalServiceConnector;
        _bus = bus;
        _grpcClient = grpcClient;
        //_gateway = HostContext.AppHost.GetServiceGateway(); gunner
    }

    public async Task<CommissionTransactionDto> CommissionInsertAsync(CommissionTransactionDto item)
    {
        try
        {
            var comm = item.ConvertTo<CommissionTransaction>();
            var commDb = await _commissionRepository.CommissionInsertAsync(comm);
            return commDb?.ConvertTo<CommissionTransactionDto>();
        }
        catch (Exception e)
        {
            _logger.LogError($"CommissionInsertAsync error:{e}");
            return null;
        }
    }

    public async Task<bool> CommissionUpdateAsync(CommissionTransactionDto item)
    {
        try
        {
            var comm = item.ConvertTo<CommissionTransaction>();
            return await _commissionRepository.CommissionUpdateAsync(comm);
        }
        catch (Exception e)
        {
            _logger.LogError($"CommissionInsertAsync error:{e}");
            return false;
        }
    }

    public async Task<NewMessageResponseBase<object>> CommissionRequest(CommissionTransactionCommand request)
    {
        try
        {
            _logger.LogInformation($"CommissionRequest:{request.ToJson()}");
            var check = await GetCommissionByRef(request.TransRef);
            if (check != null)
            {
                _logger.LogWarning($"{request.TransRef} commission exsit");
                return new NewMessageResponseBase<object>
                {
                    ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error,
                        "Giao dịch đã được ghi nhận hoa hồng")
                };
            }

            if (string.IsNullOrEmpty(request.ParentCode))
                return new NewMessageResponseBase<object>
                {
                    ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error,
                        "Giao dịch không hợp lệ. Không thể ghi nhận hoa hồng")
                };
            if (request.ParentCode == request.PartnerCode)
                return new NewMessageResponseBase<object>
                {
                    ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error,
                        "Tài khoản không hợp lệ")
                };

            ProductDiscountDto discountParent;
            if (request.ServiceCode == ServiceCodes.PAY_BILL)
                discountParent = await _externalServiceConnector.CheckProductDiscount(request.TransRef,request.ParentCode,
                    request.ProductCode, request.Amount);
            else
                discountParent = await _externalServiceConnector.CheckProductDiscount(request.TransRef,request.ParentCode,
                    request.ProductCode, 0, request.Quantity);

            if (discountParent == null)
            {
                _logger.LogWarning($"{request.TransRef} Cannot get discount parentAccount");
                return new NewMessageResponseBase<object>
                {
                    ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error,
                        "Giao dịch không thành công")
                };
            }

            if (discountParent.DiscountAmount <= 0)
            {
                _logger.LogWarning($"{request.TransRef} Discount parent not valid");
                return new NewMessageResponseBase<object>
                {
                    ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error,
                        "Thông tin chiết khấu tài khoản tổng không hợp lệ")
                };
            }

            var commAmount = discountParent.DiscountAmount - request.DiscountAmount;
            if (commAmount <= 0)
            {
                _logger.LogWarning($"{request.TransRef} Commission value not valid");
                return new NewMessageResponseBase<object>
                {
                    ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error,
                        "Thông tin hoa hồng không hợp lệ")
                };
            }

            var comm = request.ConvertTo<CommissionTransactionDto>();
            comm.CommissionAmount = commAmount;
            comm.PaymentDate = DateTime.UtcNow;
            comm.ParentDiscountAmount = discountParent.DiscountAmount;
            var commRequest = await CommissionInsertAsync(comm);
            if (commRequest != null)
            {
                _logger.LogInformation($"{request.TransRef}-Process pay commision");
                var response = await _grpcClient.GetClientCluster(GrpcServiceName.Balance).SendAsync(new BalancePayCommissionRequest
                {
                    AccountCode = request.ParentCode,
                    CurrencyCode = CurrencyCode.VND.ToString("G"),
                    TransRef = commRequest.TransCode,
                    Amount = commAmount,
                    TransNote = $"Thanh toán hoa hồng cho GD: {request.TransRef}"
                });
                _logger.LogInformation(
                    $"{request.TransRef}-{commRequest.TransCode}-PayComm return:{response.ToJson()}");
                if (response.ResponseStatus.ErrorCode == ResponseCodeConst.Success)
                {
                    _logger.LogInformation($"{request.TransRef}-{commRequest.TransCode}-PayComm success");
                    commRequest.Status = CommissionTransactionStatus.Success;
                    var updateComm = await CommissionUpdateStatusAsync(commRequest.TransCode,
                        CommissionTransactionStatus.Success);
                    _logger.LogInformation(
                        $"{request.TransRef}-{commRequest.TransCode}-Update status comm:{updateComm}");
                    await _bus.Publish<CommissionReportCommand>(new
                    {
                        TransCode = comm.TransRef,
                        comm.ParentCode,
                        CommissionAmount = commAmount,
                        CommissionDate = DateTime.Now,
                        Status = 1,
                        CorrelationId = Guid.NewGuid()
                    });
                    //send notifi
                    await SendNotifi(commRequest);
                    return new NewMessageResponseBase<object>
                    {
                        ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Success,
                            "Ghi nhận hoa hồng thành công")
                    };
                }

                await _bus.Publish<CommissionReportCommand>(new
                {
                    TransCode = comm.TransRef,
                    comm.ParentCode,
                    CommissionAmount = commAmount,
                    Status = 0,
                    CorrelationId = Guid.NewGuid()
                });


                _logger.LogInformation($"{request.TransRef}-Cannot pay commission");
                await SendTeleMessage(new SendTeleTrasactionRequest
                {
                    BotType = BotType.Sale,
                    TransRef = request.TransRef,
                    Title = "Không thể thanh toán hoa hồng cho giao dịch",
                    Message = $"GD {request.TransRef}\n" +
                              $"Ref: {commRequest.TransCode}\n" +
                              $"TK thực hiện: {request.PartnerCode}\n" +
                              $"TK: {request.ParentCode}\n" +
                              $"HH: {commAmount.ToFormat("đ")}",
                    BotMessageType = BotMessageType.Error
                });
                return new NewMessageResponseBase<object>
                {
                    ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error,
                        "Không thể thanh toán hoa hồng cho giao dịch")
                };
            }

            _logger.LogInformation($"{request.TransRef}-Cannot insert commission");
            await SendTeleMessage(new SendTeleTrasactionRequest
            {
                BotType = BotType.Sale,
                TransRef = request.TransRef,
                Title = "Ghi nhận HH cho giao dịch không thành công",
                Message = $"GD {request.TransRef}\n" +
                          $"TK thực hiện: {request.PartnerCode}\n" +
                          $"TK: {request.ParentCode}\n" +
                          $"HH: {commAmount.ToFormat("đ")}",
                BotMessageType = BotMessageType.Error
            });
            return new NewMessageResponseBase<object>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error,
                    "Ghi nhận hoa hồng không thành công")
            };
        }
        catch (Exception e)
        {
            _logger.LogError($"{request.TransRef}-CommissionRequest error:{e}");
            await SendTeleMessage(new SendTeleTrasactionRequest
            {
                BotType = BotType.Dev,
                TransRef = request.TransRef,
                Title = "Có lỗi ghi nhận HH cho giao dịch không thành công",
                Message = $"GD {request.TransRef}\n" +
                          $"TK thực hiện: {request.PartnerCode}\n" +
                          $"TK: {request.ParentCode}\n" +
                          $"Error: {e.Message}",
                BotMessageType = BotMessageType.Error
            });
            return new NewMessageResponseBase<object>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error,
                    "Ghi nhận hoa hồng không thành công")
            };
        }
    }

    public async Task<bool> CommissionUpdateStatusAsync(string transcode, CommissionTransactionStatus status)
    {
        return await _commissionRepository.CommissionUpdateStatusAsync(transcode, status);
    }

    public async Task<CommissionTransactionDto> GetCommissionByRef(string transRef)
    {
        var item = await _commissionRepository.GetCommissionByRef(transRef);
        return item?.ConvertTo<CommissionTransactionDto>();
    }


    private async Task SendTeleMessage(SendTeleTrasactionRequest request)
    {
        try
        {
            await _bus.Publish<SendBotMessage>(new
            {
                MessageType = request.BotMessageType ?? BotMessageType.Wraning,
                BotType = request.BotType ?? BotType.Sale,
                Module = "Commission",
                request.Title,
                request.Message,
                TimeStamp = DateTime.Now,
                CorrelationId = Guid.NewGuid()
            });
        }
        catch (Exception ex)
        {
            _logger.LogInformation($"SendTeleMessage : {ex}");
        }
    }

    private async Task SendNotifi(CommissionTransactionDto request)
    {
        try
        {
            _logger.LogInformation($"SendNotifiCommission:{request.TransCode}-{request.TransRef}");
            var account = await _grpcClient.GetClientCluster(GrpcServiceName.Report).SendAsync(new ReportGetAccountInfoRequest
            {
                AccountCode = request.PartnerCode
            });
            if (account.ResponseStatus.ErrorCode == ResponseCodeConst.Success)
            {
                var message =
                    $"Chúc mừng bạn đã nhận được hoa hồng đại lý tổng từ {request.PartnerCode} - {account.Results.FullName}. Mã giao dịch: {request.TransCode}, số tiền: {request.CommissionAmount.ToFormat("đ")} lúc {DateTime.Now:dd/MM/yyyy hh:mm:ss}";
                await _bus.Publish<NotificationSendCommand>(new
                {
                    Body = message,
                    Data = new
                    {
                        request.Amount, request.TransCode, request.TransRef, request.ParentCode, request.PartnerCode
                    }.ToJson(),
                    Title = "Trả thưởng hoa hồng đại lý tổng",
                    ReceivingAccount = request.ParentCode,
                    AppNotificationName = "App.PayCommission",
                    TimeStamp = DateTime.Now,
                    CorrelationId = Guid.NewGuid()
                });
            }
        }
        catch (Exception e)
        {
            _logger.LogError($"SendNotifiCommission error:{e}-{request.TransCode}-{request.TransRef}");
        }
    }
}