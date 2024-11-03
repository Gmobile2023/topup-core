using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HLS.Paygate.Gw.Domain.Entities;
using HLS.Paygate.Gw.Model.Commands;
using HLS.Paygate.Gw.Model.Dtos;
using HLS.Paygate.Shared;
using HLS.Paygate.Shared.ConfigDtos;
using HLS.Paygate.Shared.Dtos;
using HLS.Paygate.Shared.Helpers;
using HLS.Paygate.Shared.Utils;
using MassTransit;
using Microsoft.Extensions.Logging;
using Paygate.Contracts.Commands.Worker;
using Paygate.Contracts.Requests.Commons;
using Paygate.Discovery.Requests.Balance;
using Paygate.Discovery.Requests.Stocks;
using Paygate.Discovery.Requests.TopupGateways;
using Paygate.Discovery.Requests.Workers;
using ServiceStack;

namespace HLS.Paygate.Worker.Components.WorkerProcess
{
    public partial class WorkerProcess
    {
        public async Task<NewMessageReponseBase<List<CardRequestResponseDto>>> CardSaleRequest(
            WorkerPinCodeRequest request)
        {
            try
            {
                _logger.LogInformation("CardSaleRequest: " + request.ToJson());
                var response = new NewMessageReponseBase<List<CardRequestResponseDto>>();
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

                if (request.Quantity <= 0)
                {
                    response.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_00,
                        $"Số lượng yêu cầu không hợp lệ");
                    return response;
                }

                var saleRequest = request.ConvertTo<SaleRequestDto>();
                saleRequest.RequestDate = DateTime.Now;
                saleRequest.TransRef = request.TransCode;
                saleRequest.Amount = request.CardValue;
                var checkExist =
                    await _saleService.SaleRequestCheckAsync(saleRequest.TransRef, saleRequest.PartnerCode);
                _logger.LogInformation($"CheckSaleRequestDone:{saleRequest.TransCode}-{saleRequest.TransRef}");
                if (checkExist.ResponseCode != ResponseCodeConst.ResponseCode_TransactionNotFound)
                {
                    _logger.LogWarning($"{saleRequest.TransRef}-{saleRequest.PartnerCode} is duplicate request");
                    response.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_InProcessing,
                        "Giao dịch đang xử lý");
                    return response;
                }

                if (string.IsNullOrEmpty(saleRequest.PartnerCode))
                {
                    response.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_00,
                        $"Tài khoản không tồn tại");
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
                //Check cấu hình kênh
                var serviceConfiguration = await _externalServiceConnector.ServiceConfigurationAsync(request.TransCode,
                    saleRequest.PartnerCode,
                    saleRequest.ServiceCode,
                    saleRequest.CategoryCode, saleRequest.ProductCode, saleRequest.Channel == Channel.API);

                if (serviceConfiguration == null || serviceConfiguration.Count <= 0)
                {
                    _logger.LogInformation($"{saleRequest.TransRef}-ServiceConfiguration not config");
                    response.ResponseStatus = new ResponseStatusApi(
                        ResponseCodeConst.Error,
                        "Giao dịch không thành công");
                    return response;
                }

                _logger.LogInformation($"{saleRequest.TransRef}-ServiceConfiguration:{serviceConfiguration.Count}");
                //Discount
                var discount = await _externalServiceConnector.CheckProductDiscount(saleRequest.TransCode,
                    saleRequest.PartnerCode,
                    saleRequest.ProductCode, 0, saleRequest.Quantity);

                if (discount == null || discount.ProductValue <= 0 || discount.PaymentAmount <= 0)
                {
                    response.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error,
                        "Giao dịch không thành công");
                    return response;
                }

                _logger.LogInformation($"{saleRequest.TransRef}-GetDiscount return:{discount.ToJson()}");
                //Check hạn mức
                if (_workerConfig.IsCheckLimit && saleRequest.AgentType != AgentType.AgentApi)
                {
                    //check hạn mức
                    var checkLimit = await
                        _limitTransAccountService.CheckLimitAccount(saleRequest.StaffAccount,
                            saleRequest.PaymentAmount);
                    _logger.LogInformation($"{saleRequest.TransRef}-CheckLimit return:{checkLimit.ToJson()}");

                    if (checkLimit.ResponseCode != ResponseCodeConst.Success)
                    {
                        response.ResponseStatus = new ResponseStatusApi(checkLimit.ResponseCode,
                            checkLimit.ResponseMessage);
                        return response;
                    }

                    var checkLimitProduct = await _checkLimit.CheckLimitProductPerDay(saleRequest.PartnerCode,
                        saleRequest.ProductCode,
                        discount.ProductValue, saleRequest.Quantity, saleRequest.TransCode);
                    _logger.LogInformation(
                        $"{saleRequest.TransRef}-CheckLimitProductPerDay return:{checkLimitProduct.ToJson()}");
                    if (checkLimitProduct.ResponseCode != ResponseCodeConst.Success)
                    {
                        response.ResponseStatus = new ResponseStatusApi(checkLimitProduct.ResponseCode,
                            checkLimitProduct.ResponseMessage);
                        return response;
                    }
                }

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
                saleRequest.DiscountAmount = discount.DiscountAmount;

                var serviceConfig = serviceConfiguration.OrderBy(c => c.Priority).First();
                saleRequest.Provider = serviceConfig.ProviderCode;
                saleRequest.ParentProvider = serviceConfig.ParentProvider;

                if (!string.IsNullOrEmpty(serviceConfig.TransCodeConfig))
                {
                    saleRequest.ProviderTransCode = serviceConfig.TransCodeConfig + "_" + saleRequest.ProviderTransCode;
                }

                // var updateSaleRequest = await _saleService.SaleRequestUpdateAsync(saleRequest);
                // if (updateSaleRequest == null)
                // {
                //     _logger.LogInformation($"{saleRequest.TransCode}-{saleRequest.TransRef}-Update SaleReuest fail");
                //     response.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_00,
                //         "Tiếp nhận giao dịch không thành công");
                //     return response;
                // }
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

                var paymentResponse = await _grpcClient.GetClientCluster(GrpcServiceName.Balance).SendAsync(
                    new BalancePaymentRequest()
                    {
                        AccountCode = saleRequest.PartnerCode,
                        PaymentAmount = saleRequest.PaymentAmount,
                        CurrencyCode = CurrencyCode.VND.ToString("G"),
                        TransRef = saleRequest.TransRef,
                        TransCode = saleRequest.TransCode,
                        TransNote = $"Thanh toán cho giao dịch: {saleRequest.TransRef}"
                    });
                _logger.LogInformation(
                    $"PaymentResponse:{saleRequest.TransCode}-{saleRequest.TransRef}-{paymentResponse.ToJson()}");
                decimal balance = 0;
                if (paymentResponse.ResponseStatus.ErrorCode == ResponseCodeConst.Success)
                {
                    saleRequest.PaymentAmount = saleRequest.PaymentAmount;
                    saleRequest.PaymentTransCode = paymentResponse.Results.TransactionCode;
                    saleRequest.Status = SaleRequestStatus.Paid;
                    var cardUpdate = await _saleService.SaleRequestUpdateAsync(saleRequest);
                    balance = paymentResponse.Results.SrcBalance;

                    //Call api Lấy thẻ trả về kết quả
                    if (cardUpdate != null)
                    {
                        _logger.LogInformation(
                            $"Update topup item payment success: {cardUpdate.TransCode}-{cardUpdate.TransRef}-{cardUpdate.Status}");

                        var providerTwo =
                            (from x in serviceConfiguration.Where(c =>
                                    c.ProviderCode != serviceConfig.ProviderCode)
                             select new ProviderConfig
                             {
                                 ProviderCode = x.ProviderCode,
                                 Priority = x.Priority,
                                 TransCodeConfig = x.TransCodeConfig,
                                 ProviderMaxWaitingTimeout = x.ProviderMaxWaitingTimeout,
                                 ProviderSetTransactionTimeout = x.ProviderSetTransactionTimeout,
                                 StatusResponseWhenJustReceived = x.StatusResponseWhenJustReceived,
                                 IsEnableResponseWhenJustReceived = x.IsEnableResponseWhenJustReceived,
                                 WaitingTimeResponseWhenJustReceived = x.WaitingTimeResponseWhenJustReceived,
                             }).ToList();

                        var requestCard = new GateCardRequest
                        {
                            Quantity = saleRequest.Quantity,
                            Amount = saleRequest.Price,
                            ProductCode = saleRequest.ProductCode,
                            TransRef = saleRequest.TransCode,
                            ProviderCode = serviceConfig.ProviderCode,
                            TransCodeProvider = saleRequest.ProviderTransCode,
                            Vendor = saleRequest.ProductCode.Split('_')[0],
                            RequestDate = saleRequest.CreatedTime,
                            ReferenceCode = saleRequest.TransRef
                        };

                        var (newMessageReponseBase, transactionInfoDto) =
                            await CallCardPriority(requestCard, providerTwo);
                        response = newMessageReponseBase;
                        var providerCode = transactionInfoDto.ProviderCode;
                        var transCodeProvider = transactionInfoDto.TransCodeProvider;
                        if (!string.IsNullOrEmpty(providerCode))
                            saleRequest.Provider = providerCode;
                        if (!string.IsNullOrEmpty(transCodeProvider))
                            saleRequest.ProviderTransCode = transCodeProvider;


                        //nếu thành công nhưng vì lý do nào đó thẻ null, thì để trạng thái là chưa có KQ
                        if (response.ResponseStatus.ErrorCode == ResponseCodeConst.Success && response.Results is not { Count: > 0 })
                        {
                            _logger.LogWarning($"{saleRequest.TransRef}-{saleRequest.TransCode}-can not get card_info=> ReCreate errorCode");
                            await SendTeleMessage(new SendTeleTrasactionRequest
                            {
                                BotType = BotType.Dev,
                                TransRef = saleRequest.TransRef,
                                TransCode = saleRequest.TransCode,
                                Title = "GD Mua mã thẻ có vấn đề!. Không lấy được thông tin thẻ",
                                Message = $"Mã GD {saleRequest.TransRef}-{saleRequest.TransCode}-{saleRequest.Provider}",
                                BotMessageType = BotMessageType.Error
                            });
                            response.ResponseStatus.ErrorCode = ResponseCodeConst.ResponseCode_WaitForResult;
                        }

                        switch (response.ResponseStatus.ErrorCode)
                        {
                            case ResponseCodeConst.Success:
                                {
                                    saleRequest.Status = SaleRequestStatus.Success;
                                    await _saleService.SaleRequestUpdateStatusAsync(saleRequest.TransCode,
                                        saleRequest.Provider, SaleRequestStatus.Success, transCodeProvider);
                                    response.ResponseStatus = new ResponseStatusApi(
                                        response.ResponseStatus.ErrorCode,
                                        $"Bạn đã lấy thẻ thành công số tiền {saleRequest.Amount:0}. Mã GD {saleRequest.TransCode}");

                                    try
                                    {
                                        var cards = response.Results;
                                        var lstTopupItem = (from item in cards
                                                            select new SaleItemDto
                                                            {
                                                                Amount = Convert.ToInt32(item.CardValue),
                                                                Serial = item.Serial,
                                                                CardExpiredDate = item.ExpiredDate,
                                                                Status = SaleRequestStatus.Success,
                                                                Vendor = item.CardValue,
                                                                CardCode = item.CardCode.EncryptTripDes(),
                                                                CardValue = Convert.ToInt32(item.CardValue),
                                                                ServiceCode = saleRequest.ServiceCode,
                                                                PartnerCode = saleRequest.PartnerCode,
                                                                SaleType = "PINCODE",
                                                                SaleTransCode = saleRequest.TransCode,
                                                                CreatedTime = DateTime.Now
                                                            }).ToList();
                                        await _saleService.SaleItemListCreateAsync(lstTopupItem);
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogError(
                                            $"{saleRequest.TransRef}-{saleRequest.TransCode}-Insert detail card error:{ex}");
                                        await SendTeleMessage(new SendTeleTrasactionRequest
                                        {
                                            BotType = BotType.Dev,
                                            TransRef = saleRequest.TransRef,
                                            TransCode = saleRequest.TransCode,
                                            Title = "GD Mua mã thẻ có vấn đề!. Không lưu được thông tin thẻ",
                                            Message =
                                                $"Mã GD {saleRequest.TransRef}-{saleRequest.TransCode}-{saleRequest.Provider}-{ex.Message}",
                                            BotMessageType = BotMessageType.Error
                                        });
                                    }

                                    if (!string.IsNullOrEmpty(saleRequest.ParentCode) &&
                                        saleRequest.AgentType == AgentType.SubAgent)
                                    {
                                        await _saleService.CommissionRequest(saleRequest);
                                    }

                                    break;
                                }
                            case ResponseCodeConst.ResponseCode_WaitForResult:
                            case ResponseCodeConst.ResponseCode_TimeOut:
                            case ResponseCodeConst.ResponseCode_InProcessing:
                                response.ResponseStatus = new ResponseStatusApi(
                                    response.ResponseStatus.ErrorCode,
                                    "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ");
                                saleRequest.Status = SaleRequestStatus.WaitForResult;
                                _logger.LogWarning(
                                    $"CardPending: {saleRequest.TransCode}-{saleRequest.TransRef}");
                                await _saleService.SaleRequestUpdateStatusAsync(saleRequest.TransCode,
                                    saleRequest.Provider, SaleRequestStatus.WaitForResult, transCodeProvider);
                                break;
                            default:
                                _logger.LogWarning(
                                    $"CardRefund: {saleRequest.TransCode}-{saleRequest.TransRef}");
                                await _bus.Publish<PaymentCancelCommand>(new
                                {
                                    CorrelationId = Guid.NewGuid(),
                                    saleRequest.TransCode,
                                    saleRequest.PaymentTransCode,
                                    TransNote = $"Hoàn tiền cho giao dịch thanh toán: {saleRequest.TransRef}",
                                    RevertAmount = saleRequest.PaymentAmount,
                                    AccountCode = saleRequest.PartnerCode,
                                    Timestamp = DateTime.Now
                                });

                                saleRequest.Status = SaleRequestStatus.Failed;
                                await _saleService.SaleRequestUpdateStatusAsync(saleRequest.TransCode,
                                    saleRequest.Provider, SaleRequestStatus.Failed, transCodeProvider);
                                break;
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
                        //Hoàn tiền
                        await _bus.Publish<PaymentCancelCommand>(new
                        {
                            CorrelationId = Guid.NewGuid(),
                            saleRequest.TransCode,
                            saleRequest.PaymentTransCode,
                            TransNote = $"Hoàn tiền cho giao dịch thanh toán: {saleRequest.TransRef}",
                            RevertAmount = saleRequest.PaymentAmount,
                            AccountCode = saleRequest.PartnerCode,
                            Timestamp = DateTime.Now
                        });
                        saleRequest.Status = SaleRequestStatus.Failed;
                        await _saleService.SaleRequestUpdateStatusAsync(saleRequest.TransCode,
                            string.Empty,
                            SaleRequestStatus.Failed);

                        // await _saleService.PublishConsumerReport(new SaleReponseDto()
                        // {
                        //     NextStep = 1,
                        //     Sale = saleRequest,
                        //     Status = SaleRequestStatus.Failed,
                        // });
                    }
                }
                else
                {
                    saleRequest.Status = SaleRequestStatus.Failed;
                    _logger.LogInformation(
                        $"Payment fail. {saleRequest.TransCode}-{saleRequest.TransRef} - {paymentResponse.ResponseStatus.ErrorCode}-{paymentResponse.ResponseStatus.Message}");
                    await _saleService.SaleRequestUpdateStatusAsync(saleRequest.TransCode, string.Empty,
                        SaleRequestStatus.Failed);
                    if (paymentResponse.ResponseStatus.ErrorCode == ResponseCodeConst.ResponseCode_Balance_Not_Enough)
                    {
                        response.ResponseStatus = new ResponseStatusApi(
                            ResponseCodeConst.ResponseCode_Balance_Not_Enough,
                            "Thanh toán cho giao dịch lỗi. Vui lòng kiểm tra lại số dư");
                    }
                    else
                    {
                        //chỗ này gửi cảnh báo. theo dõi nguyên nhân
                        response.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error,
                            "Giao dịch không thành công. Vui lòng thử lại sau");
                    }
                }

                await _saleService.PublishConsumerReport(new SaleReponseDto()
                {
                    NextStep = 0,
                    Sale = saleRequest,
                    Status = saleRequest.Status,
                    Balance = balance
                });

                return response;
            }
            catch (Exception e)
            {
                _logger.LogError($"{request.TransCode}-CardSaleRequest: " + e);
                await SendTeleMessage(new SendTeleTrasactionRequest
                {
                    BotType = BotType.Dev,
                    TransRef = request.TransCode,
                    TransCode = request.TransCode,
                    Title = "GD có vấn đề!. Vui lòng kiểm tra trạng thái giao dịch",
                    Message = $"Mã GD {request.TransCode}\nHàm CardSaleRequest\nLỗi:{e.Message}",
                    BotMessageType = BotMessageType.Error
                });
                return new NewMessageReponseBase<List<CardRequestResponseDto>>()
                {
                    ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_WaitForResult,
                        "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ. Xin vui lòng thử lại sau")
                };
            }
        }


        private async Task<NewMessageReponseBase<List<CardRequestResponseDto>>> CallCardGate(GateCardRequest request)
        {
            try
            {
                var response = await _grpcClient.GetClientCluster(GrpcServiceName.TopupGateway).SendAsync(request);
                string reponseView = "";
                if (response != null && response.Results != null)
                {
                    reponseView = response.Results.Select(c => new { c.CardValue, c.Serial }).ToList().ToJson();
                }

                _logger.LogInformation(
                    $"{request.TransCodeProvider}-{request.TransRef}-{request.ProviderCode} CallCardGate Return: ResponseStatus= {response?.ResponseStatus?.ToJson()}|CardReponse= {reponseView}");
                if (response == null)
                {
                    _logger.LogWarning(
                        $"{request.TransCodeProvider}-{request.TransRef}-{request.ProviderCode}-Can not get Response TopupGate");
                    await SendTeleMessage(new SendTeleTrasactionRequest
                    {
                        BotType = BotType.Dev,
                        TransRef = request.TransCodeProvider,
                        TransCode = request.TransRef,
                        Title = "Giao dịch mua mã thẻ lỗi - Hàm CallCardGate.Can not get Response TopupGate",
                        Message =
                            $"GD {request.TransRef}-{request.TransCodeProvider}\nGD chưa được xử lý thành công. Không có response từ TopupGw",
                        BotMessageType = BotMessageType.Error
                    });
                    return new NewMessageReponseBase<List<CardRequestResponseDto>>()
                    {
                        ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_WaitForResult,
                            "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ")
                    };
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    $"{request.TransCodeProvider}-{request.TransRef}-{request.ProviderCode} CallCardGate Exception: {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
                await SendTeleMessage(new SendTeleTrasactionRequest
                {
                    BotType = BotType.Dev,
                    TransRef = request.TransCodeProvider,
                    TransCode = request.TransRef,
                    Title = $"GD mua mã thẻ có vấn đề!. Vui lòng kiểm tra trạng thái giao dịch",
                    Message =
                        $"Mã GD {request.TransRef}-{request.TransCodeProvider}\nHàm CallCardGate\nLỗi:{ex.Message}",
                    BotMessageType = BotMessageType.Error
                });

                return new NewMessageReponseBase<List<CardRequestResponseDto>>()
                {
                    ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_WaitForResult,
                        "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ"),
                };
            }
        }

        private async Task<(NewMessageReponseBase<List<CardRequestResponseDto>>, TransactionInfoDto)> CallCardPriority(
            GateCardRequest request,
            List<ProviderConfig> providerCodes)
        {
            if (request.ProviderCode.StartsWith(ProviderConst.VINNET))
                request.TransCodeProvider = Guid.NewGuid().ToString();

            var info = new TransactionInfoDto
            {
                TransCodeProvider = request.TransCodeProvider,
                ProviderCode = request.ProviderCode
            };
            try
            {
                var result = await CallCardGate(request);
                _logger.LogInformation(
                    $"{request.TransCodeProvider}|{request.TransRef}|{request.ProviderCode} CallCardPriority reponse : {result?.ResponseStatus?.ToJson()}");
                if (result != null && (result.ResponseStatus.ErrorCode != ResponseCodeConst.ResponseCode_WaitForResult
                                       && result.ResponseStatus.ErrorCode != ResponseCodeConst.ResponseCode_TimeOut
                                       && result.ResponseStatus.ErrorCode != ResponseCodeConst.ResponseCode_InProcessing
                                       && result.ResponseStatus.ErrorCode != ResponseCodeConst.Success)
                                   && providerCodes.Count > 0)
                {
                    foreach (var item in providerCodes.OrderBy(c => c.Priority))
                    {
                        try
                        {
                            var config = item;
                            if (config.ProviderCode.StartsWith(ProviderConst.VINNET))
                                request.TransCodeProvider = Guid.NewGuid().ToString();
                            else
                            {
                                request.TransCodeProvider = string.IsNullOrEmpty(config.TransCodeConfig)
                                    ? request.TransRef
                                    : config.TransCodeConfig + "_" + request.TransRef;
                            }
                            request.ProviderCode = config.ProviderCode;
                            info.TransCodeProvider = request.TransCodeProvider;
                            info.ProviderCode = request.ProviderCode;
                            var update = await _saleService.SaleRequestUpdateStatusAsync(request.TransRef,
                                request.ProviderCode, SaleRequestStatus.InProcessing, request.TransCodeProvider);
                            if (!update)
                                break;

                          

                            result = await CallCardGate(request);
                            _logger.LogInformation(
                                $"{request.TransCodeProvider}-{request.TransRef}-{request.ProviderCode} CallCardPriority Return: {result?.ResponseStatus?.ToJson()}");
                            if (result.ResponseStatus.ErrorCode == ResponseCodeConst.Error)
                                continue;
                            break;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(
                                $"{request.TransCodeProvider}-{request.TransRef}-{request.ProviderCode} CallCardPriority Exception: {ex.Message}|{ex.StackTrace}|{ex.InnerException}");

                            await SendTeleMessage(new SendTeleTrasactionRequest
                            {
                                BotType = BotType.Dev,
                                TransRef = request.TransCodeProvider,
                                TransCode = request.TransRef,
                                Title = $"GD mua mã thẻ có vấn đề!. Vui lòng kiểm tra trạng thái giao dịch",
                                Message =
                                    $"Mã GD {request.TransRef}-{request.TransCodeProvider}\nHàm CallCardPriority.\nLỗi:{ex.Message}",
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
                    $"{request.TransCodeProvider}|{request.TransRef}|{request.ProviderCode} CallCardPriority error : {e}");
                await SendTeleMessage(new SendTeleTrasactionRequest
                {
                    BotType = BotType.Dev,
                    TransRef = request.TransCodeProvider,
                    TransCode = request.TransRef,
                    Title = "GD Mua mã thẻ có vấn đề!. Vui lòng kiểm tra trạng thái giao dịch",
                    Message =
                        $"Mã GD {request.TransRef}-{request.TransCodeProvider}\nHàm CallCardPriority\nLỗi:{e.Message}",
                    BotMessageType = BotMessageType.Error
                });
                var rs = new NewMessageReponseBase<List<CardRequestResponseDto>>
                {
                    ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_WaitForResult,
                        "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ")
                };
                return (rs, info);
            }
        }
    }
}