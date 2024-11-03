﻿using System;
using System.Threading.Tasks;
using HLS.Paygate.Gw.Domain.Services;
using HLS.Paygate.Gw.Model.Commands;
using HLS.Paygate.Gw.Model.RequestDtos;
using HLS.Paygate.Shared;
using MassTransit;
using Microsoft.Extensions.Logging;
using Paygate.Contracts.Commands.Backend;
using ServiceStack;

namespace HLS.Paygate.Backend.Interface.Consumers
{
    public class TransactionRefundConsumer : IConsumer<TransactionRefundCommand>, IConsumer<CallBackCorrectTransCommand>, IConsumer<TransGatePushCommand>
    {
        private readonly ILogger<TransactionRefundConsumer> _logger;
        private readonly ISaleService _saleService;

        public TransactionRefundConsumer(ISaleService saleService,
            ILogger<TransactionRefundConsumer> logger)
        {
            _saleService = saleService;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<CallBackCorrectTransCommand> context)
        {
            try
            {
                _logger.LogInformation("CallBackCorrectTransCommand comming request: " + context.Message.TransCode);
                await _saleService.TransactionCallBackCorrect(new CallBackCorrectTransRequest
                {
                    ResponseCode = context.Message.ResponseCode,
                    ResponseMessage = context.Message.ResponseMessage,
                    TransCode = context.Message.TransCode
                });
            }
            catch (Exception e)
            {
                _logger.LogError($"CallBackCorrectTransCommand error: {e}");
            }
        }

        public async Task Consume(ConsumeContext<TransactionRefundCommand> context)
        {
            try
            {
                _logger.LogInformation("TransactionRefundConsumer comming request: " + context.Message.TransCode);
                var transCode = context.Message.TransCode;
                var canelResponse = await _saleService.RefundTransaction(transCode);
                _logger.LogInformation("TransactionRefundConsumer return: " + canelResponse.ToJson());
                await context.RespondAsync<MessageResponseBase>(new
                {
                    Id = context.Message.CorrelationId,
                    ReceiveTime = DateTime.Now,
                    ResponseCode = canelResponse.ResponseStatus.ErrorCode,
                    ResponseMessage = canelResponse.ResponseStatus.Message
                });
            }
            catch (Exception e)
            {
                _logger.LogError($"TransactionRefundConsumer error: {e}");
                await context.RespondAsync<MessageResponseBase>(new
                {
                    Id = context.Message.CorrelationId,
                    ReceiveTime = DateTime.Now,
                    ResponseCode = ResponseCodeConst.Error,
                    ResponseMessage = e.Message
                });
            }
        }

        public async Task Consume(ConsumeContext<TransGatePushCommand> context)
        {
            try
            {
                var message = context.Message;
                _logger.LogInformation($"TransGatePushCommand Input request: {message.ToJson()}");
                var saleDto = message.ConvertTo<Gw.Model.Dtos.SaleGateRequestDto>();
                saleDto.TopupAmount = saleDto.FirstAmount;
                await _saleService.SaleGateCreateAsync(saleDto);
            }
            catch (Exception e)
            {
                _logger.LogError($"TransGatePushCommand error: {e}");
            }
        }
    }
}