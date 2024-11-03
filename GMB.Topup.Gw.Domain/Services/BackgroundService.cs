using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GMB.Topup.Contracts.Commands.Commons;
using GMB.Topup.Contracts.Requests.Commons;
using GMB.Topup.Gw.Model.Dtos;
using GMB.Topup.Gw.Model.Events;
using GMB.Topup.Shared;
using GMB.Topup.Shared.CacheManager;
using GMB.Topup.Shared.ConfigDtos;
using GMB.Topup.Shared.Dtos;
using GMB.Topup.Shared.Helpers;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver.Core.WireProtocol.Messages;


using GMB.Topup.Discovery.Requests.TopupGateways;
using GMB.Topup.Discovery.Requests.Workers;
using ServiceStack;
using ServiceStack.Model;

namespace GMB.Topup.Gw.Domain.Services
{
    public class BackgroundService : IBackgroundService
    {
        private readonly ISaleService _saleService;

        private readonly ISystemService _systemService;

        //private readonly IServiceGateway _gateway; gunner
        private readonly ILogger<BackgroundService> _logger;
        private readonly IBus _bus;
        private readonly IConfiguration _configuration;
        private static bool _inProcess;
        private static bool _inProcessGate;
        private static List<string> _transCodeSlows;
        private static List<string> _transCodeGates;
        private readonly ICacheManager _cacheManager;
        private readonly IDateTimeHelper _dateTimeHelper;
        private readonly GrpcClientHepper _grpcClient;

        public BackgroundService(ISaleService saleService, ILogger<BackgroundService> logger, IBus bus,
            IConfiguration configuration, ISystemService systemService, ICacheManager cacheManager,
            IDateTimeHelper dateTimeHelper, GrpcClientHepper grpcClient)
        {
            _saleService = saleService;
            _logger = logger;
            _bus = bus;
            _configuration = configuration;
            _systemService = systemService;
            _cacheManager = cacheManager;
            _dateTimeHelper = dateTimeHelper;
            _grpcClient = grpcClient;
            //_gateway = HostContext.AppHost.GetServiceGateway(); gunner
        }

        public async Task AutoCheckTrans()
        {
            try
            {
                var lstPending = new List<SaleRequestDto>();
                var lstPendingSlow = new List<SaleRequestDto>();
                var config = new BackendHangFireConfig();
                _configuration.GetSection("Hangfire").Bind(config);
                _logger.LogInformation($"Process check trans with time pending:{config.AutoCheckTrans.TimePending}");
                _transCodeSlows ??= new List<string>();

                var saleToCheck = await _saleService.GetSaleRequestPending(config.AutoCheckTrans.TimePending);

                if (saleToCheck != null && saleToCheck.Any() && _inProcess == false)
                {
                    if (saleToCheck.Count <= config.AutoCheckTrans.MaxTransProcess)
                    {
                        _inProcess = true;

                        #region process

                        foreach (var item in saleToCheck.Where(item => !_transCodeSlows.Contains(item.TransCode))
                                     .Where(item =>
                                         item.CreatedTime.AddMinutes(config.AutoCheckTrans.TimePending) <=
                                         DateTime.UtcNow))
                        {
                            _logger.LogInformation(
                                $"Process auto check:{item.TransCode}-{item.TransRef}-{item.Provider}-{item.SaleType:G}");
                            var checkTrans = await CheckTrans(item.ProviderTransCode, item.Provider,
                                item.ServiceCode, partnerCode: item.PartnerCode, value: item.Price);
                            var code = checkTrans.ResponseStatus.ErrorCode;
                            _logger.LogInformation(
                                $"Auto check Status return:{item.TransCode}-{item.TransRef}-{item.Provider}-{item.SaleType:G}-{checkTrans.ToJson()}");
                            if (!config.AutoCheckTrans.IsProcess) continue;
                            switch (checkTrans.ResponseStatus.ErrorCode)
                            {
                                case ResponseCodeConst.ResponseCode_WaitForResult:
                                case ResponseCodeConst.ResponseCode_TimeOut:
                                    {
                                        var time = (DateTime.UtcNow - item.CreatedTime).TotalMinutes;
                                        _logger.LogInformation($"{item.TransCode}-{time}");
                                        var configTime = item.SaleType == SaleType.Slow
                                            ? config.AutoCheckTrans.TimePendingWarningSlow
                                            : config.AutoCheckTrans.TimePendingWarning;
                                        if ((DateTime.UtcNow - item.CreatedTime).TotalMinutes >= configTime)
                                        {
                                            if (item.SaleType == SaleType.Slow)
                                                lstPendingSlow.Add(item);
                                            else
                                                lstPending.Add(item);
                                        }

                                        break;
                                    }
                                case ResponseCodeConst.Success:
                                    {
                                        _logger.LogInformation(
                                            $"Auto check Status Success:{item.TransCode}-{item.TransRef}-{item.Provider}-{item.SaleType:G}");
                                        item.Status = SaleRequestStatus.Success;
                                        item.ReceiverTypeResponse = checkTrans.Results.ReceiverType;
                                        await _saleService.SaleRequestUpdateStatusAsync(item.TransCode, null,
                                            SaleRequestStatus.Success, isBackend: true,
                                            receiverType: checkTrans.Results.ReceiverType,
                                            providerResponseTransCode: checkTrans.Results.ProviderResponseTransCode);
                                        // await _bus.Publish(new ReportTransStatusMessage()
                                        // {
                                        //     TransCode = item.TransCode,
                                        //     Status = 1,
                                        //     ReceiverTypeResponse = checkTrans.Results.ReceiverType//CHỗ này ghi nhận thêm report
                                        // });
                                        var title = "Checktrans tự động - GD Thành công";
                                        if (!string.IsNullOrEmpty(item.ParentCode) &&
                                            item.AgentType == AgentType.SubAgent)
                                        {
                                            title =
                                                $"Checktrans tự động - GD Thành công - Cộng tiền HH cho Đại lý: {item.ParentCode}";
                                            await _saleService.CommissionRequest(item);
                                        }

                                        if (config.AutoCheckTrans.IsSendTele)
                                        {
                                            await SendTeleMessage(new SendTeleTrasactionRequest
                                            {
                                                BotType = BotType.Sale,
                                                TransRef = item.TransRef,
                                                TransCode = item.TransCode,
                                                Title = title,
                                                Message =
                                                    $"Mã GD {item.TransCode}" +
                                                    $"\nRef: {item.TransRef}" +
                                                    $"\nMã TK: {item.PartnerCode}" +
                                                    $"\nMã NCC: {item.Provider}" +
                                                    $"\nSố tiền: {item.Amount.ToFormat("đ")}" +
                                                    $"\nSĐT: {item.ReceiverInfo}" +
                                                    $"\nLoại giao dịch: {item.SaleType:G}" +
                                                    $"\nVui lòng kiểm tra lại thông tin",
                                                BotMessageType = BotMessageType.Message
                                            });
                                        }

                                        if (config.AutoCheckTrans.PartnerCodeOffset == item.ParentCode)
                                            await UpdateOriginOffsetSuccess(item.TransRef);
                                        break;
                                    }
                                default:
                                    {
                                        if (code != ResponseCodeConst.Success
                                            && code != ResponseCodeConst.ResponseCode_WaitForResult
                                            && code != ResponseCodeConst.ResponseCode_TimeOut
                                            && code != ResponseCodeConst.ResponseCode_Paid
                                            && code != ResponseCodeConst.ResponseCode_InProcessing
                                           ) //Nếu gd nạp chậm mà lỗi thì k xử lý gì
                                        {
                                            _logger.LogInformation(
                                                $"Auto check Status Error:{item.TransCode}-{item.TransRef}-{item.Provider}-{item.SaleType.ToString("G")}");
                                            if (item.SaleType != SaleType.Slow)
                                            {
                                                item.Status = SaleRequestStatus.Canceled;
                                                var refund = await _saleService.RefundTransaction(item.TransCode);
                                                _logger.LogInformation(
                                                    $"Auto check refund:{item.TransCode}-{item.TransRef}-{item.Provider}-{refund.ToJson()}");
                                                if (config.AutoCheckTrans.IsSendTele)
                                                {
                                                    var text = refund.ResponseStatus.ErrorCode ==
                                                               ResponseCodeConst.Success
                                                        ? ($"Hoàn tiền thành công: {refund.Results.TransactionCode}")
                                                        : ($"Không thể hoàn tiền cho KH");
                                                    if (refund.ResponseStatus.ErrorCode == ResponseCodeConst.Success &&
                                                        config.AutoCheckTrans.IsSendTele)
                                                    {
                                                        await SendTeleMessage(new SendTeleTrasactionRequest
                                                        {
                                                            BotType = BotType.Sale,
                                                            TransRef = item.TransRef,
                                                            TransCode = item.TransCode,
                                                            Title = "Checktrans tự động - Kết quả GD Lỗi",
                                                            Message =
                                                                $"Mã GD {item.TransCode}" +
                                                                $"\nRef: {item.TransRef}" +
                                                                $"\nMã TK: {item.PartnerCode}" +
                                                                $"\nMã NCC: {item.Provider}" +
                                                                $"\nSố tiền: {item.Amount.ToFormat("đ")}" +
                                                                $"\nSĐT: {item.ReceiverInfo}" +
                                                                $"\nMessage: {checkTrans.ResponseStatus.ToJson()}" +
                                                                $"\nMessage NCC: {checkTrans.Results.ToJson()}" +
                                                                $"\n{text}" +
                                                                $"\nVui lòng kiểm tra lại thông tin",
                                                            BotMessageType =
                                                                refund.ResponseStatus.ErrorCode ==
                                                                ResponseCodeConst.Success
                                                                    ? BotMessageType.Message
                                                                    : BotMessageType.Error
                                                        });
                                                    }
                                                    else if (refund.ResponseStatus.ErrorCode !=
                                                             ResponseCodeConst.Success)
                                                    {
                                                        item.Status =
                                                            SaleRequestStatus
                                                                .Undefined; //ghi nhận tạm lại check sau sao k hoàn dc
                                                        await _saleService.SaleRequestUpdateStatusAsync(item.TransCode,
                                                            null, SaleRequestStatus.Undefined);
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                if (config.AutoCheckTrans.IsOffset)
                                                {
                                                    var saleOffset =
                                                        await _saleService.SaleOffsetOriginGetAsync(item.TransCode,
                                                            false);
                                                    if (saleOffset == null)
                                                    {
                                                        await SendOffsetProcess(config, item);
                                                        _transCodeSlows.Add(item.TransCode);
                                                    }
                                                }

                                                if ((DateTime.UtcNow - item.CreatedTime).TotalMinutes >=
                                                    config.AutoCheckTrans.TimePendingWarningSlow)
                                                {
                                                    lstPendingSlow.Add(item);
                                                }

                                                if (config.AutoCheckTrans.IsSendTeleSlowTrans)
                                                {
                                                    await SendTeleMessage(new SendTeleTrasactionRequest
                                                    {
                                                        BotType = BotType.Sale,
                                                        TransRef = item.TransRef,
                                                        TransCode = item.TransCode,
                                                        Title =
                                                            "Checktrans tự động - Giao dịch Nạp Chậm - Kết quả GD Lỗi",
                                                        Message =
                                                            $"GD: {item.TransCode}" +
                                                            $"\nRef: {item.TransRef}" +
                                                            $"\nMã TK: {item.PartnerCode}" +
                                                            $"\nMã NCC: {item.Provider}" +
                                                            $"\nSố tiền: {item.Amount.ToFormat("đ")}" +
                                                            $"\nSĐT: {item.ReceiverInfo}" +
                                                            $"\nMessage: {checkTrans.ResponseStatus.ToJson()}" +
                                                            $"\nMessage NCC: {checkTrans.Results.ToJson()}" +
                                                            $"\nHệ thống không tự động hoàn tiền. Vui lòng xử lý kết luận bằng tay",
                                                        BotMessageType = BotMessageType.Wraning
                                                    });
                                                }
                                            }
                                        }

                                        break;
                                    }
                            }
                        }

                        #endregion

                        if (lstPending.Any() && config.AutoCheckTrans.IsSendTeleWarning)
                        {
                            var total = lstPending.Count;
                            var message = total <= 20
                                ? string.Join(", ", lstPending.Select(x => x.TransCode).ToArray())
                                : $"Tổng GD chưa có kết quả là {total} gd. Vui lòng check thông tin chi tiết trong QLGD";
                            await SendTeleMessage(new SendTeleTrasactionRequest
                            {
                                BotType = BotType.Sale,
                                Title =
                                    $"Giao dịch quá {config.AutoCheckTrans.TimePendingWarning}p chưa có KQ. Vui lòng check",
                                Message = message,
                                BotMessageType = BotMessageType.Wraning
                            });
                        }

                        if (lstPendingSlow.Any() && config.AutoCheckTrans.IsSendTeleWarningSlow)
                        {
                            var message = string.Join(", ", lstPendingSlow.Select(x => x.TransCode).ToArray());
                            await SendTeleMessage(new SendTeleTrasactionRequest
                            {
                                BotType = BotType.Sale,
                                Title =
                                    $"Giao dịch NẠP CHẬM quá {config.AutoCheckTrans.TimePendingWarningSlow}p chưa xử lý. Vui lòng check",
                                Message = message,
                                BotMessageType = BotMessageType.Wraning
                            });
                        }

                        _inProcess = false;
                    }
                    else
                    {
                        await SendTeleMessage(new SendTeleTrasactionRequest
                        {
                            BotType = BotType.Sale,
                            Title = $"Giao dịch Chưa có KQ vượt quá ngưỡng xử lý. Còn tồn {saleToCheck.Count} GD",
                            Message = "Vui lòng kiểm tra hệ thống. Đang có nhiều GD chưa có KQ",
                            BotMessageType = BotMessageType.Wraning
                        });
                    }
                }
                else
                {
                    _logger.LogInformation($"No pending transactions");
                }
            }
            catch (Exception e)
            {
                _logger.LogError($"AutoCheckTrans error:{e.Message}");
            }
        }

        public async Task CheckLastTrans()
        {
            try
            {
                _logger.LogInformation("Auto CheckLastTrans");
                var partners = await _systemService.GetListPartnerCheckLastTrans();

                if (partners.Any())
                {
                    Parallel.ForEach(partners, async item => { await DoWorkCheckLastTrans(item); });
                }
                else
                {
                    _logger.LogInformation($"No CheckLastTrans");
                }
            }
            catch (Exception e)
            {
                _logger.LogError($"CheckLastTrans error:{e}");
                Console.WriteLine(e);
            }
        }


        // public async Task CheckAutoCloseProvider()
        // {
        //     var providers = await _cacheManager.GetAllEntityByPattern<ProviderInfoDto>("PayGate_ProviderInfo:Items:*");
        //
        //     foreach (var provider in providers)
        //     {
        //         //_ = Task.Run(() =>
        //         //{
        //             CheckAutoClose(provider);
        //         //});
        //
        //     }
        // }

        // private async Task CheckAutoClose(ProviderInfoDto provider)
        // {
        //     try
        //     {
        //         _logger.LogInformation($"CheckAutoClose {provider.ProviderCode} - TimeScan: {provider.TimeScan} TotalTransScan: {provider.TotalTransScan}  TotalTransError: {provider.TotalTransErrorScan} TotalTimeout: {provider.TotalTransDubious}");
        //
        //         if (provider.TotalTransErrorScan > 0 || provider.TotalTransDubious > 0)
        //         {
        //             Expression<Func<TopupRequestLog, bool>> query = p => p.ProviderCode == provider.ProviderCode;
        //
        //             
        //
        //
        //             var items = await _transRepository.GetSortedPaginatedAsync<TopupRequestLog, Guid>(
        //                               query,
        //                               s => s.TransCode, false, 0, provider.TotalTransScan);
        //             _logger.LogInformation($"CheckAutoClose {provider.ProviderCode} Total Items {items.Count}");
        //
        //             items = items.Where(p => p.AddedAtUtc > DateTime.UtcNow.AddMinutes(-1 * provider.TimeScan)).ToList();
        //             var lastLock = await _cacheManager.GetEntity<DateTime?>($"PayGate:BackgroundJob:AutoCloseProvider:LastLock:{provider.ProviderCode}");
        //             if (lastLock != null)
        //             {
        //                 Console.WriteLine(lastLock);
        //                 items = items.Where(p => p.AddedAtUtc > lastLock).ToList();
        //             }
        //             var totalError = items.Count(p => p.Status == TopupGw.Contacts.Enums.TransRequestStatus.Fail);
        //             var totalTimeout = items.Count(p => p.Status == TopupGw.Contacts.Enums.TransRequestStatus.Timeout);
        //
        //             _logger.LogInformation($"CheckAutoClose {provider.ProviderCode} ==>{totalError} {totalTimeout}");
        //             if ((totalError > provider.TotalTransErrorScan && provider.TotalTransErrorScan > 0) || (totalTimeout > provider.TotalTransDubious && provider.TotalTransDubious > 0))
        //             {
        //                 _logger.LogInformation($"SetAutoClose:{provider.ProviderCode}");
        //                 //Lock provider
        //                 await _bus.Publish<LockProviderCommand>(new
        //                 {
        //                     CorrelationId = Guid.NewGuid(),
        //                     provider.ProviderCode,
        //                     provider.TimeClose
        //                 });
        //                 //ResetAuto
        //               //  await _transCodeGenerator.ResetAutoCloseIndex(provider.ProviderCode);
        //                 await _cacheManager.AddEntity<DateTime?>($"PayGate:BackgroundJob:AutoCloseProvider:LastLock:{provider.ProviderCode}", DateTime.UtcNow);
        //                 await _bus.Publish<SendBotMessage>(new
        //                 {
        //                     MessageType = BotMessageType.Wraning,
        //                     BotType = BotType.Channel,
        //                     Module = "TopupGw",
        //                     Title =
        //                         $"Kênh:{provider.ProviderCode} đóng tự động",
        //                     Message =
        //                         $"NCC {provider.ProviderCode} sẽ đóng tự động.\n" +
        //                         $"Số lượng GD không thành công :{totalError}\n" +
        //                           $"Số lượng GD Timeout :{totalTimeout}\n" +
        //                         $"Thời gian đóng:{provider.TimeClose} phút",
        //                     TimeStamp = DateTime.Now,
        //                     CorrelationId = Guid.NewGuid()
        //                 });
        //             }
        //         }
        //
        //
        //     }
        //     catch (Exception e)
        //     {
        //         _logger.LogError(e, $"CheckAutoClose {provider.ProviderCode} - {e.Message}");
        //
        //     }
        //
        //
        //
        //
        // }
        private async Task<NewMessageResponseBase<ResponseProvider>> CheckTrans(string transcode, string provider,
            string serviceCode, string partnerCode = "", decimal value = 0)
        {
            try
            {
                var response = await _grpcClient.GetClientCluster(GrpcServiceName.TopupGateway).SendAsync(
                    new GateCheckTransRequest
                    {
                        ProviderCode = provider,
                        ServiceCode = serviceCode,
                        TransCodeToCheck = transcode
                    });

                if (response.ResponseStatus.ErrorCode == ResponseCodeConst.Success
                    && serviceCode is ServiceCodes.PIN_CODE or ServiceCodes.PIN_DATA or ServiceCodes.PIN_GAME
                    && !string.IsNullOrEmpty(response.Results.PayLoad))
                {
                    try
                    {
                        var check = await _saleService.CheckSaleItemAsync(transcode);
                        if (check == false)
                        {
                            var cards = response.Results.PayLoad.FromJson<List<CardRequestResponseDto>>();
                            var lstTopupItem = (from item in cards
                                                select new SaleItemDto
                                                {
                                                    Amount = Convert.ToInt32(value),
                                                    Serial = item.Serial,
                                                    CardExpiredDate = item.ExpiredDate,
                                                    Status = SaleRequestStatus.Success,
                                                    CardValue = Convert.ToInt32(value),
                                                    CardCode = item.CardCode,
                                                    ServiceCode = serviceCode,
                                                    PartnerCode = partnerCode,
                                                    SaleType = "PINCODE",
                                                    SaleTransCode = transcode,
                                                    CreatedTime = DateTime.Now
                                                }).ToList();
                            await _saleService.SaleItemListCreateAsync(lstTopupItem);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Insert detail card error:{ex}");
                    }
                }

                return response;
            }
            catch (Exception e)
            {
                _logger.LogError($"CheckTrans error:{e.Message}");
                return new NewMessageResponseBase<ResponseProvider>()
                {
                    ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_WaitForResult)
                };
            }
        }

        private async Task SendTeleMessage(SendTeleTrasactionRequest request)
        {
            try
            {
                await _bus.Publish<SendBotMessage>(new
                {
                    MessageType = request.BotMessageType ?? BotMessageType.Wraning,
                    BotType = request.BotType ?? BotType.Dev,
                    Module = "Backend",
                    Title = request.Title,
                    Message = request.Message,
                    TimeStamp = DateTime.Now,
                    CorrelationId = Guid.NewGuid(),
                });
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"SendTeleMessage : {ex}");
            }
        }

        private async Task SendTeleoGroupMessage(BotMessageType type, string alarmTeleChatId, string title, string message)
        {
            try
            {
                await _bus.Publish<SendBotMessageToGroup>(new
                {
                    MessageType = type,
                    BotType = BotType.Private,
                    ChatId = alarmTeleChatId,
                    Module = "BackEnd",
                    Title = title,
                    Message = message,
                    TimeStamp = DateTime.Now,
                    CorrelationId = Guid.NewGuid()
                });

            }
            catch (Exception ex)
            {
                _logger.LogInformation($"SendTeleMessage : {ex}");
            }
        }


        private Task SendOffsetProcess(BackendHangFireConfig tempConfig, SaleRequestDto dtoTemp)
        {
            Task.Factory.StartNew(async () =>
            {
                var config = tempConfig;
                var dto = dtoTemp;
                try
                {
                    var saleOffsetDto = _saleService.SaleOffsetOriginGetAsync(dto.TransCode, false).Result;
                    if (saleOffsetDto == null)
                        saleOffsetDto = _saleService.SaleOffsetCreateAsync(dto, config.AutoCheckTrans.PartnerCodeOffset)
                            .Result;
                    else return;

                    var request = dto.ConvertTo<WorkerTopupRequest>();
                    request.RequestDate = DateTime.Now;
                    request.StaffAccount = config.AutoCheckTrans.PartnerCodeOffset;
                    //request.Channel = Channel.API;
                    request.AccountType = SystemAccountType.MasterAgent;
                    //request.RequestIp = Request.UserHostAddress;
                    request.PartnerCode = config.AutoCheckTrans.PartnerCodeOffset;
                    request.TransCode = saleOffsetDto.TransRef;
                    request.RequestDate = DateTime.Now;
                    var getApi = _grpcClient.GetClientCluster(GrpcServiceName.Worker).SendAsync(request).Result;
                    _logger.LogError(
                        $"{dto.TransRef}|{dto.TransCode} SendOffsetProcess SendAsync reponse : {getApi.ToJson()}");
                    var returnMessage = new NewMessageResponseBase<object>
                    {
                        ResponseStatus = getApi.ResponseStatus
                    };
                    returnMessage.ResponseStatus.TransCode = request.TransCode;
                    saleOffsetDto.TransCode = request.TransCode;
                    switch (returnMessage.ResponseStatus.ErrorCode)
                    {
                        case ResponseCodeConst.Success:
                            saleOffsetDto.Status = SaleRequestStatus.Success;
                            dto.Status = SaleRequestStatus.Success;
                            await _saleService.SaleRequestUpdateStatusAsync(dto.TransCode, null,
                                SaleRequestStatus.Success);
                            await _bus.Publish(new ReportTransStatusMessage()
                            {
                                TransCode = saleOffsetDto.TransCode,
                                Status = 1
                            });
                            _transCodeSlows.Remove(dto.TransCode);
                            await SendTeleMessage(new SendTeleTrasactionRequest
                            {
                                BotType = BotType.Sale,
                                TransRef = saleOffsetDto.TransCode,
                                TransCode = dtoTemp.TransCode,
                                Title = $"Nạp bù cho giao dịch {dtoTemp.TransCode} thành công",
                                Message =
                                    $"Mã GD gốc {dtoTemp.TransCode}" +
                                    $"\nGD bù: {saleOffsetDto.TransCode}" +
                                    $"\nMã TK: {dtoTemp.PartnerCode}" +
                                    $"\nSố tiền: {dtoTemp.Amount.ToFormat("đ")}" +
                                    $"\nSĐT: {dtoTemp.ReceiverInfo}" +
                                    $"\nVui lòng kiểm tra lại thông tin",
                                BotMessageType = BotMessageType.Message
                            });
                            break;
                        case ResponseCodeConst.ResponseCode_WaitForResult:
                        case ResponseCodeConst.ResponseCode_TimeOut:
                        case ResponseCodeConst.ResponseCode_Paid:
                        case ResponseCodeConst.ResponseCode_InProcessing:
                            saleOffsetDto.Status = SaleRequestStatus.ProcessTimeout;
                            break;
                        default:
                            saleOffsetDto.Status = SaleRequestStatus.Failed;
                            _transCodeSlows.Remove(dto.TransCode);
                            await SendTeleMessage(new SendTeleTrasactionRequest
                            {
                                BotType = BotType.Sale,
                                TransRef = saleOffsetDto.TransCode,
                                TransCode = dtoTemp.TransCode,
                                Title = $"Nạp bù cho giao dịch {dtoTemp.TransCode} lỗi",
                                Message =
                                    $"Mã GD gốc {dtoTemp.TransCode}" +
                                    $"\nGD bù: {saleOffsetDto.TransCode}" +
                                    $"\nMã TK: {dtoTemp.PartnerCode}" +
                                    $"\nSố tiền: {dtoTemp.Amount.ToFormat("đ")}" +
                                    $"\nSĐT: {dtoTemp.ReceiverInfo}" +
                                    $"\nLỗi: {returnMessage.ToJson()}",
                                BotMessageType = BotMessageType.Wraning
                            });
                            break;
                    }

                    await _saleService.SaleOffsetUpdateAsync(saleOffsetDto);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"{dto.TransRef}|{dto.TransCode} SendOffsetProcess Exception: {ex}");
                }
            }).Wait();
            return Task.CompletedTask;
        }

        private Task UpdateOriginOffsetSuccess(string transCode)
        {
            Task.Factory.StartNew(async () =>
            {
                var code = transCode;
                try
                {
                    var saleOffsetDto = _saleService.SaleOffsetGetAsync(code).Result;
                    if (saleOffsetDto != null)
                    {
                        await _saleService.SaleRequestUpdateStatusAsync(saleOffsetDto.OriginTransCode, null,
                            SaleRequestStatus.Success);
                        await _bus.Publish(new ReportTransStatusMessage()
                        {
                            TransCode = saleOffsetDto.OriginTransCode,
                            Status = 1
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"{transCode} UpdateOriginOffsetSuccess Exception: {ex}");
                }
            }).Wait();

            return Task.CompletedTask;
        }

        private async Task DoWorkCheckLastTrans(PartnerConfigDto partner)
        {
            try
            {
                Console.WriteLine($"DoWorkCheckLastTrans:{partner.PartnerCode}-{partner.PartnerName}");
                var config = new BackendHangFireConfig();
                _configuration.GetSection("Hangfire").Bind(config);


                var key = $"PayGate_CheckLastTrans:Items:{partner.PartnerCode}";
                var lastSend = await _cacheManager.GetEntity<CheckLastransInfo>(key) ??
                               new CheckLastransInfo
                               {
                                   TotalSend = 0,
                                   LastTimeSend = DateTime.Now
                               };
                //Sau khoảng x phút. Nếu hết hạn thì reset lại CountSend. Bắt đầu cảnh báo lại. Khi đạt CountSend thì dừng gửi
                var totalMinute = (DateTime.Now - lastSend.LastTimeSend).TotalMinutes;
                if (totalMinute > config.CheckLastTrans.TimeResend)
                    lastSend.TotalSend = 0;
                if (lastSend.TotalSend < config.CheckLastTrans.CountResend)
                {
                    lastSend.TotalSend += 1;
                    lastSend.LastTimeSend = DateTime.Now;
                    var sale = await _saleService.GetLastSaleRequest(partner.PartnerCode);
                    if (sale != null && (DateTime.UtcNow - sale.CreatedTime).TotalMinutes >=
                        partner.LastTransTimeConfig)
                    {
                        var total = Math.Round((DateTime.UtcNow - sale.CreatedTime).TotalMinutes);
                        var spWorkMin = TimeSpan.FromMinutes(total);
                        var workHours = string.Format("{0} giờ {1} phút", spWorkMin.Hours, spWorkMin.Minutes);
                        _logger.LogWarning($"No transaction:{partner.PartnerCode}-{partner.PartnerName}-{total}");
                        var lastTransTime = _dateTimeHelper.ConvertToUserTime(sale.CreatedTime, DateTimeKind.Utc);
                        await SendTeleMessage(new SendTeleTrasactionRequest
                        {
                            BotType = BotType.Channel,
                            Title =
                                $"Đại lý : {partner.PartnerCode}-{partner.PartnerName} không phát sinh GD trong {workHours}",
                            Message =
                                $"Thời gian cấu hình: {partner.LastTransTimeConfig} phút" +
                                $"\nThời gian GD gần nhất: {lastTransTime:dd/MM/yyyy HH:mm:ss}" +
                                $"\nThời gian không phát sinh GD: {workHours}" +
                                $"\nMã GD:{sale.TransCode}" +
                                $"\nTrạng thái: {sale.Status.ToString("G")}" +
                                $"\nDịch vụ: {sale.ServiceCode}" +
                                $"\nNhắc lần: {lastSend.TotalSend}",
                            BotMessageType = BotMessageType.Wraning
                        });
                    }

                    await _cacheManager.AddEntity(key, lastSend, TimeSpan.FromDays(300));
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                _logger.LogError($"DoWorkCheckLastTrans:{e}");
            }
        }


        public async Task AutoCheckGateTrans()
        {
            try
            {
                var lstPending = new List<SaleGateRequestDto>();
                var config = new BackendHangFireConfig();
                _configuration.GetSection("Hangfire").Bind(config);
                _logger.LogInformation($"Process check trans with time pending:{config.AutoCheckTrans.TimePending}");
                _transCodeGates ??= new List<string>();

                var saleToCheck = await _saleService.GetSaleGateRequestPending(config.AutoCheckTrans.TimePending);

                if (saleToCheck != null && saleToCheck.Any() && _inProcessGate == false)
                {
                    var last = saleToCheck.FirstOrDefault();
                    if (saleToCheck.Count <= config.AutoCheckTrans.MaxTransProcess)
                    {
                        _inProcessGate = true;

                        #region process

                        foreach (var item in saleToCheck.Where(item => !_transCodeGates.Contains(item.TransCode))
                                     .Where(item =>
                                         item.CreatedDate.AddMinutes(config.AutoCheckTrans.TimePending) <=
                                         DateTime.UtcNow))
                        {
                            _logger.LogInformation(
                                $"Process auto check:{item.TransCode}-{item.TransCode}-{item.Provider}-");
                            var checkTrans = await CheckTrans(item.TransCode, item.Provider,
                                item.ServiceCode, partnerCode: "", value: item.TransAmount);
                            var code = checkTrans.ResponseStatus.ErrorCode;
                            _logger.LogInformation(
                                $"Auto check Status return:{item.TransCode}-{item.Provider}-{checkTrans.ToJson()}");
                            if (!config.AutoCheckTrans.IsProcess) continue;
                            switch (checkTrans.ResponseStatus.ErrorCode)
                            {
                                case ResponseCodeConst.ResponseCode_WaitForResult:
                                case ResponseCodeConst.ResponseCode_TimeOut:
                                    {
                                        var time = (DateTime.UtcNow - item.CreatedDate).TotalMinutes;
                                        _logger.LogInformation($"{item.TransCode}-{time}");
                                        var configTime = config.AutoCheckTrans.TimePendingWarning;
                                        if ((DateTime.UtcNow - item.CreatedDate).TotalMinutes >= configTime)
                                        {
                                            lstPending.Add(item);
                                        }
                                        break;
                                    }
                                case ResponseCodeConst.Success:
                                    {
                                        _logger.LogInformation(
                                            $"Auto check Status Success:{item.TransCode}-{item.Provider}");
                                        item.Status = SaleRequestStatus.Success;
                                        item.TopupProvider = item.Provider;
                                        await _saleService.SaleGateRequestUpdateAsync(item);
                                        break;
                                    }
                                default:
                                    {
                                        if (code != ResponseCodeConst.Success
                                            && code != ResponseCodeConst.ResponseCode_WaitForResult
                                            && code != ResponseCodeConst.ResponseCode_TimeOut
                                            && code != ResponseCodeConst.ResponseCode_InProcessing)
                                        {
                                            var saleDtoQuery = await _saleService.SaleGateRequestGetAsync(item.TransCode);
                                            if (saleDtoQuery.Status == SaleRequestStatus.WaitForResult)
                                            {
                                                if (saleDtoQuery.TransAmount > saleDtoQuery.TopupAmount)
                                                {
                                                    var callReponse = await TopupTransWorker(saleDtoQuery, Convert.ToInt32(saleDtoQuery.TransAmount));

                                                    _logger.LogInformation($"{saleDtoQuery.TransCode} - Topup_Bu_reponse: {callReponse.ToJson()}");
                                                    string message = string.Empty;
                                                    string title = string.Empty;
                                                    var botType = BotMessageType.Message;

                                                    if (callReponse.ResponseCode == ResponseCodeConst.Success)
                                                    {
                                                        saleDtoQuery.TopupProvider = saleDtoQuery.FirstProvider;
                                                        saleDtoQuery.TopupAmount = saleDtoQuery.TopupAmount + saleDtoQuery.TransAmount;
                                                        saleDtoQuery.Status = SaleRequestStatus.Success;
                                                        saleDtoQuery.EndDate = DateTime.Now;
                                                        await _saleService.SaleGateRequestUpdateAsync(saleDtoQuery);
                                                        message =
                                                        $"Mã GD: {item.TransCode}" +
                                                        $"\nTài khoản nạp: {saleDtoQuery.Provider}" +
                                                        $"\nSố tiền: {saleDtoQuery.TransAmount.ToFormat("đ")}" +
                                                        $"\nSĐT: {item.Mobile}" +
                                                        $"\nMessage: {callReponse.ResponseCode}- {callReponse.ResponseMessage}" +
                                                        $"\nGiao dịch nạp bù thành công";
                                                        title = "Nạp bù cho kênh GATE - Thành công";
                                                    }
                                                    else if (callReponse.ResponseCode == ResponseCodeConst.ResponseCode_WaitForResult
                                                        || callReponse.ResponseCode == ResponseCodeConst.ResponseCode_TimeOut)
                                                    {
                                                        saleDtoQuery.Status = SaleRequestStatus.Failed;
                                                        await _saleService.SaleGateRequestUpdateAsync(saleDtoQuery);
                                                        message =
                                                        $"GD: {item.TransCode}" +
                                                        $"\nTài khoản nạp: {saleDtoQuery.FirstProvider}" +
                                                        $"\nSố tiền: {saleDtoQuery.TransAmount.ToFormat("đ")}" +
                                                        $"\nSĐT: {item.Mobile}" +
                                                        $"\nMessage: {callReponse.ResponseCode} - {callReponse.ResponseMessage}" +
                                                        $"\nGiao dịch nạp bù chưa có kết quả";
                                                        title = "Nạp bù cho kênh GATE - Chưa có kết quả";
                                                    }
                                                    else
                                                    {
                                                        saleDtoQuery.Status = SaleRequestStatus.Failed;
                                                        await _saleService.SaleGateRequestUpdateAsync(saleDtoQuery);
                                                        message =
                                                        $"Mã GD: {item.TransCode}" +
                                                        $"\nTài khoản nạp: {saleDtoQuery.FirstProvider}" +
                                                        $"\nSố tiền: {saleDtoQuery.TransAmount.ToFormat("đ")}" +
                                                        $"\nSĐT: {item.Mobile}" +
                                                        $"\nMessage: {callReponse.ResponseCode} - {callReponse.ResponseMessage}" +
                                                        $"\nGiao dịch nạp bù chưa có kết quả";
                                                        title = "Nạp bù cho kênh GATE - Kết quả thất bại";
                                                    }

                                                    if (last != null && !string.IsNullOrEmpty(last.ChartId))
                                                    {
                                                        await SendTeleoGroupMessage(botType, last.ChartId, title, message);
                                                    }
                                                }
                                            }
                                        }

                                        break;
                                    }
                            }
                        }

                        #endregion

                        if (lstPending.Any() && config.AutoCheckTrans.IsSendTeleWarning)
                        {
                            if (last != null && !string.IsNullOrEmpty(last.ChartId))
                            {
                                var total = lstPending.Count;
                                var message = total <= 20
                                    ? string.Join(", ", lstPending.Select(x => x.TransCode).ToArray())
                                    : $"Tổng GD chưa có kết quả  là {total} gd. Vui lòng check thông tin chi tiết trong phan xu Gate";
                                await SendTeleoGroupMessage(BotMessageType.Wraning,
                                    last.ChartId, $"Giao dịch GATE quá  {config.AutoCheckTrans.TimePendingWarning}p chưa có KQ. Vui lòng check",
                                    message);
                            }
                        }

                        _inProcessGate = false;
                    }
                    else
                    {
                        if (last != null && !string.IsNullOrEmpty(last.ChartId))
                        {
                            await SendTeleoGroupMessage(BotMessageType.Wraning, last.ChartId, $"GD Kênh GATE Chưa có KQ vượt quá ngưỡng xử lý. Còn tồn {saleToCheck.Count} GD",
                                "Vui lòng kiểm tra hệ thống. Đang có nhiều GD chưa có KQ nhưng cần nạp cho khách");
                        }
                    }
                }
                else
                {
                    _logger.LogInformation($"No pending transactions");
                }
            }
            catch (Exception e)
            {
                _logger.LogError($"AutoCheckGateTrans error:{e.Message}");
            }
        }

        private async Task<MessageResponseBase> TopupTransWorker(SaleGateRequestDto saleGateUpdate, int transAmount)
        {
            var responseMessage = new MessageResponseBase();
            try
            {
                var publicKey = saleGateUpdate.FirstProvider;
                string productCode = "";
                var gateProviderInfo = await _grpcClient.GetClientCluster(GrpcServiceName.TopupGateway).SendAsync(new GateProviderInfoRequest()
                {
                    ProviderCode = publicKey
                });

                if (saleGateUpdate.TransAmount == transAmount)
                    productCode = saleGateUpdate.ProductCode;
                else
                {
                    var code = transAmount / 1000;
                    productCode = saleGateUpdate.CategoryCode + "_" + code.ToString();
                }

                if (gateProviderInfo.ResponseStatus.ErrorCode == ResponseCodeConst.Success)
                {
                    var infoAccount = gateProviderInfo.Results;
                    var requestDto = new WorkerTopupRequest
                    {
                        Amount = transAmount,
                        Channel = Channel.API,
                        AgentType = AgentType.AgentApi,
                        AccountType = SystemAccountType.MasterAgent,
                        CategoryCode = saleGateUpdate.CategoryCode,
                        ProductCode = productCode,
                        PartnerCode = infoAccount.ApiUser,
                        ReceiverInfo = saleGateUpdate.Mobile,
                        RequestIp = string.Empty,
                        ServiceCode = saleGateUpdate.ServiceCode,
                        StaffAccount = infoAccount.ApiUser,
                        StaffUser = infoAccount.ApiUser,
                        TransCode = saleGateUpdate.TransCode,
                        RequestDate = DateTime.Now,
                        IsCheckReceiverType = false,
                        IsNoneDiscount = false,
                        DefaultReceiverType = "",
                        IsCheckAllowTopupReceiverType = false
                    };
                    var reponseTopup = await _grpcClient.GetClientCluster(GrpcServiceName.Worker).SendAsync(requestDto);

                    _logger.LogInformation($"{saleGateUpdate.TransCode} - {saleGateUpdate.FirstProvider} TopupTransWorker_Backend: reponse: {reponseTopup.ToJson()}");

                    if (!string.IsNullOrEmpty(infoAccount.AlarmTeleChatId) && infoAccount.IsAlarm)
                    {
                        BotMessageType type = responseMessage.ResponseCode == ResponseCodeConst.Success
                            ? BotMessageType.Message : responseMessage.ResponseCode == ResponseCodeConst.Error
                            ? BotMessageType.Error : BotMessageType.Wraning;

                        await _bus.Publish<SendBotMessageToGroup>(new
                        {
                            MessageType = type,
                            BotType = BotType.Private,
                            ChatId = infoAccount.AlarmTeleChatId,
                            Module = "TopupGate",
                            Title = "Nạp Bù GD",
                            Message =
                                $"Mã GD: {requestDto.TransCode}\n" +
                                $"Đại lý: {requestDto.PartnerCode}\n" +
                                $"Sản phẩm {requestDto.ProductCode}\n" +
                                $"Tài khoản thụ hưởng: {requestDto.ReceiverInfo}\n" +
                                $"Số tiền nạp: {requestDto.Amount.ToFormat("đ")}\n" +
                                $"Hình thức nap: Nạp bù tiền\n" +
                                $"Trạng thái: {reponseTopup.ResponseStatus.ErrorCode}\n" +
                                $"Nội dung: {reponseTopup.ResponseStatus.Message}",
                            TimeStamp = DateTime.Now,
                            CorrelationId = Guid.NewGuid()
                        }); ;
                    }

                    var responseStatus = new MessageResponseBase
                    {
                        TransCode = requestDto.TransCode,
                        ResponseCode = reponseTopup.ResponseStatus.ErrorCode,
                        ResponseMessage = reponseTopup.ResponseStatus.Message
                    };

                    return responseStatus;
                }
                else
                {
                    _logger.LogInformation($"GET: {publicKey} TopupTransWorker_Backend_Profile: {gateProviderInfo.ToJson()}");
                }

            }
            catch (Exception ex)
            {
                _logger.LogError($"{saleGateUpdate.TransCode} - {saleGateUpdate.FirstProvider} TopupTransWorker_Backend Exception: {ex}");
            }

            return responseMessage;
        }
    }
}