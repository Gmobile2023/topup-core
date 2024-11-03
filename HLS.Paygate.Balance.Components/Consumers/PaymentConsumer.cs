﻿using System;
using System.Threading.Tasks;
using HLS.Paygate.Balance.Domain.Services;
using HLS.Paygate.Gw.Model.Commands;
using HLS.Paygate.Shared;
using MassTransit;
using Microsoft.Extensions.Logging;
using Paygate.Discovery.Requests.Balance;
using ServiceStack;

namespace HLS.Paygate.Balance.Components.Consumers;

public class PaymentConsumer : IConsumer<PaymentProcessCommand>
{
    //private readonly IServiceGateway _gateway; gunner

    private readonly ILogger<PaymentConsumer> _logger;
    private readonly IBalanceService _balanceService;

    public PaymentConsumer(ILogger<PaymentConsumer> logger, IBalanceService balanceService)
    {
        _logger = logger;
        _balanceService = balanceService;
        //_gateway = HostContext.AppHost.GetServiceGateway(); gunner
    }

    public async Task Consume(ConsumeContext<PaymentProcessCommand> context)
    {
        var saleRequest = context.Message;
        _logger.LogInformation("PaymentConsumer_received: " + saleRequest.ToJson());
        var response = await _balanceService.PaymentAsync(new BalancePaymentRequest()
        {
            AccountCode = saleRequest.AccountCode,
            CurrencyCode = saleRequest.CurrencyCode,
            TransRef = saleRequest.TransRef,
            PaymentAmount = saleRequest.PaymentAmount,
            TransNote = saleRequest.TransNote
        });
        _logger.LogInformation($"{saleRequest.TransRef}-PaymentConsumer_response: " + response.ToJson());
        await context.RespondAsync<MessageResponseBase>(new
        {
            context.Message.CorrelationId,
            ReceiveTime = DateTime.Now,
            response.ResponseCode,
            response.ResponseMessage,
            response.Payload,
            response.ExtraInfo
        });
    }
}