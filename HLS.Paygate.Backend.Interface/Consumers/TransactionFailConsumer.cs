using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using NLog;
using HLS.Paygate.Gw.Domain.Services;
using HLS.Paygate.Gw.Model.Commands;
using HLS.Paygate.Gw.Model.Enums;
using HLS.Paygate.Gw.Model.Events;
using Microsoft.Extensions.Logging;

namespace HLS.Paygate.Backend.Interface.Consumers
{
    public class TransactionFailConsumer : IConsumer<SaleCommandFailed>
    {
        private readonly IConfiguration _configuration;
        //private readonly Logger _logger = LogManager.GetLogger("TopupFailConsumer");
        private readonly ILogger<TransactionFailConsumer> _logger;
        private readonly ISaleService _saleService;

        public TransactionFailConsumer(ISaleService saleService, IConfiguration configuration, ILogger<TransactionFailConsumer> logger)
        {
            _saleService = saleService;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<SaleCommandFailed> context)
        {
            //Chỗ này sẽ làm nhiều việc khác khi 1 gd bị lỗi. Trước mắt chỉ hoàn tiền
            _logger.LogInformation("TopupFailConsumer comming request: " + context.Message.ToJson());
            var request = context.Message.SaleRequest;
            var saleRequest = await _saleService.SaleRequestGetAsync(request.TransCode);
            if (saleRequest != null)
            {
                if (saleRequest.RevertAmount <= 0 && !string.IsNullOrEmpty(saleRequest.PaymentTransCode) &&
                    saleRequest.PaymentAmount > 0)
                {
                    var revertAmount = request.PaymentAmount;
                    request.RevertAmount = revertAmount;
                    request.Status = SaleRequestStatus.Failed;
                    await _saleService.SaleRequestUpdateAsync(request);
                    await context.Publish<PaymentCancelCommand>(new
                    {
                        context.CorrelationId,
                        request.TransCode,
                        request.TransRef,
                        request.PaymentTransCode,
                        TransNote = $"Hoàn tiền cho giao dịch thanh toán: {request.TransRef}",
                        RevertAmount = revertAmount,
                        AccountCode = request.PartnerCode
                    });
                }

                await _saleService.SaleItemUpdateStatus(saleRequest.TransCode, saleRequest.Status);
            }
        }
    }
}
