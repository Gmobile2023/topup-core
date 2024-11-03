using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HLS.Paygate.Backend.Interface.Connectors;
using MassTransit;
using Microsoft.Extensions.Configuration;
using NLog;
using ServiceStack;
using HLS.Paygate.Gw.Domain.Services;
using HLS.Paygate.Gw.Model.Commands;
using HLS.Paygate.Gw.Model.Dtos;
using HLS.Paygate.Gw.Model.Enums;
using HLS.Paygate.Gw.Model.Events;
using HLS.Paygate.Gw.Model.ResponseDto;
using HLS.Paygate.Shared;
using HLS.Paygate.Shared.Utils;

namespace HLS.Paygate.Backend.Interface.Consumers
{
    public class CardSaleRequestConsumer : IConsumer<CardSaleRequestCommand>
    {
        private readonly IConfiguration _configuration;
        private readonly Logger _logger = LogManager.GetLogger("CardSaleRequestConsumer");
        private readonly ISaleService _saleService;
        private readonly IServiceGateway _gateway;
        readonly IRequestClient<PaymentProcessCommand> _requestClient;
        // readonly IRequestClient<StockCommand> _cardStockRequestClient;
        private readonly IRequestClient<StockInventoryCommand> _stockInventoryClient;
        private readonly IRequestClient<StockSaleCommand> _stockSaleClient;
        private readonly ExternalServiceConnector _externalServiceConnector;

        public CardSaleRequestConsumer(ISaleService saleService, IConfiguration configuration,
            ExternalServiceConnector externalServiceConnector, IRequestClient<PaymentProcessCommand> requestClient,
             IRequestClient<StockInventoryCommand> stockInventoryClient, IRequestClient<StockSaleCommand> stockSaleClient)
        {
            _saleService = saleService;
            _configuration = configuration;
            _externalServiceConnector = externalServiceConnector;
            _requestClient = requestClient;
            // _cardStockRequestClient = cardStockRequestClient;
            _stockInventoryClient = stockInventoryClient;
            _stockSaleClient = stockSaleClient;
            _gateway = HostContext.AppHost.GetServiceGateway();
        }

        public async Task Consume(ConsumeContext<CardSaleRequestCommand> context)
        {
            try
            {
                _logger.LogInformation("CardSaleRequestConsumer is comming request: " + context.Message.SaleRequest.ToJson());

                var cardRequest = context.Message.SaleRequest;
                var checkExist = await
                    _saleService.SaleRequestCheckAsync(cardRequest.TransRef,
                        cardRequest.PartnerCode);

                var response = new MessageResponseBase();
                if (checkExist.ResponseCode != "07")
                {
                    response.ResponseMessage =
                        $"Giao dịch của tài khoản: {cardRequest.PartnerCode} có mã giao dịch: {cardRequest.TransRef} đã tồn tại";
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
                    var saleRequest = await _saleService.SaleRequestCreateAsync(context.Message.SaleRequest);
                    if (null != saleRequest)
                    {
                        //Check khả dụng thẻ trước

                        var (accepted, rejected) =
                            await _stockInventoryClient
                                .GetResponse<CardStockCommandSubmitted<int>, CardStockCommandRejected>(
                                    new
                                    {
                                        context.Message.CorrelationId,
                                        StockCode = StockCodeConst.STOCK_SALE,
                                        saleRequest.ProductCode,
                                    },
                                    CancellationToken.None, RequestTimeout.After(m: 1));

                        var result = await accepted;

                        var cardInStock = int.Parse(result.Message.Payload.ToString());
                        if (cardInStock < cardRequest.Quantity)
                        {
                            _logger.LogInformation($"Topup fail {saleRequest.TransCode} - Kho the khong du");
                            await context.RespondAsync<MessageResponseBase>(new
                            {
                                Id = context.Message.CorrelationId,
                                ReceiveTime = DateTime.Now,
                                ResponseCode = ResponseCodeConst.ResponseCode_CardNotInventory,
                                ResponseMessage = "Kho thẻ không đủ"
                            });
                            saleRequest.Status = TopupStatus.Failed;
                            await _saleService.SaleRequestUpdateAsync(saleRequest);
                        }
                        else
                        {
                            decimal paymentAmount = saleRequest.Amount; //Chỗ này đã nhân với số lượng ở trong rồi
                            var discountObject = await
                                _externalServiceConnector.DiscountPolicyGetAsync(saleRequest.PartnerCode,
                                    saleRequest.CategoryCode, context.Message.CardValue);

                            if (discountObject != null)
                            {
                                paymentAmount -= paymentAmount * discountObject.DiscountValue / 100;
                                saleRequest.DiscountRate = discountObject.DiscountValue;
                            }

                            var paymentResponse = await _requestClient.GetResponse<MessageResponseBase>(new
                            {
                                context.CorrelationId,
                                AccountCode = saleRequest.PartnerCode,
                                PaymentAmount = paymentAmount,
                                CurrencyCode = CurrencyCode.VND.ToString("G"),
                                TransRef = cardRequest.TransCode,
                                saleRequest.ServiceCode,
                                saleRequest.CategoryCode,
                                TransNote = $"Thanh toán cho giao dịch: {saleRequest.TransRef}"
                            });
                            if (paymentResponse.Message.ResponseCode == "01")
                            {
                                saleRequest.PaymentAmount = paymentAmount;
                                saleRequest.PaymentTransCode = paymentResponse.Message.ResponseMessage;
                                saleRequest.Status = TopupStatus.Paid;
                                await _saleService.SaleRequestUpdateAsync(saleRequest);
                                //Call api Lấy thẻ trả về kết quả
                                var (accepted1, rejected1) =
                                    await _stockSaleClient
                                        .GetResponse<CardStockCommandSubmitted<List<CardSaleResponseDto>>, CardStockCommandRejected>(
                                            new
                                            {
                                                CorrelationId = Guid.NewGuid(),
                                                StockCode = StockCodeConst.STOCK_SALE,
                                                saleRequest.ProductCode,
                                                Amount = saleRequest.Quantity,
                                            },
                                            CancellationToken.None, RequestTimeout.After(m: 1));


                                if (accepted1.IsCompleted && accepted1.Status == TaskStatus.RanToCompletion)
                                {
                                    var result1 = await accepted1;
                                    await _saleService.SaleRequestUpdateStatusAsync(saleRequest.TransCode,
                                        TopupStatus.Success);
                                    //Trả về kết quả giao dịch => Cộng lãi chiết khấu sau.
                                    await context.RespondAsync<MessageResponseBase>(new
                                    {
                                        Id = context.Message.CorrelationId,
                                        ReceiveTime = DateTime.Now,
                                        ResponseCode = "01",
                                        ResponseMessage = "Giao dịch thành công",
                                        PayLoad = result.Message.Payload
                                    });
                                    try
                                    {
                                        var cards = result1.Message.Payload;
                                        var lstTopupItem = cards.Select(item => new SaleItemDto
                                            {
                                                Amount = item.CardValue,
                                                Serial = item.Serial,
                                                CardExpiredDate = item.ExpiredDate,
                                                Status = TopupStatus.Success,
                                                Vendor = item.Vendor,
                                                CardCode = item.CardCode.EncryptTripDes(),
                                                CardValue = item.CardValue,
                                                ServiceCode = saleRequest.ServiceCode,
                                                PartnerCode = saleRequest.PartnerCode,
                                                SaleType = "PINCODE",
                                                SaleTransCode = saleRequest.TransCode,
                                                CreatedTime = DateTime.Now
                                            })
                                            .ToList();
                                        await _saleService.SaleItemListCreateAsync(lstTopupItem);
                                    }
                                    catch (Exception e)
                                    {
                                    }

                                    //Cộng lãi chiết khấu. Mua mã thẻ
                                    // await context.Publish<TopupCommandDone>(new
                                    // {
                                    //     TopupRequest = saleRequest
                                    // });
                                }
                                else
                                {
                                    _logger.LogInformation(
                                        $"Get card not found. Begin refund: {saleRequest.TransCode}-{saleRequest.TransRef}");
                                    await context.RespondAsync<MessageResponseBase>(new
                                    {
                                        Id = context.Message.CorrelationId,
                                        ReceiveTime = DateTime.Now,
                                        ResponseCode = ResponseCodeConst.ResponseCode_CardNotInventory,
                                        ResponseMessage = "Không lấy được thông tin thẻ. Vui lòng liên hệ CSKH để được hỗ trợ"
                                    });
                                    //Hoàn tiền
                                    await context.Publish<SaleCommandFailed>(new
                                    {
                                        TopupRequest = saleRequest
                                    });
                                }
                            }
                            else
                            {
                                saleRequest.Status = TopupStatus.Failed;
                                await _saleService.SaleRequestUpdateAsync(saleRequest);
                                await context.RespondAsync<MessageResponseBase>(new
                                {
                                    Id = context.Message.CorrelationId,
                                    ReceiveTime = DateTime.Now,
                                    ResponseCode = "6001", //chỗ này xem lại mã lỗi bên ví
                                    ResponseMessage = "Không thể thanh toán cho giao dịch. Vui lòng kiểm tra lại số dư"
                                });
                            }
                        }
                    }
                    else
                    {
                        response.ResponseMessage = "Khởi tạo giao dịch lỗi";
                        response.ResponseCode = ResponseCodeConst.ResponseCode_00;
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
            catch (Exception e)
            {
                _logger.LogError("CardSaleRequestConsumer error:" + e);
            }
        }
    }
}
