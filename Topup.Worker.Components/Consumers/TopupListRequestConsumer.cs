using System;
using System.Threading.Tasks;
using HLS.Paygate.Gw.Domain.Services;
using HLS.Paygate.Gw.Model.Commands;
using HLS.Paygate.Shared;
using HLS.Paygate.Shared.AbpConnector;
using HLS.Paygate.Worker.Components.Connectors;
using MassTransit;
using Microsoft.Extensions.Logging;
using NLog;
using ServiceStack;

namespace HLS.Paygate.Worker.Components.Consumers
{
    public class TopupListRequestConsumer : IConsumer<TopupListRequestCommand>
    {
        //private readonly Logger _logger = LogManager.GetLogger("TopupListRequestConsumer");
        private readonly ILogger<TopupListRequestConsumer> _logger;
        private readonly ISaleService _saleService;
        private readonly ExternalServiceConnector _externalServiceConnector;
        private readonly ILimitTransAccountService _limitTransAccountService;

        public TopupListRequestConsumer(ISaleService saleService, ExternalServiceConnector externalServiceConnector, ILimitTransAccountService limitTransAccountService, ILogger<TopupListRequestConsumer> logger)
        {
            _saleService = saleService;
            _externalServiceConnector = externalServiceConnector;
            _limitTransAccountService = limitTransAccountService;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<TopupListRequestCommand> context)
        {
            _logger.LogInformation("TopupList request is comming: " + context.Message.ToJson());

            var saleListRequestCommand = context.Message;
            var checkExist = await
                _saleService.SaleRequestCheckAsync(saleListRequestCommand.TransRef,
                    saleListRequestCommand.PartnerCode);

            var response = new MessageResponseBase();
            if (checkExist.ResponseCode != ResponseCodeConst.ResponseCode_TransactionNotFound)
            {
                response.ResponseMessage =
                    $"Giao dịch của tài khoản: {saleListRequestCommand.PartnerCode} có mã giao dịch: {saleListRequestCommand.TransRef} đã tồn tại";
                response.ResponseCode = ResponseCodeConst.ResponseCode_RequestAlreadyExists;
                await context.RespondAsync<MessageResponseBase>(new
                {
                    Id = context.Message.CorrelationId,
                    ReceiveTime = DateTime.Now,
                    response.ResponseCode,
                    response.ResponseMessage
                });
            }
            else
            {
                if (string.IsNullOrEmpty(context.Message.PartnerCode))
                    throw new ArgumentNullException(nameof(context.Message.PartnerCode));

                if (string.IsNullOrEmpty(context.Message.CurrencyCode))
                    //throw new ArgumentNullException(nameof(payment.CurrencyCode));
                    context.Message.CurrencyCode = CurrencyCode.VND.ToString("G");

                var totalPaymentAmount = 0m;

                foreach (var topupItem in context.Message.TopupItems)
                {
                    var paymentAmount = topupItem.Amount;
                    var discount = await
                        _externalServiceConnector.CheckProductDiscount(context.Message.PartnerCode,
                            topupItem.ProductCode, 0, 1);

                    if (discount == null)
                    {
                        throw new Exception("Thông tin sản phẩm không tồn tại");
                    }

                    //paymentAmount = discount.PaymentAmount;

                    totalPaymentAmount += discount.PaymentAmount;
                }


                // if (paymentAmount <= 0)
                //     throw new ArgumentOutOfRangeException(nameof(context.Message.SaleRequest.Amount));
                // //check hạn mức
                // var checkLimit = await
                //     _limitTransAccountService.CheckLimitAccount(
                //         context.Message.SaleRequest.StaffAccount, paymentAmount);
                // _logger.LogInformation($"CheckLimit return:{checkLimit.ToJson()}");
                // if (checkLimit.ResponseCode == "01")
                // {
                //     //Check kênh trước
                //     var serviceConfiguration = await _externalServiceConnector.ServiceConfigurationAsync(
                //         context.Message.SaleRequest.ServiceCode,
                //         context.Message.SaleRequest.CategoryCode, context.Message.SaleRequest.ProductCode);
                //     if (serviceConfiguration != null)
                //     {
                //         var saleRequest = await _saleService.SaleRequestCreateAsync(context.Message.SaleRequest);
                //         if (null != saleRequest)
                //         {
                //             saleRequest.PaymentAmount = paymentAmount;
                //             saleRequest.DiscountRate = discount?.DiscountValue;
                //             saleRequest.FixAmount = discount?.FixAmount;
                //             saleRequest.DiscountAmount = discount?.DiscountAmount;
                //             saleRequest.Provider = serviceConfiguration.ProviderCode;
                //
                //             await context.Publish<TopupSubmitted<SaleRequestDto>>(new
                //             {
                //                 context.Message.CorrelationId,
                //                 SaleRequest = saleRequest
                //             });
                //
                //             _logger.LogInformation(
                //                 $"Create topup request success: {saleRequest.TransCode}-{saleRequest.TransRef}");
                //             response.ResponseMessage = "Tiếp nhận giao dịch thành công";
                //             response.ResponseCode = ResponseCodeConst.ResponseCode_TopupReceived;
                //
                //             #region Payment
                //
                //             var paymentResponse = await _paymentProcessClient.GetResponse<MessageResponseBase>(new
                //             {
                //                 context.CorrelationId,
                //                 AccountCode = saleRequest.PartnerCode,
                //                 PaymentAmount = paymentAmount,
                //                 saleRequest.CurrencyCode,
                //                 saleRequest.TransRef,
                //                 saleRequest.ServiceCode,
                //                 saleRequest.CategoryCode,
                //                 TransNote = $"Thanh toán cho giao dịch: {saleRequest.TransRef}"
                //             }, CancellationToken.None, RequestTimeout.After(m: 5));
                //
                //             #endregion
                //
                //             _logger.LogInformation(
                //                 $"Paymeny topup request return: {paymentResponse.Message.ResponseCode}-{paymentResponse.Message.ResponseMessage} {saleRequest.TransCode}-{saleRequest.TransRef}");
                //             if (paymentResponse.Message.ResponseCode == "01")
                //             {
                //                 saleRequest.Status = SaleRequestStatus.Paid;
                //                 saleRequest.PaymentTransCode = paymentResponse.Message.ResponseMessage;
                //                 var topupUpdate = await _saleService.SaleRequestUpdateAsync(saleRequest);
                //
                //                 await context.Publish<TopupPaid>(new
                //                 {
                //                     context.Message.CorrelationId, saleRequest.PaymentTransCode
                //                 });
                //
                //                 if (topupUpdate != null)
                //                 {
                //                     _logger.LogInformation(
                //                         $"Update topup item payment success: {topupUpdate.TransCode}-{topupUpdate.TransRef}-{topupUpdate.Status}");
                //                     var topupResult = await _topupClient.GetResponse<MessageResponseBase>(new
                //                     {
                //                         saleRequest.ServiceCode,
                //                         saleRequest.CategoryCode,
                //                         saleRequest.Amount,
                //                         saleRequest.ReceiverInfo,
                //                         saleRequest.ProductCode,
                //                         TransRef = saleRequest.TransCode,
                //                         serviceConfiguration.ProviderCode, // "ZOTA",
                //                         Vendor = saleRequest.ProductCode.Split('_')[0],
                //                         RequestDate = saleRequest.CreatedTime,
                //                         context.Message.CorrelationId
                //                     }, CancellationToken.None, RequestTimeout.After(m: 5));
                //
                //                     _logger.LogInformation(
                //                         $"Update topup item payment success: {topupUpdate.TransCode}-{topupUpdate.TransRef}-{topupResult.Message.ResponseCode}");
                //
                //                     if (topupResult.Message.ResponseCode == ResponseCodeConst.Success)
                //                     {
                //                         await _saleService.SaleRequestUpdateStatusAsync(saleRequest.TransCode,
                //                             SaleRequestStatus.Success);
                //                         // await context.RespondAsync<MessageResponseBase>(new
                //                         // {
                //                         //     // Id = context.Message.CorrelationId,
                //                         //     // ReceiveTime = DateTime.Now,
                //                         //     topupResult.Message.ResponseCode,
                //                         //     ResponseMessage =
                //                         //
                //                         // });
                //
                //                         response.ResponseCode = topupResult.Message.ResponseCode;
                //                         response.ResponseMessage =
                //                             $"Bạn đã nạp tiền thành công cho số {saleRequest.ReceiverInfo} số tiền {saleRequest.Amount:0}. Mã GD {saleRequest.TransCode}";
                //
                //                         await context.Publish<TopupCompleted>(new
                //                         {
                //                             context.Message.CorrelationId,
                //                             Result = topupResult.Message.ResponseCode,
                //                             Message = topupResult.Message.ResponseMessage
                //                         });
                //                     }
                //                     else if (topupResult.Message.ResponseCode ==
                //                              ResponseCodeConst.ResponseCode_WaitForResult)
                //                     {
                //                         response.ResponseCode = topupResult.Message.ResponseCode;
                //                         response.ResponseMessage =
                //                             $"Giao dịch nạp tiền cho {saleRequest.ReceiverInfo} số tiền {saleRequest.Amount:0} chưa có kết quả. Mã GD {saleRequest.TransCode}";
                //                         await _saleService.SaleRequestUpdateStatusAsync(saleRequest.TransCode,
                //                             SaleRequestStatus.ProcessTimeout);
                //
                //                         await context.Publish<TopupTimedOut>(new
                //                         {
                //                             context.Message.CorrelationId,
                //                             WaitTimeToCheckAgain = TimeSpan.FromMinutes(30)
                //                         });
                //                     }
                //                     else
                //                     {
                //                         // await context.RespondAsync<MessageResponseBase>(new
                //                         // {
                //                         //     Id = context.Message.CorrelationId,
                //                         //     ReceiveTime = DateTime.Now,
                //                         //     topupResult.Message.ResponseCode,
                //                         //     topupResult.Message.ResponseMessage
                //                         // });
                //
                //                         response.ResponseCode = topupResult.Message.ResponseCode;
                //                         response.ResponseMessage = topupResult.Message.ResponseMessage;
                //
                //                         await context.Publish<PaymentCancelCommand>(new
                //                         {
                //                             context.Message.CorrelationId,
                //                             saleRequest.TransCode,
                //                             saleRequest.PaymentTransCode,
                //                             TransNote = $"Hoàn tiền cho giao dịch thanh toán: {saleRequest.TransCode}",
                //                             RevertAmount = saleRequest.PaymentAmount,
                //                             AccountCode = saleRequest.PartnerCode
                //                         });
                //
                //                         await context.Publish<TopupCompleted>(new
                //                         {
                //                             context.Message.CorrelationId,
                //                             Result = string.Join("|", topupResult.Message.ResponseCode,
                //                                 topupResult.Message.ResponseMessage)
                //                         });
                //
                //                         await _saleService.SaleRequestUpdateStatusAsync(saleRequest.TransCode,
                //                             SaleRequestStatus.Failed);
                //                     }
                //                 }
                //                 else
                //                 {
                //                     _logger.LogInformation(
                //                         $"Update topup item payment faild: {saleRequest.TransCode}-{saleRequest.TransRef}-{saleRequest.Status}");
                //
                //                     await context.Publish<PaymentCancelCommand>(new
                //                     {
                //                         context.Message.CorrelationId,
                //                         saleRequest.TransCode,
                //                         saleRequest.PaymentTransCode,
                //                         TransNote = $"Hoàn tiền cho giao dịch thanh toán: {saleRequest.TransCode}",
                //                         RevertAmount = saleRequest.PaymentAmount,
                //                         AccountCode = saleRequest.PartnerCode
                //                     });
                //                 }
                //             }
                //             else
                //             {
                //                 _logger.LogInformation(
                //                     $"Payment fail. {saleRequest.TransCode}-{saleRequest.TransRef} - {paymentResponse.Message.ResponseCode}-{paymentResponse.Message.ResponseMessage}");
                //                 await _saleService.SaleRequestUpdateStatusAsync(saleRequest.TransCode,
                //                     SaleRequestStatus.Failed);
                //                 response.ResponseMessage = "Thanh toán cho giao dịch lỗi. Vui lòng kiểm tra lại số dư";
                //                 response.ResponseCode = ResponseCodeConst.ResponseCode_Balance_Not_Enough;
                //             }
                //         }
                //         else
                //         {
                //             response.ResponseMessage = "Khởi tạo giao dịch lỗi";
                //             response.ResponseCode = ResponseCodeConst.ResponseCode_00;
                //         }
                //     }
                //     else
                //     {
                //         response.ResponseMessage = "Giao dịch lỗi. Không tìm thấy kênh giao dịch";
                //         response.ResponseCode = ResponseCodeConst.ResponseCode_00;
                //     }
                // }
                // else
                // {
                //     _logger.LogInformation(checkLimit.ResponseMessage);
                //     response.ResponseMessage = checkLimit.ResponseMessage;
                //     response.ResponseCode = "00";
                // }


                await context.RespondAsync<MessageResponseBase>(new
                {
                    Id = context.Message.CorrelationId,
                    ReceiveTime = DateTime.Now,
                    response.ResponseCode,
                    response.ResponseMessage
                });

            }
        }
    }
}
