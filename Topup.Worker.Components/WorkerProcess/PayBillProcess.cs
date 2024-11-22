using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Topup.Gw.Model.Commands;
using Topup.Gw.Model.Dtos;
using Topup.Shared;
using Topup.Shared.ConfigDtos;
using Topup.Shared.Dtos;
using Microsoft.Extensions.Logging;
using Topup.Contracts.Requests.Commons;
using Topup.Discovery.Requests.Balance;
using Topup.Discovery.Requests.TopupGateways;
using Topup.Discovery.Requests.Workers;
using ServiceStack;

namespace Topup.Worker.Components.WorkerProcess
{
    public partial class WorkerProcess
    {
        public async Task<NewMessageResponseBase<WorkerResult>> PayBillRequest(WorkerPayBillRequest request)
        {
            try
            {
                var response = new NewMessageResponseBase<WorkerResult>()
                {
                    Results = new WorkerResult(),
                    ResponseStatus = new ResponseStatusApi()
                    {
                        ErrorCode = ResponseCodeConst.ResponseCode_WaitForResult,
                        Message = "",
                    }

                };
                _logger.LogInformation("PayBillRequest:{Request}", request.ToJson());
                if ((DateTime.Now - request.RequestDate).TotalSeconds >= _workerConfig.TimeOutProcess)
                {
                    _logger.LogWarning($"{request.TransCode}-{request.PartnerCode}-Transaction timeout over setting");
                    response.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error,
                        $"Giao dịch không thành công. Vui lòng thử lại sau");
                    return response;
                }

                if (string.IsNullOrEmpty(request.TransCode))
                {
                    response.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_00,
                        $"Vui lòng truyền mã giao dịch");
                    return response;
                }

                var saleRequest = request.ConvertTo<SaleRequestDto>();
                saleRequest.RequestDate = DateTime.Now;

                saleRequest.TransRef = request.TransCode;
                var checkExist = await _saleService.SaleRequestCheckAsync(saleRequest.TransRef,
                    saleRequest.PartnerCode);
                _logger.LogInformation($"CheckSaleRequestDone:{saleRequest.TransCode}-{saleRequest.TransRef}");

                if (checkExist.ResponseCode != ResponseCodeConst.ResponseCode_TransactionNotFound)
                {
                    _logger.LogWarning($"{saleRequest.TransRef}-{saleRequest.PartnerCode} is duplicate request");
                    response.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_InProcessing,"Giao dịch đang xử lý");
                    return response;
                }

                if (saleRequest.Amount <= 0)
                {
                    response.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_00,
                        $"Số tiền không hợp lệ");
                    return response;
                }

                saleRequest.CurrencyCode = CurrencyCode.VND.ToString("G");
                saleRequest.Status = SaleRequestStatus.InProcessing;

                //Cấu hình kênh
                var serviceConfiguration = await _externalServiceConnector.ServiceConfigurationAsync(request.TransCode,
                    saleRequest.PartnerCode,
                    saleRequest.ServiceCode,
                    saleRequest.CategoryCode, saleRequest.ProductCode,
                    saleRequest.Channel == Channel.API);
                if (serviceConfiguration == null || serviceConfiguration.Count <= 0)
                {
                    _logger.LogInformation($"{saleRequest.TransRef}-ServiceConfiguration not config");
                    response.ResponseStatus = new ResponseStatusApi(
                        ResponseCodeConst.Error,
                        "Giao dịch không thành công");
                    return response;
                }

                _logger.LogInformation($"{saleRequest.TransRef}-ServiceConfiguration:{serviceConfiguration.ToJson()}");
                //check hạn mức
                if (_workerConfig.IsCheckLimit && saleRequest.Channel != Channel.API)
                {
                    var checkLimit =
                        await _limitTransAccountService.CheckLimitAccount(saleRequest.StaffAccount,
                            saleRequest.PaymentAmount);
                    _logger.LogInformation(
                        $"{saleRequest.TransRef}-CheckLimit return:{checkLimit.ToJson()}");
                    if (checkLimit.ResponseCode != ResponseCodeConst.ResponseCode_Success)
                    {
                        response.ResponseStatus = new ResponseStatusApi(checkLimit.ResponseCode,
                            checkLimit.ResponseMessage);
                        return response;
                    }

                    var checkLimitProduct = await
                        _checkLimit.CheckLimitProductPerDay(saleRequest.PartnerCode, saleRequest.ProductCode,
                            saleRequest.Amount, saleRequest.Quantity, saleRequest.TransCode);
                    _logger.LogInformation(
                        $"{saleRequest.TransRef}-CheckLimitProductPerDay return:{checkLimitProduct.ToJson()}");
                    if (checkLimitProduct.ResponseCode != ResponseCodeConst.ResponseCode_Success)
                    {
                        response.ResponseStatus = new ResponseStatusApi(checkLimitProduct.ResponseCode,
                            checkLimitProduct.ResponseMessage);
                        return response;
                    }
                }

                //Check phi giao dich
                var fee = await _externalServiceConnector.GetProductFee(request.TransCode, saleRequest.PartnerCode,
                    saleRequest.ProductCode, saleRequest.Amount);
                if (fee == null)
                {
                    response.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error,
                        "Giao dịch không thành công");
                    return response;
                }

                _logger.LogInformation($"{saleRequest.TransRef}-GetFee return:{fee.FeeValue}");
                var discount = await
                    _externalServiceConnector.CheckProductDiscount(saleRequest.TransCode, saleRequest.PartnerCode,
                        saleRequest.ProductCode, saleRequest.Amount);

                if (discount == null)
                {
                    response.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error,
                        "Giao dịch không thành công");
                    return response;
                }

                _logger.LogInformation($"{saleRequest.TransRef}-GetDiscount return:{discount.ToJson()}");
                saleRequest = await _saleService.SaleRequestCreateAsync(saleRequest);
                if (saleRequest == null)
                {
                    response.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_00,
                        "Tiếp nhận giao dịch không thành công");
                    return response;
                }

                _logger.LogInformation($"Create topup request success: {saleRequest.TransCode}-{saleRequest.TransRef}");

                saleRequest.PaymentAmount = discount.PaymentAmount;
                saleRequest.DiscountRate = discount.DiscountValue;
                saleRequest.FixAmount = discount.FixAmount;
                saleRequest.DiscountAmount = discount?.DiscountAmount;
                saleRequest.Fee = fee.FeeValue;
                saleRequest.PaymentAmount += fee.FeeValue;

                if (saleRequest.PaymentAmount <= 0)
                {
                    response.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_00,
                        $"Số tiền thanh toán không hợp lệ");
                    saleRequest.Status = SaleRequestStatus.Failed;
                    await _saleService.SaleRequestUpdateAsync(saleRequest);
                    await _saleService.PublishConsumerReport(new SaleReponseDto()
                    {
                        NextStep = 0,
                        Sale = saleRequest,
                        Status = saleRequest.Status
                    });
                    return response;
                }

                var serviceConfig = serviceConfiguration.OrderBy(c => c.Priority).First();
                saleRequest.Provider = serviceConfig.ProviderCode;
                saleRequest.ParentProvider = serviceConfig.ParentProvider;
                // saleRequest.Status = SaleRequestStatus.Init;
                // var updateSaleRequest = await _saleService.SaleRequestUpdateAsync(saleRequest);
                // if (updateSaleRequest == null)
                // {
                //     _logger.LogInformation($"{saleRequest.TransCode}-{saleRequest.TransRef}-Update SaleReuest fail");
                //     response.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_00,
                //         "Tiếp nhận giao dịch không thành công");
                //     return response;
                // }

                // _logger.LogInformation(
                //     $"Update topup request success => Processing payment: {saleRequest.TransCode}-{saleRequest.TransRef}");
                // response.ResponseStatus = new ResponseStatusApi(
                //     ResponseCodeConst.ResponseCode_RequestReceived, "Tiếp nhận giao dịch thành công");


                #region Payment

                var paymentResponse = await _grpcClient.GetClientCluster(GrpcServiceName.Balance).SendAsync(new BalancePaymentRequest()
                {
                    AccountCode = saleRequest.PartnerCode,
                    PaymentAmount = saleRequest.PaymentAmount,
                    CurrencyCode = saleRequest.CurrencyCode,
                    TransRef = saleRequest.TransRef,
                    TransCode = saleRequest.TransCode,
                    TransNote = $"Thanh toán cho giao dịch: {saleRequest.TransRef}"
                });

                #endregion

                _logger.LogInformation(
                    $"Payment for bill request return: {paymentResponse.ResponseStatus.ErrorCode}-{paymentResponse.ResponseStatus.Message} {saleRequest.TransCode}-{saleRequest.TransRef}");

                decimal balance = 0;
                if (paymentResponse.ResponseStatus.ErrorCode == ResponseCodeConst.Success)
                {
                    saleRequest.Status = SaleRequestStatus.Paid;
                    saleRequest.PaymentTransCode = paymentResponse.Results.TransactionCode;
                    var topupUpdate = await _saleService.SaleRequestUpdateAsync(saleRequest);
                    if (topupUpdate != null)
                    {
                        balance = paymentResponse.Results.SrcBalance;
                        var providerTwo =
                            (from x in serviceConfiguration.Where(c =>
                                    c.ProviderCode != serviceConfig.ProviderCode)
                             select new ProviderConfig
                             {
                                 ProviderCode = x.ProviderCode,
                                 Priority = x.Priority,
                                 MustCount = !string.IsNullOrEmpty(x.WorkShortCode),
                                 ProviderMaxWaitingTimeout = x.ProviderMaxWaitingTimeout,
                                 ProviderSetTransactionTimeout = x.ProviderSetTransactionTimeout,
                                 StatusResponseWhenJustReceived = x.StatusResponseWhenJustReceived,
                                 IsEnableResponseWhenJustReceived = x.IsEnableResponseWhenJustReceived,
                                 WaitingTimeResponseWhenJustReceived = x.WaitingTimeResponseWhenJustReceived,
                             }).ToList();

                        string transCodeProvider;
                        string providerCode;
                        string receiverType;
                        string providerResponseTransCode;
                        if (saleRequest.ProductCode.StartsWith("VMS") ||
                            saleRequest.ProductCode.StartsWith("VTE") ||
                            saleRequest.ProductCode.StartsWith("VNA"))
                        {
                            var requestTopup = new GateTopupRequest
                            {
                                ServiceCode = saleRequest.ServiceCode,
                                CategoryCode = saleRequest.CategoryCode,
                                Amount = saleRequest.Amount,
                                ReceiverInfo = saleRequest.ReceiverInfo,
                                ProductCode = saleRequest.ProductCode,
                                TransRef = saleRequest.TransCode,
                                ProviderCode = serviceConfig.ProviderCode,
                                Vendor = saleRequest.ProductCode.Split('_')[0],
                                PartnerCode = saleRequest.PartnerCode,
                                ReferenceCode = saleRequest.TransRef,
                                TransCodeProvider = saleRequest.ProviderTransCode,
                                RequestDate = saleRequest.CreatedTime
                            };
                            // response = await _gateway.SendAsync(requestTopup);

                            var (newMessageReponseBase, transactionInfoDto) = await CallTopupPriority(requestTopup,
                                !string.IsNullOrEmpty(serviceConfig.WorkShortCode), providerTwo);
                            response.ResponseStatus = newMessageReponseBase.ResponseStatus;
                            if (newMessageReponseBase.Results != null)
                                response.Results = newMessageReponseBase.Results.ConvertTo<WorkerResult>();

                            _logger.LogInformation($"{saleRequest.TransCode}-{saleRequest.TransRef}-GateTopupRequest_Bill return:{response.ToJson()}-{newMessageReponseBase.ToJson()}");
                            providerCode = transactionInfoDto.ProviderCode;
                            transCodeProvider = transactionInfoDto.TransCodeProvider;
                            saleRequest.Provider = providerCode;
                            if (!string.IsNullOrEmpty(transCodeProvider))
                                saleRequest.ProviderTransCode = transCodeProvider;

                            providerResponseTransCode = newMessageReponseBase.Results != null ? newMessageReponseBase.Results.ProviderResponseTransCode : "";
                            receiverType = newMessageReponseBase.Results != null ? newMessageReponseBase.Results.ReceiverType : "";
                            saleRequest.ProviderResponseCode = providerResponseTransCode;
                            saleRequest.ReceiverTypeResponse = receiverType;

                        }
                        else
                        {
                            var requestPayBill = new GatePayBillRequest
                            {
                                ServiceCode = saleRequest.ServiceCode,
                                CategoryCode = saleRequest.CategoryCode,
                                Amount = saleRequest.Amount,
                                ReceiverInfo = saleRequest.ReceiverInfo,
                                TransRef = saleRequest.TransCode,
                                ProviderCode = serviceConfig.ProviderCode,
                                RequestDate = saleRequest.CreatedTime,
                                ProductCode = saleRequest.ProductCode,
                                PartnerCode = saleRequest.PartnerCode,
                                ReferenceCode = saleRequest.TransRef,
                                TransCodeProvider = saleRequest.ProviderTransCode,
                                IsInvoice = request.IsInvoice
                            };
                            //  response = await _gateway.SendAsync(requestPayBill);

                            var (newMessageReponseBase, transactionInfoDto) =
                                await CallPayBillPriority(requestPayBill, providerTwo);
                            response.ResponseStatus = newMessageReponseBase.ResponseStatus;
                            if (newMessageReponseBase.Results != null)
                                response.Results = newMessageReponseBase.Results.ConvertTo<WorkerResult>();

                            _logger.LogInformation($"{saleRequest.TransCode}-{saleRequest.TransRef}-GatePayBillRequest return:{response.ToJson()}-{transactionInfoDto.ToJson()}");
                            providerCode = transactionInfoDto.ProviderCode;
                            transCodeProvider = transactionInfoDto.TransCodeProvider;
                            if (!string.IsNullOrEmpty(transCodeProvider))
                                saleRequest.ProviderTransCode = transCodeProvider;
                            if (!string.IsNullOrEmpty(providerCode))
                                saleRequest.Provider = providerCode;

                            providerResponseTransCode = newMessageReponseBase.Results != null ? newMessageReponseBase.Results.ProviderResponseTransCode : "";
                            receiverType = newMessageReponseBase.Results != null ? newMessageReponseBase.Results.ReceiverType : "";

                            saleRequest.ProviderResponseCode = providerResponseTransCode;
                            saleRequest.ReceiverTypeResponse = receiverType;
                        }

                        _logger.LogInformation($"Call topupgate response:{response.ToJson()}");

                        if (response.ResponseStatus.ErrorCode == ResponseCodeConst.Success)
                        {
                            response.ResponseStatus.Message =
                                $"Thanh toán cho hóa đơn {saleRequest.ReceiverInfo} thành công";

                            saleRequest.Status = SaleRequestStatus.Success;
                            await _saleService.SaleRequestUpdateStatusAsync(saleRequest.TransCode,
                                saleRequest.Provider, SaleRequestStatus.Success, transCodeProvider,
                                providerResponseTransCode: providerResponseTransCode,receiverType: receiverType);
                            if (!string.IsNullOrEmpty(saleRequest.ParentCode) &&
                                saleRequest.AgentType == AgentType.SubAgent)
                            {
                                await _saleService.CommissionRequest(saleRequest);
                            }
                        }
                        else if (response.ResponseStatus.ErrorCode ==
                                 ResponseCodeConst.ResponseCode_WaitForResult
                                 || response.ResponseStatus.ErrorCode ==
                                 ResponseCodeConst.ResponseCode_TimeOut
                                 || response.ResponseStatus.ErrorCode ==
                                 ResponseCodeConst.ResponseCode_InProcessing)
                        {
                            _logger.LogWarning(
                                $"PayBillPending: {saleRequest.TransCode}-{saleRequest.TransRef}");
                            response.ResponseStatus.ErrorCode = response.ResponseStatus.ErrorCode;
                            response.ResponseStatus.Message =
                                "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ";
                            saleRequest.Status = SaleRequestStatus.WaitForResult;
                            await _saleService.SaleRequestUpdateStatusAsync(saleRequest.TransCode,
                                saleRequest.Provider, SaleRequestStatus.WaitForResult, transCodeProvider);
                        }
                        else
                        {
                            _logger.LogWarning(
                                $"PayBillRefund: {saleRequest.TransCode}-{saleRequest.TransRef}");
                            await _bus.Publish<PaymentCancelCommand>(new
                            {
                                CorrelationId = Guid.NewGuid(),
                                saleRequest.TransCode,
                                saleRequest.PaymentTransCode,
                                TransNote =
                                    $"Hoàn tiền cho giao dịch thanh toán: {saleRequest.TransRef}",
                                RevertAmount = saleRequest.PaymentAmount,
                                AccountCode = saleRequest.PartnerCode
                            });

                            saleRequest.Status = SaleRequestStatus.Failed;
                            await _saleService.SaleRequestUpdateStatusAsync(saleRequest.TransCode,
                                saleRequest.Provider, SaleRequestStatus.Failed, transCodeProvider);
                        }

                        // await _saleService.PublishConsumerReport(new SaleReponseDto()
                        // {
                        //     NextStep = 1,
                        //     Sale = saleRequest,
                        //     Status = saleRequest.Status,
                        // });
                    }
                    else
                    {
                        _logger.LogInformation(
                            $"Update topup item payment faild: {saleRequest.TransCode}-{saleRequest.TransRef}-{saleRequest.Status}");

                        // await _saleService.PublishConsumerReport(new SaleReponseDto()
                        // {
                        //     NextStep = 1,
                        //     Sale = saleRequest,
                        //     Status = SaleRequestStatus.Failed,
                        // });

                        await _bus.Publish<PaymentCancelCommand>(new
                        {
                            CorrelationId = Guid.NewGuid(),
                            saleRequest.TransCode,
                            saleRequest.PaymentTransCode,
                            TransNote = $"Hoàn tiền cho giao dịch thanh toán: {saleRequest.TransRef}",
                            RevertAmount = saleRequest.PaymentAmount,
                            AccountCode = saleRequest.PartnerCode
                        });
                        saleRequest.Status = SaleRequestStatus.Failed;
                        await _saleService.SaleRequestUpdateStatusAsync(saleRequest.TransCode,
                            saleRequest.Provider, SaleRequestStatus.Failed);
                    }
                }
                else
                {
                    _logger.LogInformation(
                        $"Payment fail. {saleRequest.TransCode}-{saleRequest.TransRef} - {paymentResponse.ResponseStatus.ErrorCode}-{paymentResponse.ResponseStatus.Message}");
                    await _saleService.SaleRequestUpdateStatusAsync(saleRequest.TransCode,
                        string.Empty, SaleRequestStatus.Failed);
                    if (paymentResponse.ResponseStatus.ErrorCode == ResponseCodeConst.ResponseCode_Balance_Not_Enough)
                    {
                        response.ResponseStatus = new ResponseStatusApi(
                            ResponseCodeConst.ResponseCode_Balance_Not_Enough,
                            "Thanh toán cho giao dịch lỗi. Vui lòng kiểm tra lại số dư");
                    }
                    else
                    {
                        //chỗ này gửi cảnh báo. theo dõi nguyên nhân
                        response.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error, "Giao dịch không thành công. Vui lòng thử lại sau");
                    }

                    // await _saleService.PublishConsumerReport(new SaleReponseDto()
                    // {
                    //     NextStep = 0,
                    //     Sale = saleRequest,
                    //     Status = SaleRequestStatus.Failed,
                    // });
                }

                await _saleService.PublishConsumerReport(new SaleReponseDto()
                {
                    NextStep = 0,
                    Sale = saleRequest,
                    Status = saleRequest.Status,
                    Balance = balance,
                    FeeDto = fee.ToJson(),
                });

                return response;
            }
            catch (Exception e)
            {
                _logger.LogError($"{request.TransCode}-PayBillRequestError: " + e);
                await SendTeleMessage(new SendTeleTrasactionRequest
                {
                    BotType = BotType.Dev,
                    TransRef = request.TransCode,
                    TransCode = request.TransCode,
                    Title = "GD có vấn đề!. Vui lòng kiểm tra trạng thái giao dịch",
                    Message = $"GD {request.TransCode}\nHàm PayBillRequest\nLỗi:{e.Message}",
                    BotMessageType = BotMessageType.Error
                });
                return new NewMessageResponseBase<WorkerResult>()
                {
                    ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_WaitForResult,
                        "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ. Xin vui lòng thử lại sau"),
                    Results = new WorkerResult()
                };
            }
        }

        private async Task<NewMessageResponseBase<ResponseProvider>> CallPayBillGate(GatePayBillRequest paybill)
        {
            try
            {
                var response = await _grpcClient.GetClientCluster(GrpcServiceName.TopupGateway).SendAsync(paybill);
                _logger.LogInformation(
                    $"{paybill.TransCodeProvider}-{paybill.TransRef}-{paybill.ProviderCode} CallPayBillGate Return: {response?.ToJson()}");
                if (response == null)
                {
                    _logger.LogWarning(
                        $"{paybill.TransCodeProvider}-{paybill.TransRef}-{paybill.ProviderCode}-Can not get Response TopupGate");
                    await SendTeleMessage(new SendTeleTrasactionRequest
                    {
                        BotType = BotType.Dev,
                        TransRef = paybill.TransCodeProvider,
                        TransCode = paybill.TransRef,
                        BotMessageType = BotMessageType.Error,
                        Title = "Giao dịch lỗi - Hàm CallPayBillGate. Can not get Response TopupGate",
                        Message =
                            $"GD {paybill.TransRef}-{paybill.TransCodeProvider}\n GD chưa được xử lý thành công. Không có response từ TopupGw"
                    });
                    return new NewMessageResponseBase<ResponseProvider>()
                    {
                        ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_WaitForResult,
                            "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ"),
                        Results = new ResponseProvider()
                    };
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    $"{paybill.TransCodeProvider}-{paybill.TransRef}-{paybill.ProviderCode} CallPayBillGate Exception: {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
                await SendTeleMessage(new SendTeleTrasactionRequest
                {
                    BotType = BotType.Dev,
                    TransRef = paybill.TransCodeProvider,
                    TransCode = paybill.TransRef,
                    Title = $"GD có vấn đề!. Vui lòng kiểm tra trạng thái giao dịch",
                    Message =
                        $"GD {paybill.TransRef}-{paybill.TransCodeProvider}\nHàm CallTopupGate\n Lỗi:{ex.Message}",
                    BotMessageType = BotMessageType.Error
                });
                return new NewMessageResponseBase<ResponseProvider>()
                {
                    ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_WaitForResult,
                        "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ"),
                    Results = new ResponseProvider()
                };
            }
        }

        private async Task<(NewMessageResponseBase<ResponseProvider>, TransactionInfoDto)> CallPayBillPriority(
            GatePayBillRequest paybill,
            IReadOnlyCollection<ProviderConfig> providerCodes)
        {
            var info = new TransactionInfoDto
            {
                TransCodeProvider = paybill.TransCodeProvider,
                ProviderCode = paybill.ProviderCode
            };
            try
            {
                var result = await CallPayBillGate(paybill);
                _logger.LogInformation(
                    $"{paybill.TransCodeProvider}|{paybill.TransRef}|{paybill.ProviderCode} CallPayBillGate reponse : {result.ToJson()}");

                if (result != null && (result.ResponseStatus.ErrorCode != ResponseCodeConst.ResponseCode_WaitForResult
                                       && result.ResponseStatus.ErrorCode != ResponseCodeConst.ResponseCode_TimeOut
                                       && result.ResponseStatus.ErrorCode != ResponseCodeConst.ResponseCode_InProcessing
                                       && result.ResponseStatus.ErrorCode !=
                                       ResponseCodeConst.ResponseCode_TransactionError
                                       && result.ResponseStatus.ErrorCode != ResponseCodeConst.Success)
                                   && providerCodes.Count > 0)
                {
                    foreach (var item in providerCodes.OrderBy(c => c.Priority))
                    {
                        try
                        {
                            var config = item;

                            paybill.TransCodeProvider = string.IsNullOrEmpty(config.TransCodeConfig)
                                ? paybill.TransRef
                                : config.TransCodeConfig + "_" + paybill.TransRef;
                            paybill.ProviderCode = config.ProviderCode;

                            info.TransCodeProvider = paybill.TransCodeProvider;
                            info.ProviderCode = paybill.ProviderCode;

                            var update = await _saleService.SaleRequestUpdateStatusAsync(paybill.TransRef,
                                paybill.ProviderCode, SaleRequestStatus.InProcessing, paybill.TransCodeProvider);
                            if (!update)
                                break;

                            result = await CallPayBillGate(paybill);
                            _logger.LogInformation(
                                $"{paybill.TransCodeProvider}-{paybill.TransRef}-{paybill.ProviderCode} CallPayBillGate Return: {result.ToJson()}");


                            if (result.ResponseStatus.ErrorCode == ResponseCodeConst.Error)
                                continue;
                            break;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(
                                $"{paybill.TransCodeProvider}|{paybill.TransRef}|{paybill.ProviderCode} CallPayBillPriority Exception: {ex.Message}|{ex.StackTrace}|{ex.InnerException}");
                            await SendTeleMessage(new SendTeleTrasactionRequest
                            {
                                BotType = BotType.Dev,
                                TransRef = paybill.TransCodeProvider,
                                TransCode = paybill.TransRef,
                                Title = $"GD có vấn đề!. Vui lòng kiểm tra trạng thái giao dịch",
                                Message =
                                    $"GD {paybill.TransRef}-{paybill.TransCodeProvider}\nHàm CallPayBillPriority\nLỗi:{ex.Message}",
                                BotMessageType = BotMessageType.Error
                            });
                            break;
                        }
                    }
                }

                return (result, info);
            }
            catch (Exception e)
            {
                _logger.LogInformation(
                    $"{paybill.TransCodeProvider}|{paybill.TransRef}|{paybill.ProviderCode} CallCardPriority error : {e}");
                await SendTeleMessage(new SendTeleTrasactionRequest
                {
                    BotType = BotType.Dev,
                    TransRef = paybill.TransCodeProvider,
                    TransCode = paybill.TransRef,
                    Title = "GD PayBill có vấn đề!. Vui lòng kiểm tra trạng thái giao dịch",
                    Message =
                        $"Mã GD {paybill.TransRef}-{paybill.TransCodeProvider}\nHàm CallCardPriority\nLỗi:{e.Message}",
                    BotMessageType = BotMessageType.Error
                });
                var rs = new NewMessageResponseBase<ResponseProvider>
                {
                    ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_WaitForResult,
                        "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ"),
                    Results = new ResponseProvider()
                };
                return (rs, info);
            }
        }
    }
}