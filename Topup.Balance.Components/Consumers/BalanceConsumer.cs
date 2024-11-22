using System;
using System.Threading.Tasks;
using Topup.Balance.Domain.Services;
using Topup.Balance.Models.Events;
using Topup.Balance.Models.Requests;
using MassTransit;
using Microsoft.Extensions.Logging;
using ServiceStack;

namespace Topup.Balance.Components.Consumers;

public class BalanceConsumer : IConsumer<BalanceChanging>, IConsumer<BalanceChanged>, IConsumer<BalanceDepositMessage>
{
    //private readonly IServiceGateway _gateway; gunner
    private readonly ILogger<BalanceConsumer> _logger;
    private readonly IBalanceService _balanceService;

    public BalanceConsumer(ILogger<BalanceConsumer> logger, IBalanceService balanceService)
    {
        _logger = logger;
        _balanceService = balanceService;
        //_gateway = HostContext.AppHost.GetServiceGateway(); gunner
    }

    public async Task Consume(ConsumeContext<BalanceChanged> context)
    {
        try
        {
            _logger.LogInformation($"Consume BalanceChanged Message: {context.Message.AccountBalance.AccountCode}");
            var request = context.Message;

            await _balanceService.AccountBalanceUpdateAsync(request.AccountBalance);
        }
        catch (Exception e)
        {
            _logger.LogInformation($"Consume BalanceChanged error: {e}");
        }
    }

    public Task Consume(ConsumeContext<BalanceChanging> context)
    {
        throw new NotImplementedException();
    }

    public async Task Consume(ConsumeContext<BalanceDepositMessage> context)
    {
        try
        {
            _logger.LogInformation($"Consume BalanceDeposit Message: {context.Message.TransRef}");
            var request = context.Message;
            var response = await _balanceService.DepositAsync(new DepositRequest
            {
                TransRef = request.TransRef,
                TransNote = request.TransNote,
                Description = request.Description,
                CurrencyCode = request.CurrencyCode,
                AccountCode = request.AccountCode,
                Amount = request.Amount
            });
            _logger.LogInformation("Deposit Auto return: " + response.ToJson());
        }
        catch (Exception e)
        {
            _logger.LogInformation($"Consume BalanceChanged error: {e}");
        }
    }
}