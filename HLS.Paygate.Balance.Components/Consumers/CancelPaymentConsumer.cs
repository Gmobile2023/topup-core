using System.Threading.Tasks;
using HLS.Paygate.Balance.Domain.Services;
using HLS.Paygate.Gw.Model.Commands;
using HLS.Paygate.Shared;
using MassTransit;
using Microsoft.Extensions.Logging;
using Paygate.Discovery.Requests.Balance;
using ServiceStack;

namespace HLS.Paygate.Balance.Components.Consumers;

public class CancelPaymentConsumer : IConsumer<PaymentCancelCommand>
{
    private readonly IBalanceService _balanceService;

    //private readonly IServiceGateway _gateway; gunner

    //private readonly Logger _logger = LogManager.GetLogger("CancelPaymentConsumer");
    private readonly ILogger<CancelPaymentConsumer> _logger;

    public CancelPaymentConsumer(ILogger<CancelPaymentConsumer> logger, IBalanceService balanceService)
    {
        _logger = logger;
        _balanceService = balanceService;
        //_gateway = HostContext.AppHost.GetServiceGateway(); gunner
    }

    public async Task Consume(ConsumeContext<PaymentCancelCommand> context)
    {
        var saleRequest = context.Message;
        _logger.LogInformation("CancelPaymentConsumer recevied: " + saleRequest.ToJson());
        var response = await _balanceService.CancelPaymentAsync(new BalanceCancelPaymentRequest
        {
            TransRef = saleRequest.TransCode,
            RevertAmount = saleRequest.RevertAmount,
            TransNote = saleRequest.TransNote,
            Description = saleRequest.TransNote,
            TransactionCode = saleRequest.PaymentTransCode,
            CurrencyCode = CurrencyCode.VND.ToString("G"),
            AccountCode = saleRequest.AccountCode
        });
        _logger.LogInformation("CancelPaymentConsumer return: " + response.ToJson());
    }
}