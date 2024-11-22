using System.Threading.Tasks;
using ServiceStack;
using GMB.Topup.Gw.Domain.Services;
using GMB.Topup.Gw.Model;
using GMB.Topup.Gw.Model.Dtos;
using GMB.Topup.Gw.Model.RequestDtos;

using Microsoft.Extensions.Logging;
using System;
using GMB.Topup.Gw.Model.Events;
using MassTransit;

using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;
using GMB.Topup.Contracts.Commands.Commons;
using GMB.Topup.Contracts.Requests.Commons;
using GMB.Topup.Discovery.Requests.Backends;
using GMB.Topup.Discovery.Requests.TopupGateways;
using GMB.Topup.Discovery.Requests.Workers;
using GMB.Topup.Shared;
using GMB.Topup.Shared.ConfigDtos;
using GMB.Topup.Shared.Dtos;
using GMB.Topup.Shared.Helpers;

namespace GMB.Topup.Backend.Interface.Services
{
    //[Authenticate()]
    public partial class BackendService : Service
    {
        //private readonly Logger _logger = LogManager.GetLogger("BackendService");
        private readonly ILogger<BackendService> _logger;
        private readonly ITransactionService _transactionService;
        private readonly ILimitTransAccountService _limitTransAccountService;
        private readonly ISystemService _systemService;
        private readonly ISaleService _saleService;
        //private readonly IServiceGateway _gateway; gunner
        private readonly IConfiguration _configuration;
        private readonly GrpcClientHepper _grpcClient;
        private readonly IBus _bus;

        public BackendService(ISaleService saleService,
            ITransactionService transactionService, ILimitTransAccountService limitTransAccountService,
            ILogger<BackendService> logger, ISystemService systemService, IConfiguration configuration, IBus bus, GrpcClientHepper grpcClient)
        {
            _saleService = saleService;
            _transactionService = transactionService;
            _limitTransAccountService = limitTransAccountService;
            _logger = logger;
            _bus = bus;
            _systemService = systemService;
            _configuration = configuration;
            _grpcClient = grpcClient;

            //_gateway = HostContext.AppHost.GetServiceGateway(); gunner
        }

        public async Task<object> GetAsync(TopupListGetRequest topupListGetRequest)
        {

            _logger.LogInformation("TopupListGetRequest request: {Request}", topupListGetRequest.ToJson());
            var rs = await _saleService.SaleRequestGetListAsync(topupListGetRequest);
            _logger.LogInformation("TopupListGetRequest response: {Code}-{Message}", rs.ResponseCode,
                rs.ResponseMessage);
            return rs;
        }

        public async Task<object> PatchAsync(TopupUpdateStatusRequest topupUpdateStatusRequest)
        {
            _logger.LogInformation($"TopupUpdateStatusRequest:{topupUpdateStatusRequest.ToJson()}");
            var tran = await _saleService.SaleRequestGetAsync(topupUpdateStatusRequest.TransCode);
            var message = new MessageResponseBase();

            if (tran != null)
            {
                var result = await _saleService.SaleRequestUpdateStatusAsync(tran.TransCode,
                    string.Empty,
                    topupUpdateStatusRequest.Status, isBackend: true);

                if (result)
                {
                    message.ResponseCode = ResponseCodeConst.Success;
                    message.ResponseMessage = "Success";
                    if (topupUpdateStatusRequest.Status == SaleRequestStatus.Success)
                    {
                        if (!string.IsNullOrEmpty(tran.ParentCode) && tran.AgentType == AgentType.SubAgent)
                            await _saleService.CommissionRequest(tran);
                    }

                    await _bus.Publish(new ReportTransStatusMessage()
                    {
                        TransCode = tran.TransCode,
                        Status = topupUpdateStatusRequest.Status switch
                        {
                            SaleRequestStatus.Success => 1,
                            SaleRequestStatus.Failed => 3,
                            SaleRequestStatus.Canceled => 3,
                            _ => 2
                        }
                    });
                }
                else
                {
                    message.ResponseMessage =
                        $"Cập nhật mã giao dịch {topupUpdateStatusRequest.TransCode} không thành công!";
                }
            }
            else
            {
                message.ResponseMessage = $"Mã giao dịch {topupUpdateStatusRequest.TransCode} không tồn tại!";
            }

            return message;
        }

        public async Task<object> PatchAsync(TopupUpdateRequest topupUpdateRequest)
        {
            var tran = await _saleService.SaleRequestGetAsync(topupUpdateRequest.TransCode);
            // if (!string.IsNullOrEmpty(topupUpdateRequest.TransCode))
            // {
            //     tran = await _saleService.SaleRequestGetAsync(topupUpdateRequest.TransCode);
            // }
            // else if (!string.IsNullOrEmpty(topupUpdateRequest.TransRef))
            // {
            //     tran = await _saleService.SaleRequestGetTransRefAsync(topupUpdateRequest.TransRef);
            // }

            var message = new MessageResponseBase();
            if (tran != null)
            {
                if (topupUpdateRequest.PaymentAmount > 0)
                    tran.PaymentAmount = topupUpdateRequest.PaymentAmount;
                if (!string.IsNullOrEmpty(topupUpdateRequest.PaymentTransCode))
                    tran.PaymentTransCode = topupUpdateRequest.PaymentTransCode;
                if (topupUpdateRequest.Status > 0)
                    tran.Status = topupUpdateRequest.Status;
                if (!string.IsNullOrEmpty(topupUpdateRequest.Provider))
                    tran.Provider = topupUpdateRequest.Provider;
                if (!string.IsNullOrEmpty(topupUpdateRequest.ProviderTransCode))
                    tran.ProviderTransCode = topupUpdateRequest.ProviderTransCode;
                var result = await _saleService.SaleRequestUpdateAsync(tran);

                if (result != null)
                {
                    message.ResponseCode = ResponseCodeConst.Success;
                    message.ResponseMessage = "Success";
                }
                else
                {
                    message.ResponseMessage =
                        $"Cập nhật giao dịch {topupUpdateRequest.TransCode} không thành công!";
                }
            }
            else
            {
                message.ResponseMessage = $"Mã giao dịch {topupUpdateRequest.TransCode} không tồn tại!";
            }

            return message;
        }

        public async Task<object> GetAsync(GetTopupHistoryRequest request)
        {

            _logger.LogInformation("GetTopupHistoryRequest request: {Request}", request.ToJson());
            var rs = await _transactionService.GetTopupHistoriesAsync(request);
            _logger.LogInformation("GetTopupHistoryRequest response: {Code}-{Message}", rs.ResponseCode,
                rs.ResponseMessage);
            return rs;
        }

        public async Task<object> GetAsync(GetTopupItemsRequest request)
        {
            _logger.LogInformation("GetTopupItemsRequest request: {Request}", request.ToJson());
            var rs = await _transactionService.GetSaleTransactionDetailAsync(request);
            _logger.LogInformation("GetTopupItemsRequest response: {Code}-{Message}", rs.ResponseCode,
                rs.ResponseMessage);
            return rs;
        }

        public async Task<object> PostAsync(InvoiceRequest request)
        {
            _logger.LogInformation("InvoiceRequest request: {Request}", request.ToJson());
            var rs = await _transactionService.InvoiceCreateAync(request.ConvertTo<InvoiceDto>());
            _logger.LogInformation("InvoiceRequest response: {Code}", rs.TransCode);
            return rs;
        }

        public async Task<object> AnyAsync(GetSaleRequest request)
        {
            _logger.LogInformation("GetSaleRequest request: {Request}", request.ToJson());
            var rs = await _transactionService.GetSaleRequest(request);
            _logger.LogInformation("InvoiceRequest response: {Code}", rs.TransCode);
            return rs;
        }

        public async Task<object> GetAsync(GetSaleTopupRequest request)
        {

            _logger.LogInformation("GetSaleTopupRequest request: {Request}", request.ToJson());
            var rs = await _transactionService.GetSaleTopupRequest(request);
            _logger.LogInformation("GetSaleTopupRequest  : {ResponseCode}", rs.ResponseCode);
            return rs;
        }

        public async Task<object> GetAsync(GetCardBatchRequest request)
        {
            _logger.LogInformation("GetCardBatchRequest request: {Request}", request.ToJson());
            var rs = await _transactionService.GetCardBatchRequest(request);
            _logger.LogInformation("GetCardBatchRequest  : {ResponseCode}", rs.ResponseCode);
            return rs;
        }

        public async Task<object> GetAsync(GetPayBatchBill request)
        {
            _logger.LogInformation("GetPayBatchBill request: {Request}", request.ToJson());
            var rs = await _transactionService.GetPayBatchBillRequest(request);
            _logger.LogInformation("GetPayBatchBill response: {Code}", rs.ToJson());
            return rs;
        }

        public async Task<object> PostAsync(CreateOrUpdateLimitAccountTransRequest request)
        {
            _logger.LogInformation("CreateOrUpdateLimitAccountTransRequest request: {Request}", request.ToJson());
            var rs = await _limitTransAccountService.CreateOrUpdateLimitTransAccount(request);
            _logger.LogInformation("CreateOrUpdateLimitAccountTransRequest response: {Code}", rs.ToJson());
            return new ResponseMessageApi<object>
            {
                Error = null,
                Result = null,
                Success = rs
            };
        }

        public async Task<object> GetAsync(GetAvailableLimitAccount request)
        {
            _logger.LogInformation("GetAvailableLimitAccount request: {Request}", request.ToJson());
            var rs = await _limitTransAccountService.GetAvailableLimitAccount(request);
            _logger.LogInformation("GetAvailableLimitAccount response: {Code}", rs.ToJson());
            return new ResponseMessageApi<decimal>
            {
                Error = null,
                Result = rs,
                Success = true
            };
        }

        public async Task<object> GetAsync(GetTotalPerDayProductRequest request)
        {
            _logger.LogInformation("GetTotalPerDayProductRequest request: {Request}", request.ToJson());
            var rs = await _transactionService.GetLimitProductTransPerDay(request.AccountCode, request.ProductCode);
            _logger.LogInformation("GetTotalPerDayProductRequest response: {Code}", rs.ToJson());
            return new ResponseMessageApi<AccountProductLimitDto>
            {
                Error = null,
                Result = rs,
                Success = true
            };
        }


        public async Task<object> PostAsync(TransactionRefundRequest topupRefund)
        {
            _logger.LogInformation("TransactionRefund request {Request}", topupRefund.ToJson());
            var response = await _saleService.RefundTransaction(topupRefund.TransCode);
            _logger.LogInformation("TransactionRefund return {Response}", response.ToJson());
            return response;
        }

        /// <summary>
        /// Lấy thẻ theo APi
        /// </summary>
        /// <param name="cardSaleRequest"></param>
        /// <returns></returns>
        public async Task<object> PostAsync(CardImportProviderRequest cardProviderRequest)
        {
            Task.Factory.StartNew(() =>
            {
                _logger.LogInformation("CardImportProviderRequest request {Request}", cardProviderRequest.ToJson());
                var response = _saleService.CardImportProvider(cardProviderRequest).Result;
                _logger.LogInformation("CardImportProviderRequest return {Response}", response.ToJson());
            });

            return new NewMessageResponseBase<string>
            {
                ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Success, "Quá trình thực hiện sẽ có kết quả ở màn hình chi tiết nhập hàng")
            };
        }

        public async Task<object> GetAsync(PingRouteRequest request)
        {
            return await Task.FromResult("OK");
        }

        public async Task<object> GetAsync(GetOffsetTopupHistoryRequest request)
        {
            _logger.LogInformation("GetOffsetTopupHistoryRequest request: {Request}", request.ToJson());
            var rs = await _transactionService.GetOffsetTopupHistoriesAsync(request);
            _logger.LogInformation("GetOffsetTopupHistoryRequest response: {Code}-{Message}", rs.ResponseCode,
                rs.ResponseMessage);
            return rs;
        }

        public async Task<object> PostAsync(OffsetTopupRequest offsetTopup)
        {
            _logger.LogInformation("OffsetTopupRequest request {Request}", offsetTopup.ToJson());
            var response = await SendOffsetTopup(offsetTopup.TransCode);
            _logger.LogInformation("OffsetTopupRequest return {Response}", response.ToJson());
            return response;
        }

        public async Task<NewMessageResponseBase<string>> PostAsync(UpdateCardCodeRequest request)
        {
            string transCode = request.TransCode;
            var reponseMage = new NewMessageResponseBase<string>()
            {
                ResponseStatus = new ResponseStatusApi()
            };
            try
            {
                if (string.IsNullOrEmpty(transCode))
                {
                    reponseMage.ResponseStatus = new ResponseStatusApi()
                    {
                        ErrorCode = ResponseCodeConst.Error,
                        Message = $"Quý khách chưa truyền mã giao dịch",
                    };
                    return reponseMage;
                }

                var dtoSale = await _saleService.SaleRequestGetAsync(transCode);
                if (dtoSale == null)
                {
                    reponseMage.ResponseStatus = new ResponseStatusApi()
                    {
                        ErrorCode = ResponseCodeConst.Error,
                        Message = $"Giao dịch {transCode} không tồn tại",
                    };
                    return reponseMage;
                }

                // if (dtoSale.Status != SaleRequestStatus.Success)
                // {
                //     reponseMage.ResponseStatus = new ResponseStatusApi()
                //     {
                //         ErrorCode = ResponseCodeConst.Error,
                //         Message = $"Giao dịch {transCode} chưa ở trạng thái thành công !",
                //     };
                //     return reponseMage;
                // }

                if (!dtoSale.ServiceCode.StartsWith("PIN"))
                {
                    reponseMage.ResponseStatus = new ResponseStatusApi()
                    {
                        ErrorCode = ResponseCodeConst.Error,
                        Message = $"Giao dịch {transCode} không phải là giao dịch mua mã thẻ !",
                    };
                    return reponseMage;
                }

                var check = await _saleService.CheckSaleItemAsync(dtoSale.TransCode);
                if (check)
                {
                    reponseMage.ResponseStatus = new ResponseStatusApi()
                    {
                        ErrorCode = ResponseCodeConst.Error,
                        Message = $"Giao dịch {transCode} mã thẻ đã tồn tại !",
                    };
                    return reponseMage;
                }

                var response = await _grpcClient.GetClientCluster(GrpcServiceName.TopupGateway).SendAsync(new GateCheckTransRequest
                {
                    ProviderCode = dtoSale.Provider,
                    ServiceCode = dtoSale.ServiceCode,
                    TransCodeToCheck = dtoSale.TransCode
                });

                if (response.ResponseStatus.ErrorCode == ResponseCodeConst.Success && !string.IsNullOrEmpty(response.Results.PayLoad))
                {
                    var cards = response.Results.PayLoad.FromJson<List<CardRequestResponseDto>>();
                    var lstTopupItem = (from item in cards.ToList()
                                        select new SaleItemDto
                                        {
                                            Amount = Convert.ToInt32(dtoSale.Price),
                                            Serial = item.Serial,
                                            CardExpiredDate = item.ExpiredDate,
                                            Status = SaleRequestStatus.Success,
                                            CardValue = Convert.ToInt32(dtoSale.Price),
                                            CardCode = item.CardCode,
                                            ServiceCode = dtoSale.ServiceCode,
                                            PartnerCode = dtoSale.PartnerCode,
                                            SaleType = "PINCODE",
                                            SaleTransCode = dtoSale.TransCode,
                                            CreatedTime = DateTime.Now
                                        }).ToList();

                    await _saleService.SaleItemListCreateAsync(lstTopupItem);
                    await _saleService.SaleRequestUpdateStatusAsync(transCode, null, SaleRequestStatus.Success,
                        isBackend: true);
                    reponseMage.ResponseStatus = new ResponseStatusApi()
                    {
                        ErrorCode = ResponseCodeConst.Success,
                        Message = $"Giao dịch {transCode} cập nhật thành công !",
                    };
                }
                else
                {
                    reponseMage.ResponseStatus = new ResponseStatusApi()
                    {
                        ErrorCode = ResponseCodeConst.Error,
                        Message = $"Giao dịch {transCode} không thể cập nhật, quý khách check lại giao dịch !",
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"{transCode} UpdateCardCodeRequest_Exception: {ex}");
                reponseMage.ResponseStatus = new ResponseStatusApi()
                {
                    ErrorCode = ResponseCodeConst.Error,
                    Message = $"Xử lý giao dịch {transCode} sảy ra sự cố. Vui lòng kiểm tra lại.",
                };
            }

            return reponseMage;
        }

        private async Task<NewMessageResponseBase<string>> SendOffsetTopup(string transCode)
        {
            try
            {
                var reponseMage = new NewMessageResponseBase<string>()
                {
                    ResponseStatus = new ResponseStatusApi()
                };
                SaleRequestDto dtoTemp = await _saleService.SaleRequestGetAsync(transCode);
                if (dtoTemp == null)
                {
                    reponseMage.ResponseStatus = new ResponseStatusApi()
                    {
                        ErrorCode = ResponseCodeConst.Error,
                        Message = $"Giao dịch {transCode} không tồn tại",
                    };

                    return reponseMage;
                }

                if (dtoTemp.SaleType != SaleType.Slow)
                {
                    reponseMage.ResponseStatus = new ResponseStatusApi()
                    {
                        ErrorCode = ResponseCodeConst.Error,
                        Message = $"Giao dịch {transCode} không phải là giao dịch nạp chậm",
                    };

                    return reponseMage;
                }

                if (dtoTemp.Status == SaleRequestStatus.Success)
                {
                    reponseMage.ResponseStatus = new ResponseStatusApi()
                    {
                        ErrorCode = ResponseCodeConst.Error,
                        Message = $"Giao dịch {transCode} đã thành công. Nên không thể nạp bù",
                    };
                    return reponseMage;
                }
                else if (dtoTemp.Status == SaleRequestStatus.Failed || dtoTemp.Status == SaleRequestStatus.Canceled)
                {
                    reponseMage.ResponseStatus = new ResponseStatusApi()
                    {
                        ErrorCode = ResponseCodeConst.Error,
                        Message = $"Giao dịch {transCode} đã lỗi đã hoàn tiền, nên không xử lý nạp bù nữa",
                    };
                    return reponseMage;
                }

                string partnerCodeOffset = _configuration["Hangfire:AutoCheckTrans:PartnerCodeOffset"];
                var saleOffsetDto = _saleService.SaleOffsetOriginGetAsync(dtoTemp.TransCode, true).Result;
                if (saleOffsetDto == null)
                    saleOffsetDto = _saleService.SaleOffsetCreateAsync(dtoTemp, partnerCodeOffset).Result;
                else
                {
                    if (saleOffsetDto.Status != SaleRequestStatus.Failed &&
                        saleOffsetDto.Status != SaleRequestStatus.Canceled)
                        return new NewMessageResponseBase<string>()
                        {
                            ResponseStatus = new ResponseStatusApi()
                            {
                                ErrorCode = ResponseCodeConst.Error,
                                Message = "Giao dịch nạp bù đang xử lý, chưa có kết quả cuối."
                            }
                        };
                }

                var request = dtoTemp.ConvertTo<WorkerTopupRequest>();
                request.RequestDate = DateTime.Now;
                request.StaffAccount = partnerCodeOffset;
                request.AccountType = SystemAccountType.MasterAgent;
                request.PartnerCode = partnerCodeOffset;
                request.TransCode = saleOffsetDto.TransRef;
                request.RequestDate = DateTime.Now;
                var getApi = _grpcClient.GetClientCluster(GrpcServiceName.Worker).SendAsync(request).Result;
                _logger.LogError(
                    $"{partnerCodeOffset}|{dtoTemp.TransRef}|{dtoTemp.TransCode} SendOffsetTopup SendAsync reponse : {getApi.ToJson()}");
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
                        dtoTemp.Status = SaleRequestStatus.Success;
                        await _saleService.SaleRequestUpdateStatusAsync(dtoTemp.TransCode, null,
                            SaleRequestStatus.Success);
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
                                $"\nSố tiền: {dtoTemp.Amount.ToString()} đ" +
                                $"\nSĐT: {dtoTemp.ReceiverInfo}" +
                                $"\nVui lòng kiểm tra lại thông tin",
                            BotMessageType = BotMessageType.Message
                        });
                        reponseMage.ResponseStatus = new ResponseStatusApi()
                        {
                            ErrorCode = ResponseCodeConst.Success,
                            Message = $"Nạp bù cho giao dịch {dtoTemp.TransCode} thành công"
                        };
                        await _bus.Publish(new ReportTransStatusMessage()
                        {
                            TransCode = saleOffsetDto.TransCode,
                            Status = 1
                        });
                        break;
                    case ResponseCodeConst.ResponseCode_WaitForResult:
                    case ResponseCodeConst.ResponseCode_TimeOut:
                    case ResponseCodeConst.ResponseCode_Paid:
                    case ResponseCodeConst.ResponseCode_InProcessing:
                        saleOffsetDto.Status = SaleRequestStatus.ProcessTimeout;
                        reponseMage.ResponseStatus = new ResponseStatusApi()
                        {
                            ErrorCode = ResponseCodeConst.Error,
                            Message = $"Nạp bù cho giao dịch {dtoTemp.TransCode} chưa có kết quả."
                        };
                        break;
                    default:
                        saleOffsetDto.Status = SaleRequestStatus.Failed;
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
                                $"\nSố tiền: {dtoTemp.Amount.ToString()} đ" +
                                $"\nSĐT: {dtoTemp.ReceiverInfo}" +
                                $"\nLỗi: {returnMessage.ToJson()}",
                            BotMessageType = BotMessageType.Wraning
                        });
                        reponseMage.ResponseStatus = new ResponseStatusApi()
                        {
                            ErrorCode = ResponseCodeConst.Error,
                            Message = $"Nạp bù cho giao dịch {dtoTemp.TransCode} lỗi."
                        };
                        break;
                }

                await _saleService.SaleOffsetUpdateAsync(saleOffsetDto);
                return reponseMage;
            }
            catch (Exception ex)
            {
                _logger.LogError($"{transCode} SendOffsetTopup Exception: {ex}");
                return new NewMessageResponseBase<string>()
                {
                    ResponseStatus = new ResponseStatusApi()
                    {
                        ErrorCode = ResponseCodeConst.Error,
                        Message = $"Xử lý giao dịch {transCode} sảy ra sự cố. Vui lòng kiểm tra lại."
                    },
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
                    CorrelationId = Guid.NewGuid()
                });
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"SendTeleMessage : {ex}");
            }
        }


        //public async Task<NewMessageReponseBase<string>> PostAsync(Backend_test_Request request)
        //{
        //    try
        //    {
        //        var message = request.Data.FromJson<TransGatePushCommand>();
        //        if (message == null)
        //            message = new TransGatePushCommand()
        //            {
        //                Status = SaleRequestStatus.WaitForResult,
        //                CreatedDate = DateTime.Now,
        //                TransCode = "NT23103100000014",
        //                CategoryCode = "VTE_TOPUP",
        //                CorrelationId = Guid.NewGuid(),
        //                ProductCode = "VTE_TOPUP_50",
        //                FirstAmount = 1000,
        //                TransAmount = 50000,
        //                FirstProvider = "FAKE",
        //                Provider = "GATE",
        //                Mobile = "0962256458",
        //                ServiceCode = "TOPUP",
        //                Type = "2",
        //                ChartId = "121212",
        //                Vender = "VTE",
        //            };
        //        _logger.LogInformation($"Backend_test_Request Input request: {message.ToJson()}");
        //        var saleDto = message.ConvertTo<Gw.Model.Dtos.SaleGateRequestDto>();
        //        saleDto.TopupAmount = saleDto.FirstAmount;
        //        await _saleService.SaleGateCreateAsync(saleDto);
        //    }
        //    catch (Exception e)
        //    {
        //        _logger.LogError($"Backend_test_Request error: {e}");
        //    }

        //    return null;
        //}
    }
}