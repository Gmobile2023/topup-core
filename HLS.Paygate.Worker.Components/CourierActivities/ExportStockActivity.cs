using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HLS.Paygate.Gw.Domain.Services;
using HLS.Paygate.Gw.Model.Commands.Stock;
using HLS.Paygate.Gw.Model.Dtos;
using HLS.Paygate.Gw.Model.Enums;
using HLS.Paygate.Gw.Model.Events.Stock;
using HLS.Paygate.Shared;
using HLS.Paygate.Shared.Dtos;
using HLS.Paygate.Shared.Utils;
using MassTransit;
using MassTransit.Courier;
using MassTransit.Definition;

namespace HLS.Paygate.Worker.Components.CourierActivities
{
    public class ExportStockActivity : IActivity<ExportStockArguments, ExportStockLog>
    {
        private readonly IRequestClient<StockCardSaleCommand> _cardStockRequestClient;

        private readonly ISaleService _saleService;

        public ExportStockActivity(IRequestClient<StockCardSaleCommand> cardStockRequestClient, ISaleService saleService)
        {
            _cardStockRequestClient = cardStockRequestClient;
            _saleService = saleService;
        }

        public async Task<ExecutionResult> Execute(ExecuteContext<ExportStockArguments> context)
        {
            var cardSaleRequest = NewId.NextGuid();

            var (accepted, rejected) =
                await _cardStockRequestClient
                    .GetResponse<CardStockCommandSubmitted<List<CardSaleResponseDto>>, CardStockCommandRejected>(
                        new
                        {
                            CorrelationId = cardSaleRequest,
                            StockCode = StockCodeConst.STOCK_SALE,
                            context.Arguments.ProductCode,
                            Amount = context.Arguments.Quantity
                        },
                        CancellationToken.None, RequestTimeout.After(m: 1));


            if (accepted.IsCompleted && accepted.Status == TaskStatus.RanToCompletion)
            {
                var result = await accepted;
                await _saleService.SaleRequestUpdateStatusAsync(context.Arguments.TransCode,
                    string.Empty,
                    SaleRequestStatus.Success);
                // //Trả về kết quả giao dịch => Cộng lãi chiết khấu sau.
                // await context.RespondAsync<MessageResponseBase>(new
                // {
                //     ReceiveTime = DateTime.Now,
                //     ResponseCode = "01",
                //     ResponseMessage = "Giao dịch thành công",
                //     PayLoad = result.Message.Payload
                // });
                try
                {
                    var cards = result.Message.Payload;
                    var lstTopupItem = cards.Select(item => new SaleItemDto
                    {
                        Amount = item.CardValue,
                        Serial = item.Serial,
                        CardExpiredDate = item.ExpiredDate,
                        Status = SaleRequestStatus.Success,
                        Vendor = item.Vendor,
                        CardCode = item.CardCode.EncryptTripDes(),
                        CardValue = item.CardValue,
                        ServiceCode = context.Arguments.ServiceCode,
                        PartnerCode = context.Arguments.AccountCode,
                        SaleType = "PINCODE",
                        SaleTransCode = context.Arguments.TransCode,
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
            //TODO (Namnl 10/9/2020) chỗ này ném vào StateMachine
            // else
            // {
            //     _logger.LogInformation(
            //         $"Get card not found. Begin refund: {saleRequest.TransCode}-{saleRequest.TransRef}");
            //     await context.RespondAsync<MessageResponseBase>(new
            //     {
            //         Id = context.Message.CorrelationId,
            //         ReceiveTime = DateTime.Now,
            //         ResponseCode = ResponseCodeConst.ResponseCode_CardNotInventory,
            //         ResponseMessage = "Không lấy được thông tin thẻ"
            //     });
            //     //Hoàn tiền
            //     await context.Publish<TopupCommandFailed>(new
            //     {
            //         TopupRequest = saleRequest
            //     });
            // }

            return context.Completed();
        }

        public async Task<CompensationResult> Compensate(CompensateContext<ExportStockLog> context)
        {
            throw new NotImplementedException();
        }
    }

    public interface ExportStockArguments
    {
        int Quantity { get; }
        string ProductCode { get; }
        string TransCode { get; }
        string ServiceCode { get; }
        string AccountCode { get; }
    }

    public class ExportStockActivityDefinition :
        ActivityDefinition<ExportStockActivity, ExportStockArguments, ExportStockLog>
    {
        public ExportStockActivityDefinition()
        {
            ConcurrentMessageLimit = 10;
        }
    }

    public interface ExportStockLog
    {
    }
}
