using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Topup.Gw.Model.Commands;
using Topup.Gw.Model.Dtos;
using Topup.Shared;
using Topup.Shared.AbpConnector;
using Topup.Shared.ConfigDtos;
using Topup.Shared.Dtos;
using Topup.Shared.Helpers;
using MassTransit;
using Microsoft.Extensions.Logging;
using Topup.Contracts.Commands.Worker;
using Topup.Contracts.Requests.Commons;
using Topup.Discovery.Requests.Balance;
using Topup.Discovery.Requests.TopupGateways;
using Topup.Discovery.Requests.Workers;
using ServiceStack;
using Topup.Worker.Components.TaskQueues;

namespace Topup.Worker.Components.WorkerProcess
{
    public partial class WorkerProcess
    {
        private readonly IBackgroundTaskQueue _queue;

        public async Task<NewMessageResponseBase<WorkerResult>> TopupRequest(WorkerTopupRequest request,
            ConsumeContext<TopupRequestCommand> context = null)
        {
            var slowTopup = false;
            const bool isRefund = true;
            var checkPhone = new NewMessageResponseBase<string>
            {
                ResponseStatus = new ResponseStatusApi("NOT_CHECK")
            };
            try
            {
                var response = new NewMessageResponseBase<WorkerResult>
                {
                    Results = new WorkerResult()
                };
                _logger.LogInformation("TopupRequest: {Request}", request.ToJson());
                if ((DateTime.Now - request.RequestDate).TotalSeconds >= _workerConfig.TimeOutProcess)
                {
                    _logger.LogWarning("{TransCode}-{Partner} Transaction timeout over setting", request.TransCode,
                        request.PartnerCode);
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

                // var partner = await _systemService.GetPartnerCache(request.PartnerCode);
                // if (partner == null)
                // {
                //     _logger.LogError($"Tài khoản không tồn tại:{request.ToJson()}");
                //     response.ResponseStatus =
                //         new ResponseStatusApi(ResponseCodeConst.Error, "Giao dịch không thành công");
                //     return response;
                // }

                // heck ProductCode Lấy từ Cache từ Sale Service Nếu đối tác nạp bẳng kênh API

                var productInfo = await _externalServiceConnector.GetProductInfo(request.TransCode,
                    request.CategoryCode, request.ProductCode, request.Amount);

                if (productInfo is not { Status: 1 })
                {
                    response.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error,
                        $"Giao dịch không thành công");
                    return response;
                }

                if ((productInfo.MinAmount != null && productInfo.MinAmount > request.Amount) ||
                    (productInfo.MaxAmount != null && productInfo.MaxAmount < request.Amount))
                {
                    response.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error,
                        $"Mệnh giá nạp không hợp lệ");
                    return response;
                }

                //var productInfo = await _cacheManager.GetEntity<ProductInfoDto>($"PayGates_ProductInfos:Items:{request.PartnerCode}:{request.ProductCode}:{request.TransCode}-{request.Amount}");

                //await _cacheManager.DeleteEntity($"PayGates_ProductInfos:Items:{request.PartnerCode}:{request.ProductCode}:{request.TransCode}-{request.Amount}");


                var saleRequest = request.ConvertTo<SaleRequestDto>();

                if (productInfo.ProductCode != request.ProductCode || request.Amount == productInfo.MinAmount)
                {
                    _logger.LogInformation($"{request.CategoryCode}-{request.Amount} -> Mệnh giá lẻ đổi product code");
                    saleRequest.ProductCode = productInfo.ProductCode;
                    saleRequest.Quantity = (int)(request.Amount / productInfo.ProductValue);
                    saleRequest.Price = productInfo.ProductValue;
                }


                saleRequest.RequestDate = DateTime.Now;
                saleRequest.TransRef = request.TransCode;

                var checkExist =
                    await _saleService.SaleRequestCheckAsync(saleRequest.TransRef, saleRequest.PartnerCode);

                _logger.LogInformation("CheckSaleRequestDone: {TransCode} - {TransRef}", saleRequest.TransCode,
                    saleRequest.TransRef);

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
                saleRequest.ReceiverType = ReceiverType.Default;
                saleRequest.IsDiscountPaid = false;

                if (_workerConfig.IsCheckLimit && saleRequest.Channel != Channel.API)
                {
                    //check hạn mức
                    var checkLimit = await
                        _limitTransAccountService.CheckLimitAccount(
                            saleRequest.StaffAccount, saleRequest.PaymentAmount);
                    _logger.LogInformation("{TransRef} - CheckLimit return: {Limit}", saleRequest.TransRef,
                        checkLimit.ToJson());

                    if (checkLimit.ResponseCode != ResponseCodeConst.Success)
                    {
                        response.ResponseStatus = new ResponseStatusApi(checkLimit.ResponseCode,
                            checkLimit.ResponseMessage);
                        return response;
                    }

                    var checkLimitProduct = await
                        _checkLimit.CheckLimitProductPerDay(saleRequest.PartnerCode, saleRequest.ProductCode,
                            saleRequest.Amount, saleRequest.Quantity, saleRequest.TransCode);
                    _logger.LogInformation("{TransRef} - CheckLimitProductPerDay return: {Limit}", saleRequest.TransRef,
                        checkLimitProduct.ToJson());
                    if (checkLimitProduct.ResponseCode != ResponseCodeConst.Success)
                    {
                        response.ResponseStatus = new ResponseStatusApi(checkLimitProduct.ResponseCode,
                            checkLimitProduct.ResponseMessage);
                        return response;
                    }
                }

                //Check kênh trước update
                _logger.LogInformation("{TransRef} - GetServiceConfiguration", saleRequest.TransRef);
                var serviceConfiguration = await _externalServiceConnector.ServiceConfigurationAsync(request.TransCode,
                    saleRequest.PartnerCode,
                    saleRequest.ServiceCode,
                    saleRequest.CategoryCode, saleRequest.ProductCode,
                    _workerConfig.IsTest || saleRequest.Channel == Channel.API);
                if (serviceConfiguration == null || serviceConfiguration.Count <= 0)
                {
                    _logger.LogInformation($"{saleRequest.TransRef}-ServiceConfiguration not config");
                    response.ResponseStatus = new ResponseStatusApi(
                        ResponseCodeConst.Error,
                        "Giao dịch không thành công");
                    return response;
                }

                _logger.LogInformation("{TransRef} - GetServiceConfiguration success: {Count} ==> {info}",
                    saleRequest.TransRef,
                    serviceConfiguration.Count, serviceConfiguration.Select(x => x.ProviderCode).ToJson());
                saleRequest = await _saleService.SaleRequestCreateAsync(saleRequest);
                if (saleRequest == null)
                {
                    response.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.ResponseCode_00,
                        "Tiếp nhận giao dịch không thành công");
                    return response;
                }

                _logger.LogInformation($"Create topup request success: {saleRequest.TransCode}-{saleRequest.TransRef}");

                #region Xử lý phần check loại thuê bao

                if ((request.IsCheckReceiverType || request.IsNoneDiscount) &&
                    saleRequest.CategoryCode == CategoryConst.VTE_TOPUP)
                {
                    var getVendor = TelcoHepper.GetVendorTrans(saleRequest.ServiceCode, saleRequest.ProductCode);
                    saleRequest.Vendor = getVendor;
                    checkPhone = await ValidateTelco(saleRequest.TransCode, saleRequest.ReceiverInfo,
                        saleRequest.Vendor);
                    _logger.LogInformation(
                        "{TransCode}-{TransRef} - CheckReceiverType return: {Return}", saleRequest.TransCode,
                        saleRequest.TransRef,
                        checkPhone.ToJson());
                    if (checkPhone.ResponseStatus.ErrorCode == ResponseCodeConst.Success &&
                        checkPhone.Results is "TS" or "TT")
                    {
                        saleRequest.IsCheckReceiverTypeSuccess = true;
                        response.Results.ReceiverType = checkPhone.Results; //nếu check oke thì trả thêm ra cho đối tác
                        if (checkPhone.Results == "TS")
                        {
                            _logger.LogInformation("{TransCode}-{TransRef} - IsDiscountPaid", saleRequest.TransCode,
                                saleRequest.TransRef);
                            saleRequest.ReceiverType = ReceiverType.PostPaid;
                        }
                        else
                        {
                            saleRequest.ReceiverType = ReceiverType.PrePaid;
                        }
                    }
                    else
                    {
                        //nếu không check được thì lấy mặc định
                        var receiverType = string.Empty;
                        if (!string.IsNullOrEmpty(request.DefaultReceiverType))
                        {
                            receiverType = request.DefaultReceiverType == "TS"
                                ? ReceiverType.PostPaid
                                : ReceiverType.PrePaid;
                        }

                        response.Results.ReceiverType = !string.IsNullOrEmpty(request.DefaultReceiverType)
                            ? request.DefaultReceiverType
                            : null;
                        saleRequest.ReceiverType = receiverType;
                    }

                    if (request.IsNoneDiscount) //Nếu bật k tính ck cho thuê bao TS
                    {
                        //Theo loại thuê bao trả  ra cho KH, nếu TT thì tính ck còn nếu TS thì k tính
                        saleRequest.IsDiscountPaid = !string.IsNullOrEmpty(response.Results.ReceiverType) &&
                                                     response.Results.ReceiverType == "TS";
                    }
                }

                if (saleRequest.IsCheckReceiverTypeSuccess &&
                    request
                        .IsCheckAllowTopupReceiverType) //nếu check được chính xác loại thuê bao,và bật xử lý gd ts cho khách
                {
                    //bỏ qua các kênh chỉ set cho chạy trả trước hoặc trả sau theo cấu hình
                    serviceConfiguration = serviceConfiguration.Where(x =>
                        x.AllowTopupReceiverType == null || string.IsNullOrEmpty(x.AllowTopupReceiverType) ||
                        x.AllowTopupReceiverType == saleRequest.ReceiverType).ToList();
                    _logger.LogInformation(
                        "{TransCode} - {TransRef} - ReCreateServiceConfiguration for CheckReceiverType : {Config}",
                        saleRequest.TransCode,
                        saleRequest.TransRef, serviceConfiguration.Select(x => x.ProviderCode.ToJson()));
                    if (serviceConfiguration.Count <= 0)
                    {
                        _logger.LogError(
                            $"{saleRequest.TransCode}-{saleRequest.TransRef}-Không có thông tin cấu hình kênh");
                        response.ResponseStatus =
                            new ResponseStatusApi(ResponseCodeConst.Error, "Giao dịch lỗi");
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
                }

                #endregion

                if (!saleRequest.IsDiscountPaid) //nếu ck sau thì k lấy ck nữa
                {
                    var discount = await _externalServiceConnector.CheckProductDiscount(saleRequest.TransCode,
                        saleRequest.PartnerCode,
                        saleRequest.ProductCode, quantity: saleRequest.Quantity);
                    if (discount == null || discount.ProductValue <= 0 || discount.PaymentAmount <= 0)
                    {
                        response.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error,
                            "Giao dịch không thành công");
                        saleRequest.Status = SaleRequestStatus.Failed;
                        await _saleService.SaleRequestUpdateAsync(saleRequest);
                        await _saleService.PublishConsumerReport(new SaleReponseDto
                        {
                            NextStep = 0,
                            Sale = saleRequest,
                            Status = saleRequest.Status
                        });
                        return response;
                    }

                    _logger.LogInformation(
                        $"Get discount done: {saleRequest.TransCode} - {saleRequest.TransRef} - {discount.ToJson()}");
                    // saleRequest.Amount = discount.ProductValue;
                    saleRequest.DiscountRate = discount.DiscountValue;
                    saleRequest.FixAmount = discount.FixAmount;
                    saleRequest.DiscountAmount = discount.DiscountAmount;
                    saleRequest.PaymentAmount = discount.PaymentAmount;
                }
                else
                {
                    saleRequest.PaymentAmount = saleRequest.Amount;
                }

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

                response.Results.PaymentAmount = saleRequest.PaymentAmount;
                response.Results.TransCode = saleRequest.TransCode;
                response.Results.TransRef = saleRequest.TransRef;
                response.Results.Discount = saleRequest.DiscountAmount ?? 0;

                #region Xử lý luồng nạp chậm

                var isSlow = IsSlowTrans(serviceConfiguration);
                if (saleRequest.CategoryCode == CategoryConst.VTE_TOPUP && isSlow && context != null)
                {
                    //Nếu off check số điện thoại thì xử lý luồng nạp chậm theo cấu hình như bình thường
                    if (!request.IsCheckPhone)
                    {
                        _logger.LogInformation("Topup slow not check phone: {TransCode} - {TransRef}",
                            saleRequest.TransCode, saleRequest.TransRef);
                        slowTopup = true;
                        saleRequest.SaleType = SaleType.Slow;
                    }
                    else //Ngược lại trước khi sang bên NCC nạp chậm thì check số điện thoại trước
                    {
                        _logger.LogInformation("{TransCode} - CheckPhone to Topup Slow: {Check}", saleRequest.TransCode,
                            checkPhone.ToJson());
                        if (checkPhone.ResponseStatus.ErrorCode == "NOT_CHECK") //Nếu chưa check thì check lại
                        {
                            var getVendor =
                                TelcoHepper.GetVendorTrans(saleRequest.ServiceCode, saleRequest.ProductCode);
                            saleRequest.Vendor = getVendor;
                            checkPhone = await ValidateTelco(saleRequest.TransCode, saleRequest.ReceiverInfo,
                                saleRequest.Vendor);
                        }

                        if (checkPhone.ResponseStatus.ErrorCode == ResponseCodeConst.Success)
                        {
                            slowTopup = true;
                            saleRequest.SaleType = SaleType.Slow;
                        }
                        else
                        {
                            //K check dc phone thì bỏ qua kênh nạp chậm
                            serviceConfiguration = serviceConfiguration.Where(x => x.IsSlowTrans == false).ToList();
                            _logger.LogInformation(
                                "{TransCode} - {TransRef} - ReCreateServiceConfiguration: {Config}",
                                saleRequest.TransCode,
                                saleRequest.TransRef, serviceConfiguration.Count);
                            if (serviceConfiguration.Count <= 0)
                            {
                                response.ResponseStatus =
                                    new ResponseStatusApi(ResponseCodeConst.Error, "Giao dịch lỗi");
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
                        }
                    }
                }

                #endregion

                var serviceConfig = serviceConfiguration.OrderBy(c => c.Priority).First();
                saleRequest.Provider = serviceConfig.ProviderCode;
                saleRequest.ParentProvider = serviceConfig.ParentProvider;
                if (!string.IsNullOrEmpty(serviceConfig.TransCodeConfig))
                {
                    saleRequest.ProviderTransCode = serviceConfig.TransCodeConfig + "_" + saleRequest.ProviderTransCode;
                }

                #region Payment

                var paymentResponse = await _grpcClient.GetClientCluster(GrpcServiceName.Balance).SendAsync(
                    new BalancePaymentRequest()
                    {
                        AccountCode = saleRequest.PartnerCode,
                        PaymentAmount = saleRequest.PaymentAmount,
                        CurrencyCode = saleRequest.CurrencyCode,
                        TransRef = saleRequest.TransRef,
                        TransCode = saleRequest.TransCode,
                        TransNote = $"Thanh toán cho giao dịch: {saleRequest.TransRef}"
                    });

                #endregion

                decimal balance;
                _logger.LogInformation(
                    $"Request Balance return: {paymentResponse.ResponseStatus.ErrorCode}-{paymentResponse.ResponseStatus.Message} {saleRequest.TransCode}-{saleRequest.TransRef}");
                if (paymentResponse.ResponseStatus.ErrorCode != ResponseCodeConst.Success)
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
                        //chỗ này gửi cảnh báo. theo dõi nguyên nhân. Gunner
                        response.ResponseStatus = new ResponseStatusApi(ResponseCodeConst.Error,
                            "Giao dịch không thành công. Vui lòng thử lại sau");
                    }
                }
                else
                {
                    saleRequest.Status = SaleRequestStatus.Paid;
                    saleRequest.PaymentTransCode = paymentResponse.Results.TransactionCode;
                    var topupUpdate = await _saleService.SaleRequestUpdateAsync(saleRequest);
                    balance = paymentResponse.Results.SrcBalance;

                    if (topupUpdate != null)
                    {
                        _logger.LogInformation(
                            $"Update topup item payment success: {topupUpdate.TransCode}-{topupUpdate.TransRef}-{topupUpdate.Status}");


                        if (slowTopup && serviceConfig.IsEnableResponseWhenJustReceived)
                        {
                            _logger.LogInformation(
                                $"{topupUpdate.TransCode}-{topupUpdate.TransRef} Nạp chậm, trả kết quả ngay sau {serviceConfig.WaitingTimeResponseWhenJustReceived} s: Mã lỗi : {serviceConfig.StatusResponseWhenJustReceived}");

                            await _queue.QueueBackgroundWorkItemAsync(async _ =>
                            {
                                await TopupAsync(request, response, saleRequest, isRefund, serviceConfiguration,
                                    serviceConfig.ProviderCode, balance, CancellationToken.None, slowTopup);
                            });

                            await Task.Delay(TimeSpan.FromSeconds(serviceConfig.WaitingTimeResponseWhenJustReceived));

                            response.Results.Responsed = true;
                            var message = serviceConfig.StatusResponseWhenJustReceived == ResponseCode.Success
                                ? $"Bạn đã nạp tiền thành công cho số {saleRequest.ReceiverInfo} số tiền {saleRequest.Amount:0}. Mã GD {saleRequest.TransCode}"
                                : "Giao dịch đã được tiếp nhận";
                            await context.RespondAsync<NewMessageResponseBase<WorkerResult>>(new
                            {
                                Id = context.Message.CorrelationId,
                                ReceiveTime = DateTime.Now,
                                ResponseCode = serviceConfig.StatusResponseWhenJustReceived,
                                response.Results,
                                ResponseStatus = new ResponseStatusApi(serviceConfig.StatusResponseWhenJustReceived,
                                    message)
                            });
                            _logger.LogInformation(
                                $"SlowTrans responsed => Process partial topup: {topupUpdate.TransCode}-{topupUpdate.TransRef}");

                            return response;
                        }

                        return await TopupAsync(request, response, saleRequest, isRefund,
                            serviceConfiguration, serviceConfig.ProviderCode, balance, CancellationToken.None,
                            slowTopup);
                    }
                    else
                    {
                        _logger.LogInformation(
                            $"Update topup item payment fail: {saleRequest.TransCode}-{saleRequest.TransRef}-{saleRequest.Status}");
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
                        await _saleService.SaleRequestUpdateAsync(saleRequest);
                        await _saleService.PublishConsumerReport(new SaleReponseDto
                        {
                            NextStep = 0,
                            Sale = saleRequest,
                            Status = saleRequest.Status,
                            Balance = balance
                        });
                    }
                }

                return response;
            }
            catch (Exception e)
            {
                _logger.LogError($"{request.TransCode}-TopupRequestError: " + e);
                await SendTeleMessage(new SendTeleTrasactionRequest
                {
                    BotType = BotType.Dev,
                    TransRef = request.TransCode,
                    TransCode = request.TransCode,
                    Title = "GD có vấn đề!. Vui lòng kiểm tra trạng thái giao dịch",
                    Message =
                        $"Mã GD {request.TransCode}\nHàm TopupRequest\nLỗi:{e.Message}",
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

        private async ValueTask<NewMessageResponseBase<WorkerResult>> TopupAsync(WorkerTopupRequest request,
            NewMessageResponseBase<WorkerResult> response, SaleRequestDto saleRequest, bool isRefund,
            List<ServiceConfiguration> configs, string firstProviderCode, decimal balance,
            CancellationToken cancellationToken, bool slowTopup)
        {
            var topupProviderConfig = configs.First(c => c.ProviderCode == firstProviderCode);
            _logger.LogInformation(
                $"Processing parital topup with slow is: {slowTopup}: {saleRequest.TransCode}-{saleRequest.TransRef} : Provider : {topupProviderConfig.ProviderCode}");

            var providerTwo =
                (from x in configs.Where(c =>
                        c.ProviderCode != firstProviderCode)
                    select new ProviderConfig
                    {
                        ProviderCode = x.ProviderCode,
                        Priority = x.Priority,
                        TransCodeConfig = x.TransCodeConfig,
                        MustCount = !string.IsNullOrEmpty(x.WorkShortCode),
                        ProviderMaxWaitingTimeout = x.ProviderMaxWaitingTimeout,
                        ProviderSetTransactionTimeout = x.ProviderSetTransactionTimeout,
                        StatusResponseWhenJustReceived = x.StatusResponseWhenJustReceived,
                        IsEnableResponseWhenJustReceived = x.IsEnableResponseWhenJustReceived,
                        WaitingTimeResponseWhenJustReceived = x.WaitingTimeResponseWhenJustReceived,
                    }).ToList();


            var requestTopup = new GateTopupRequest
            {
                ServiceCode = saleRequest.ServiceCode,
                CategoryCode = saleRequest.CategoryCode,
                Amount = saleRequest.Amount,
                ReceiverInfo = saleRequest.ReceiverInfo,
                ProductCode = saleRequest.ProductCode,
                TransRef = saleRequest.TransCode,
                ProviderCode = firstProviderCode,
                TransCodeProvider = saleRequest.ProviderTransCode,
                Vendor = saleRequest.ProductCode.Split('_')[0],
                RequestDate = saleRequest.CreatedTime,
                PartnerCode = saleRequest.PartnerCode,
                ReferenceCode = saleRequest.TransRef,
                ProviderMaxWaitingTimeout = topupProviderConfig.ProviderMaxWaitingTimeout,
                ProviderSetTransactionTimeout = topupProviderConfig.ProviderSetTransactionTimeout,
                StatusResponseWhenJustReceived = topupProviderConfig.StatusResponseWhenJustReceived,
                IsEnableResponseWhenJustReceived = topupProviderConfig.IsEnableResponseWhenJustReceived,
                WaitingTimeResponseWhenJustReceived = topupProviderConfig.WaitingTimeResponseWhenJustReceived,
            };

            var (newMessageReponseBase, transactionInfoDto) = await CallTopupPriority(requestTopup,
                !string.IsNullOrEmpty(topupProviderConfig.WorkShortCode), providerTwo);
            response.ResponseStatus = newMessageReponseBase.ResponseStatus;
            var providerResponseTransCode = newMessageReponseBase.Results != null
                ? newMessageReponseBase.Results.ProviderResponseTransCode
                : "";
            var receiverType = newMessageReponseBase.Results != null ? newMessageReponseBase.Results.ReceiverType : "";

            var providerCode = transactionInfoDto.ProviderCode;
            var transCodeProvider = transactionInfoDto.TransCodeProvider;
            saleRequest.ProviderResponseCode = providerResponseTransCode;
            saleRequest.ReceiverTypeResponse = receiverType;

            if (!string.IsNullOrEmpty(providerCode))
                saleRequest.Provider = providerCode;
            if (!string.IsNullOrEmpty(transCodeProvider))
                saleRequest.ProviderTransCode = transCodeProvider;

            //_logger.LogInformation($"{saleRequest.TransCode}-{saleRequest.TransRef}-GateTopupRequest return:{response.ToJson()}-{transCodeProvider.ToJson()}");
            switch (response.ResponseStatus.ErrorCode)
            {
                case ResponseCodeConst.Success:
                {
                    response.Results.TransCode = await _commonService.GetReferenceCodeAsync(saleRequest.Provider, saleRequest.PartnerCode, saleRequest.TransCode);
                    saleRequest.Status = SaleRequestStatus.Success;
                    await _saleService.SaleRequestUpdateStatusAsync(saleRequest.TransCode,
                        saleRequest.Provider, SaleRequestStatus.Success, transCodeProvider,
                        providerResponseTransCode: providerResponseTransCode, receiverType: receiverType,referenceCode:response.Results.TransCode);
                    response.ResponseStatus = new ResponseStatusApi(
                        response.ResponseStatus.ErrorCode,
                        $"Bạn đã nạp tiền thành công cho số {saleRequest.ReceiverInfo} số tiền {saleRequest.Amount:0}. Mã GD {saleRequest.TransCode}");
                    if (!string.IsNullOrEmpty(saleRequest.ParentCode) &&
                        saleRequest.AgentType == AgentType.SubAgent)
                    {
                        await _saleService.CommissionRequest(saleRequest);
                    }

                    break;
                }

                case ResponseCodeConst.ResponseCode_WaitForResult:
                case ResponseCodeConst.ResponseCode_RequestReceived:
                case ResponseCodeConst.ResponseCode_TimeOut:
                case ResponseCodeConst.ResponseCode_InProcessing:
                    response.ResponseStatus = new ResponseStatusApi(response.ResponseStatus.ErrorCode,
                        "Giao dịch đang chờ kết quả. Vui lòng liên hệ CSKH để được hỗ trợ");
                    saleRequest.Status = SaleRequestStatus.WaitForResult;
                    _logger.LogWarning("TopupPending: {TransCode} - {TransRef}",
                        saleRequest.TransCode, saleRequest.TransRef);
                    await _saleService.SaleRequestUpdateStatusAsync(saleRequest.TransCode,
                        saleRequest.Provider, SaleRequestStatus.WaitForResult, transCodeProvider);
                    break;
                default:
                    if (saleRequest.SaleType == SaleType.Slow)
                    {
                        if (!_workerConfig.ErrorCodeRefund.Split(",").Contains(response.ResponseStatus.ErrorCode))
                        {
                            isRefund = false;
                        }
                    }

                    if (isRefund)
                    {
                        _logger.LogWarning("TopupRefund: {TransCode} - {TransRef}",
                            saleRequest.TransCode, saleRequest.TransRef);
                        await _bus.Publish<PaymentCancelCommand>(new
                        {
                            CorrelationId = Guid.NewGuid(),
                            saleRequest.TransCode,
                            saleRequest.PaymentTransCode,
                            TransNote = $"Hoàn tiền cho giao dịch thanh toán: {saleRequest.TransRef}",
                            RevertAmount = saleRequest.PaymentAmount,
                            AccountCode = saleRequest.PartnerCode,
                            Timestamp = DateTime.Now
                        }, cancellationToken);
                        saleRequest.Status = SaleRequestStatus.Failed;
                        await _saleService.SaleRequestUpdateStatusAsync(saleRequest.TransCode,
                            saleRequest.Provider, SaleRequestStatus.Failed, transCodeProvider);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "TopupNotRefund please check status: {TransCode} - {TransRef}",
                            saleRequest.TransCode, saleRequest.TransRef);
                        saleRequest.Status = SaleRequestStatus.WaitForConfirm;
                        await _saleService.SaleRequestUpdateStatusAsync(saleRequest.TransCode,
                            saleRequest.Provider, SaleRequestStatus.WaitForConfirm, transCodeProvider);
                    }

                    break;
            }

            if (response.ResponseStatus.ErrorCode != ResponseCodeConst.Success &&
                response.ResponseStatus.ErrorCode != ResponseCodeConst.ResponseCode_RequestReceived &&
                saleRequest.SaleType == SaleType.Slow)
            {
                string message;
                if (!isRefund)
                {
                    message =
                        "Giao dịch nạp chậm có KQ lỗi (Có thể nạp bù). Trạng thái GD chưa được update, chờ xử lý nạp bù";
                }
                else
                {
                    message = response.ResponseStatus.ErrorCode == ResponseCodeConst.ResponseCode_WaitForResult ||
                              response.ResponseStatus.ErrorCode == ResponseCodeConst.ResponseCode_TimeOut
                        ? "GD nạp chậm chưa có kết quả cuối. Hãy check ngay"
                        : "Giao dịch nạp chậm có KQ lỗi (Đã hoàn tiền cho khách)";
                }

                await SendTeleMessage(new SendTeleTrasactionRequest
                {
                    BotType = BotType.Sale,
                    TransRef = request.TransCode,
                    TransCode = request.TransCode,
                    Title = message,
                    Message =
                        $"GD: {saleRequest.TransCode}\n" +
                        $"Ref: {saleRequest.TransRef}\n" +
                        $"Kênh :{saleRequest.Provider}\n" +
                        $"SĐT:{saleRequest.ReceiverInfo}\n" +
                        $"Sô tiền: {saleRequest.Amount.ToFormat("đ")}\n" +
                        $"Message: {response.ResponseStatus.ToJson()}\n" +
                        $"Message NCC: {response.Results.ToJson()}",
                    BotMessageType = BotMessageType.Error
                });
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

        private async Task<NewMessageResponseBase<ResponseProvider>> CallTopupGate(GateTopupRequest topup,
            bool mustCount)
        {
            try
            {
                if (mustCount)
                    await _transCodeGenerator.IncrementValueAsync(
                        $"PayGate_RatingTrans:Items:{topup.ProviderCode}:{topup.PartnerCode}:{topup.ServiceCode}:{topup.CategoryCode}:{topup.ProductCode}_{true}");

                var response = await _grpcClient.GetClientCluster(GrpcServiceName.TopupGateway).SendAsync(topup);
                //_logger.LogInformation($"{topup.TransCodeProvider}-{topup.TransRef}-{topup.ProviderCode} CallTopupGate Return: {response?.ToJson()}");
                if (response == null)
                {
                    _logger.LogWarning(
                        "{TransCodeProvider} - {TransRef} - {ProviderCode} - Can not get Response TopupGate",
                        topup.TransCodeProvider, topup.TransRef, topup.ProviderCode);
                    await SendTeleMessage(new SendTeleTrasactionRequest
                    {
                        BotType = BotType.Dev,
                        TransRef = topup.TransCodeProvider,
                        TransCode = topup.TransRef,
                        Title = "Giao dịch lỗi - Hàm CallTopupGate.Can not get Response TopupGate",
                        Message =
                            $"GD {topup.TransRef}-{topup.TransCodeProvider}\nGD chưa được xử lý thành công. Không có response từ TopupGw",
                        BotMessageType = BotMessageType.Error
                    });
                    return new NewMessageResponseBase<ResponseProvider>()
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
                    $"{topup.TransCodeProvider}-{topup.TransRef}-{topup.ProviderCode} CallTopupGate Exception: {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
                await SendTeleMessage(new SendTeleTrasactionRequest
                {
                    BotType = BotType.Dev,
                    TransRef = topup.TransCodeProvider,
                    TransCode = topup.TransRef,
                    Title = $"GD có vấn đề!. Vui lòng kiểm tra trạng thái giao dịch",
                    Message =
                        $"Mã GD {topup.TransRef}-{topup.TransCodeProvider}\nHàm CallTopupGate\nLỗi:{ex.Message}",
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

        private async Task<(NewMessageResponseBase<ResponseProvider>, TransactionInfoDto)> CallTopupPriority(
            GateTopupRequest topup, bool mustCount,
            List<ProviderConfig> providerConfigs)
        {
            if (topup.ProviderCode.StartsWith(ProviderConst.VINNET))
                topup.TransCodeProvider = Guid.NewGuid().ToString();
            var info = new TransactionInfoDto
            {
                TransCodeProvider = topup.TransCodeProvider,
                ProviderCode = topup.ProviderCode
            };
            try
            {
                var result = await CallTopupGate(topup, mustCount);
                _logger.LogInformation(
                    $"{topup.TransCodeProvider}|{topup.TransRef}|{topup.ProviderCode} CallTopupGate reponse : {result.ToJson()}");

                var isIgnore = !string.IsNullOrEmpty(_workerConfig.ErrorCodeRefund) && _workerConfig.ErrorCodeRefund
                    .Split(",").Contains(result.ResponseStatus.ErrorCode);

                if (result.ResponseStatus.ErrorCode != ResponseCodeConst.ResponseCode_WaitForResult
                    && result.ResponseStatus.ErrorCode != ResponseCodeConst.ResponseCode_TimeOut &&
                    result.ResponseStatus.ErrorCode != ResponseCodeConst.ResponseCode_RequestReceived
                    && result.ResponseStatus.ErrorCode != ResponseCodeConst.ResponseCode_InProcessing
                    && result.ResponseStatus.ErrorCode != ResponseCodeConst.ResponseCode_TransactionError
                    && result.ResponseStatus.ErrorCode != ResponseCodeConst.Success
                    && !isIgnore
                    && providerConfigs.Count > 0)
                {
                    foreach (var item in providerConfigs.OrderBy(c => c.Priority))
                    {
                        try
                        {
                            var config = item;
                            if (config.ProviderCode.StartsWith(ProviderConst.VINNET))
                                topup.TransCodeProvider = Guid.NewGuid().ToString();
                            else
                            {
                                topup.TransCodeProvider = string.IsNullOrEmpty(config.TransCodeConfig)
                                    ? topup.TransRef
                                    : config.TransCodeConfig + "_" + topup.TransRef;
                            }

                            topup.ProviderCode = config.ProviderCode;
                            topup.ProviderMaxWaitingTimeout = item.ProviderMaxWaitingTimeout;
                            topup.ProviderSetTransactionTimeout = item.ProviderSetTransactionTimeout;
                            topup.StatusResponseWhenJustReceived = item.StatusResponseWhenJustReceived;
                            topup.IsEnableResponseWhenJustReceived = item.IsEnableResponseWhenJustReceived;
                            topup.WaitingTimeResponseWhenJustReceived = item.WaitingTimeResponseWhenJustReceived;
                            info.TransCodeProvider = topup.TransCodeProvider;
                            info.ProviderCode = topup.ProviderCode;
                            var update = await _saleService.SaleRequestUpdateStatusAsync(topup.TransRef,
                                topup.ProviderCode, SaleRequestStatus.InProcessing, topup.TransCodeProvider);
                            if (!update)
                                break;
                            result = await CallTopupGate(topup, item.MustCount);
                            _logger.LogInformation(
                                $"{topup.TransCodeProvider}-{topup.TransRef}-{topup.ProviderCode} CallTopupPriority Return: {result.ToJson()}");
                            if (result.ResponseStatus.ErrorCode == ResponseCodeConst.Error)
                                continue;
                            break;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(
                                $"{topup.TransCodeProvider}-{topup.TransRef}-{topup.ProviderCode} TopupProviderPriority Exception: {ex.Message}|{ex.StackTrace}|{ex.InnerException}");

                            await SendTeleMessage(new SendTeleTrasactionRequest
                            {
                                BotType = BotType.Dev,
                                TransRef = topup.TransCodeProvider,
                                TransCode = topup.TransRef,
                                Title = $"GD có vấn đề!. Vui lòng kiểm tra trạng thái giao dịch",
                                Message =
                                    $"Mã GD {topup.TransRef}-{topup.TransCodeProvider}\nHàm CallTopupPriority.\nLỗi:{ex.Message}",
                                BotMessageType = BotMessageType.Error
                            });
                            break;
                        }
                    }
                }

                //if (info.ProviderCode.StartsWith("GATE") && !string.IsNullOrEmpty(result.Results.ProviderCode))
                //    info.ProviderCode = result.Results.ProviderCode;
                return (result, info);
            }
            catch (Exception e)
            {
                _logger.LogError(
                    $"{topup.TransCodeProvider}|{topup.TransRef}|{topup.ProviderCode} CallTopupPriority error : {e}");
                await SendTeleMessage(new SendTeleTrasactionRequest
                {
                    BotType = BotType.Dev,
                    TransRef = topup.TransCodeProvider,
                    TransCode = topup.TransRef,
                    Title = "GD có vấn đề!. Vui lòng kiểm tra trạng thái giao dịch",
                    Message =
                        $"Mã GD {topup.TransRef}-{topup.TransCodeProvider}\nHàm CallTopupPriority\nLỗi:{e.Message}",
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