using System;
using System.Threading.Tasks;
using ServiceStack;
using HLS.Paygate.Connector.Requests;
using HLS.Paygate.Gw.Model.Commands;
using HLS.Paygate.Gw.Model.Dtos;
using HLS.Paygate.Gw.Model.Enums;
using HLS.Paygate.Gw.Model.RequestDtos;
using HLS.Paygate.Shared;

namespace HLS.Paygate.Gw.Interface.Services
{
    public partial class TopupService
    {
        public async Task<object> Post(TopupProviderRequest request)
        {
            _logger.LogInformation(
                $"TopupProviderRequest request {request.TransCode}-{request.PartnerCode}-{request.CategoryCode}-{request.ReceiveAccount}-{request.Amount}");
            var response = new MessageResponseBase();
            try
            {
                var checkExist = await _topupService.TopupRequestCheckAsync(request.TransCode, request.PartnerCode);
                if (checkExist.ResponseCode != "07")
                {
                    response.ResponseMessage =
                        $"Giao dịch của tài khoản: {request.PartnerCode} có mã giao dịch: {request.TransCode} đã tồn tại";
                    response.ResponseCode = ResponseCodeConst.ResponseCode_RequestAlreadyExists;
                    return response;
                }

                var topupRequestDto = new TopupRequestDto
                {
                    Amount = request.Amount,
                    Quantity = 1,
                    Status = 0,
                    CategoryCode = request.CategoryCode,
                    ServiceCode = ServiceCodes.TOPUP,
                    PartnerCode = request.PartnerCode,
                    TopupType = TopupType.TopupPartner,
                    CreatedTime = DateTime.Now,
                    MobileNumber = request.ReceiveAccount,
                    StockType = request.CategoryCode.Split('|')[0],
                    TransRef = request.TransCode,
                    TransCode = await _commonService.TransCodeGenAsync("T"),
                    CurrencyCode = CurrencyCode.VND.ToString("G")
                };
                //Lấy thông tin chiết khấu
                decimal paymentAmount = request.Amount;
                var discountObject = await
                    _connectorService.DiscountPolicyGetAsync(request.PartnerCode,
                        request.CategoryCode,
                        request.Amount);

                if (discountObject != null)
                {
                    paymentAmount = topupRequestDto.Amount -
                                    topupRequestDto.Amount * discountObject.DiscountValue / 100;
                    topupRequestDto.DiscountRate = discountObject.DiscountValue; //gán discount lại
                }

                topupRequestDto.PaymentAmount = paymentAmount;
                //Gọi api check các kênh topup trước. Nếu fail thì reject giao dịch luôn
                var channelTopups = await _connectorService.GetConfigProviderTopup(topupRequestDto.PartnerCode,
                    topupRequestDto.ServiceCode, topupRequestDto.CategoryCode, topupRequestDto.Amount);
                if (channelTopups == null)
                {
                    _logger.LogInformation(
                        $"Can not get channel topup {topupRequestDto.TransCode}-{topupRequestDto.TransRef}-{topupRequestDto.PartnerCode}");
                    response.ResponseMessage = "Không có thông tinh kênh giao dịch";
                    response.ResponseCode = ResponseCodeConst.Error;
                    return response;
                }

                var topupRequest = await _topupService.TopupRequestCreateAsync(topupRequestDto);
                if (topupRequest == null)
                {
                    response.ResponseMessage = "Khởi tạo giao dịch lỗi";
                    response.ResponseCode = ResponseCodeConst.Error;
                    return response;
                }

                //Gọi sang ví thanh toán
                var paymentResponse = await _paymentClient.GetResponse<MessageResponseBase>(new
                {
                    CorrelationId = Guid.NewGuid(),
                    AccountCode = topupRequest.PartnerCode,
                    PaymentAmount = paymentAmount,
                    topupRequest.CurrencyCode,
                    topupRequest.TransRef,
                    topupRequest.ServiceCode,
                    topupRequest.CategoryCode,
                    TransNote = $"Thanh toán cho giao dịch: {topupRequest.TransRef}"
                });

                _logger.LogInformation(
                    $"Paymeny topup request return: {paymentResponse.Message.ResponseCode}-{paymentResponse.Message.ResponseMessage} {topupRequest.TransCode}-{topupRequest.TransRef}");
                if (paymentResponse.Message.ResponseCode == ResponseCodeConst.ResponseCode_Success)
                {
                    //Nếu thanh toán thành công xử lý tiếp giao dịch ở đây
                    topupRequest.PaymentTransCode = paymentResponse.Message.ResponseMessage;
                    var topupUpdate = await _topupService.TopupRequestUpdateAsync(topupRequest);
                    if (topupUpdate != null)
                    {
                        //Chỗ này lấy danh sách kênh ra đê xử lý topup. Nếu thành công update lại trậng thái giao dịch. 
                        int requestAmount = request.Amount;
                        bool isRefund = true;
                        foreach (var channel in channelTopups)
                        {
                            var process =
                                _topupProcessFactory.GetServiceByKey(channel.SupplierCode);
                            if (process == null)
                                continue;
                            var topupResponse = await process.DoWork(new TopupRequestMessage
                            {
                                Amount = requestAmount,
                                CategoryCode = request.CategoryCode,
                                PartnerCode = request.PartnerCode,
                                ProviderCode = channel.SupplierCode,
                                ServiceCode = ServiceCodes.TOPUP,
                                TransCode = topupRequest.TransCode,
                                Account = request.ReceiveAccount,
                                DoworkType = DoworkType.TopupRequest,
                                Configs= channel,
                            });

                            _logger.LogInformation(
                                $"{topupRequest.TransCode}|{channel.SupplierCode} Payment Reponse : {topupResponse.ToJson()}");
                            if (topupResponse.ResponseCode == "01")
                            {
                                isRefund = false;
                                await _topupService.TopupItemCreateAsync(new TopupItemDto
                                {
                                    Status = TopupStatus.Success,
                                    Amount = Convert.ToInt32(topupResponse.ChargeAmount),
                                    ServiceCode = ServiceCodes.TOPUP,
                                    CreatedTime = DateTime.Now,
                                    PartnerCode = request.PartnerCode,
                                    TopupTransCode = topupRequest.TransCode,
                                    StockType = request.CategoryCode.Split('_')[0],
                                    SupplierCode = channel.SupplierCode,
                                });

                                if (response.ResponseCode != "01")
                                {
                                    response.ResponseCode = "01";
                                    response.ResponseMessage = "Giao dịch thành công";
                                    response.ExtraInfo = topupRequest.TransCode;
                                }

                                if (topupResponse.RequestAmount <= topupResponse.ChargeAmount)
                                    break;
                                else
                                    requestAmount = requestAmount - Convert.ToInt32(topupResponse.ChargeAmount);
                            }
                            else if (topupResponse.ResponseCode == "26")
                            {
                                isRefund = false;
                                await _topupService.TopupItemCreateAsync(new TopupItemDto
                                {
                                    Status = TopupStatus.TimeOver,
                                    Amount = requestAmount,
                                    ServiceCode = ServiceCodes.TOPUP,
                                    CreatedTime = DateTime.Now,
                                    PartnerCode = request.PartnerCode,
                                    TopupTransCode = topupRequest.TransCode,
                                    StockType = request.CategoryCode.Split('_')[0],
                                    SupplierCode = channel.SupplierCode,
                                });

                                if (response.ResponseCode != "01")
                                {
                                    response.ResponseCode = "26";
                                    response.ResponseMessage = "Giao dịch chưa có kết quả.";
                                    response.ExtraInfo = topupRequest.TransCode;
                                }
                            }
                            else
                            {
                                await _topupService.TopupItemCreateAsync(new TopupItemDto
                                {
                                    Status = TopupStatus.Failed,
                                    Amount = requestAmount,
                                    ServiceCode = ServiceCodes.TOPUP,
                                    CreatedTime = DateTime.Now,
                                    PartnerCode = request.PartnerCode,
                                    TopupTransCode = topupRequest.TransCode,
                                    StockType = request.CategoryCode.Split('_')[0],
                                    SupplierCode = channel.SupplierCode,
                                });

                                if (response.ResponseCode != "01" && response.ResponseCode != "26")
                                {
                                    response.ResponseCode = "98";
                                    response.ResponseMessage = "Giao dịch thất bại.";
                                    response.ExtraInfo = topupRequest.TransCode;
                                }
                            }
                        }

                        if (isRefund)
                        {
                            var revertAmount = topupRequest.PaymentAmount;
                            topupRequest.RevertAmount = revertAmount;
                            topupRequest.Status = TopupStatus.Failed;
                            await _topupService.TopupRequestUpdateAsync(topupRequest);
                            await _bus.Publish<PaymentCancelCommand>(new
                            {
                                CorrelationId = Guid.NewGuid(),
                                topupRequest.TransCode,
                                topupRequest.TransRef,
                                topupRequest.PaymentTransCode,
                                TransNote = $"Hoàn tiền cho giao dịch thanh toán: {topupRequest.TransRef}",
                                RevertAmount = revertAmount,
                                AccountCode = request.PartnerCode
                            });
                        }

                        return response;
                    }
                }

                _logger.LogInformation(
                    $"Payment fail. {topupRequest.TransCode}-{topupRequest.TransRef} - {paymentResponse.Message.ResponseCode}-{paymentResponse.Message.ResponseMessage}");
                await _topupService.TopupRequestUpdateStatusAsync(topupRequest.TransCode, TopupStatus.Failed);
                response.ResponseMessage = "Thanh toán cho giao dịch lỗi. Vui lòng kiểm tra lại số dư";
                response.ResponseCode = ResponseCodeConst.ResponseCode_Balance_Not_Enough;
                return response;
            }
            catch (Exception e)
            {
                _logger.LogError("TopupRequest error: " + e);
                response.ResponseMessage = "Giao dịch lỗi";
                response.ResponseCode = ResponseCodeConst.Error;
                return response;
            }
        }
    }
}
