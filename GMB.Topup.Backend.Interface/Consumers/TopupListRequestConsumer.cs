using System;
using System.Linq;
using System.Threading.Tasks;
using HLS.Paygate.Backend.Interface.Connectors;
using MassTransit;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using NLog;
using HLS.Paygate.Gw.Domain.Services;
using HLS.Paygate.Gw.Model.Commands;
using HLS.Paygate.Gw.Model.Enums;
using HLS.Paygate.Shared;

namespace HLS.Paygate.Backend.Interface.Consumers
{
    public class TopupListRequestConsumer : IConsumer<TopupListRequestCommand>
    {
        private readonly IConfiguration _configuration;
        private readonly Logger _logger = LogManager.GetLogger("TopupListRequestConsumer");
        private readonly ISaleService _saleService;
        private readonly ExternalServiceConnector _externalServiceConnector;
        readonly IRequestClient<PaymentProcessCommand> _requestClient;

        public TopupListRequestConsumer(ISaleService saleService, IConfiguration configuration,
            ExternalServiceConnector externalServiceConnector, IRequestClient<PaymentProcessCommand> requestClient)
        {
            _saleService = saleService;
            _configuration = configuration;
            _externalServiceConnector = externalServiceConnector;
            _requestClient = requestClient;
        }

        public async Task Consume(ConsumeContext<TopupListRequestCommand> context)
        {
            _logger.LogInformation("TopupListRequestCommand comming request: " + context.Message.ToJson());
            var saleRequestCommand = context.Message;
            var checkExist = await
                _saleService.SaleRequestBatchCheckAsync(saleRequestCommand.BatchCode,
                    saleRequestCommand.PartnerCode);

            var response = new MessageResponseBase();
            if (checkExist.ResponseCode != "07")
            {
                response.ResponseMessage =
                    $"Giao dịch của tài khoản: {saleRequestCommand.PartnerCode} có mã giao dịch: {saleRequestCommand.BatchCode} đã tồn tại";
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
                var saleRequests =
                    await _saleService.SaleRequestCreatesAsync(context.Message.TopupItems,
                        context.Message.BatchCode);
                if (null != saleRequests)
                {
                    _logger.LogInformation(
                        $"Create topup request success: {context.Message.BatchCode}-{context.Message.PartnerCode}");
                    response.ResponseMessage = "Tiếp nhận giao dịch thành công";
                    response.ResponseCode = ResponseCodeConst.ResponseCode_TopupReceived;

                    #region Payment

                    decimal paymentAmount = context.Message.TopupItems.Sum(x => x.Amount);
                    var discountObject = await
                        _externalServiceConnector.DiscountPolicyGetAsync(context.Message.PartnerCode,
                            saleRequests[0].CategoryCode,
                            saleRequests[0].Amount);
                    var discountRate = 0;// context.Message.PriorityDiscountRate;
                    if (discountRate > 0 && (discountObject == null || discountRate > discountObject.DiscountValue))
                    {
                        _logger.LogInformation(
                            $"Topup fail - PriorityDiscountRate invalid: {context.Message.BatchCode}-{context.Message.PartnerCode}");
                        response.ResponseMessage = "Chiết khấu ưu tiên không hợp lệ";
                        response.ResponseCode = ResponseCodeConst.ResponseCode_Balance_Not_Enough;
                    }
                    else
                    {
                        decimal discount = 0;
                        if (discountObject != null)
                        {
                            discount = discountRate > 0 ? discountRate : discountObject.DiscountValue;
                            paymentAmount -= paymentAmount * discount / 100;
                        }

                        // foreach (var item in saleRequests)
                        // {
                        //     decimal paymentItem = item.Amount;
                        //     if (discountObject != null)
                        //     {
                        //         item.DiscountRate = discountObject.DiscountValue;
                        //         item.PriorityDiscountRate = discountRate;
                        //         paymentItem = item.Amount - item.Amount * discount / 100;
                        //     }
                        //
                        //     item.PaymentAmount = paymentItem; //gán số tiền payment lại
                        // }


                        var paymentResponse = await _requestClient.GetResponse<MessageResponseBase>(new
                        {
                            context.CorrelationId,
                            AccountCode = context.Message.PartnerCode,
                            PaymentAmount = paymentAmount,
                            CurrencyCode = CurrencyCode.VND.ToString("G"),
                            TransRef = context.Message.BatchCode,
                            TransNote = $"Thanh toán cho giao dịch: {context.Message.BatchCode}"
                        });

                        #endregion

                        _logger.LogInformation(
                            $"Paymeny topup request return: {paymentResponse.Message.ResponseCode}-{paymentResponse.Message.ResponseMessage} {context.Message.BatchCode}-{context.Message.PartnerCode}");
                        if (paymentResponse.Message.ResponseCode == "01")
                        {
                            foreach (var item in saleRequests)
                            {
                                item.PaymentTransCode = paymentResponse.Message.ResponseMessage;
                                item.Status = SaleRequestStatus.Paid;
                                await _saleService.SaleRequestUpdateAsync(item);
                            }
                        }
                        else
                        {
                            _logger.LogInformation(
                                $"Payment fail. {context.Message.BatchCode}-{context.Message.PartnerCode} - {paymentResponse.Message.ResponseCode}-{paymentResponse.Message.ResponseMessage}");
                            response.ResponseMessage = "Thanh toán cho giao dịch lỗi. Vui lòng kiểm tra lại số dư";
                            response.ResponseCode = ResponseCodeConst.ResponseCode_Balance_Not_Enough;
                        }
                    }

                    if (response.ResponseCode != ResponseCodeConst.ResponseCode_TopupReceived)
                    {
                        foreach (var item in saleRequests)
                        {
                            await _saleService.SaleRequestUpdateStatusAsync(item.TransCode, SaleRequestStatus.Failed);
                        }
                    }
                }
                else
                {
                    response.ResponseMessage = "Khởi tạo giao dịch lỗi";
                    response.ResponseCode = ResponseCodeConst.ResponseCode_00;
                }

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
