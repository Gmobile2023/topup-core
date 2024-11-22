using System;
using System.Threading.Tasks;
using Topup.Contracts.Commands.Backend;
using Topup.Gw.Domain.Services;
using Topup.Gw.Model.Commands;
using Topup.Gw.Model.Dtos;
using Topup.Gw.Model.RequestDtos;
using Topup.Shared;
using MassTransit;
using Microsoft.Extensions.Logging;

using ServiceStack;

namespace Topup.Backend.Interface.Consumers
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
                var saleDto = message.ConvertTo<SaleGateRequestDto>();
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