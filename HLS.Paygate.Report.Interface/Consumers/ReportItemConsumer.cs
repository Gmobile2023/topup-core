using System;
using System.Threading.Tasks;
using HLS.Paygate.Gw.Model.Events;
using HLS.Paygate.Report.Domain.Services;
using MassTransit;
using Microsoft.Extensions.Logging;
using Paygate.Contracts.Commands.Commissions;
using ServiceStack;

namespace HLS.Paygate.Report.Interface.Consumers;

public class ReportItemConsumer :
    IConsumer<ReportSaleMessage>,
    IConsumer<ReportTransStatusMessage>,
    IConsumer<ReportCompensationHistoryMessage>,
    IConsumer<ReportSyncAccounMessage>,
    IConsumer<CommissionReportCommand>
{
    private readonly ILogger<ReportItemConsumer> _logger;
    private readonly IBalanceReportService _reportBalanceService;

    public ReportItemConsumer(IBalanceReportService reportBalanceService,
        ILogger<ReportItemConsumer> logger)
    {
        _reportBalanceService = reportBalanceService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<CommissionReportCommand> context)
    {
        _logger.LogInformation($"{context.Message.TransCode} CommissionReportMessage Message: {context.Message.ToJson()}");
        var item = new ReportCommistionMessage
        {
            Type = context.Message.Type,
            CommissionAmount = context.Message.CommissionAmount,
            CommissionDate = context.Message.CommissionDate,
            Status = context.Message.Status,
            ParentCode = context.Message.ParentCode,
            TransCode = context.Message.TransCode,
            CommissionCode = context.Message.CommissionCode
        };
        var reponse = await _reportBalanceService.ReportCommistionMessage(item);
        if (!reponse)
            await context.Publish<CommissionReportCommand>(new
            {
                context.Message.Type,
                context.Message.TransCode,
                context.Message.ParentCode,
                context.Message.CommissionCode,
                context.Message.CommissionAmount,
                context.Message.CommissionDate,
                context.Message.Status,
                CorrelationId = Guid.NewGuid()
            });
    }

    public async Task Consume(ConsumeContext<ReportCompensationHistoryMessage> context)
    {
        _logger.LogInformation(
            $"{context.Message.PaidTransCode} ReportCompensationHistory Message: {context.Message.ToJson()}");
        await _reportBalanceService.ReportCompensationHistoryMessage(context.Message);
    }

    public async Task Consume(ConsumeContext<ReportSaleMessage> context)
    {
        _logger.LogInformation($"{context.Message.TransRef} ReportSaleMessage: {context.Message.TransCode}-{context.Message.TransRef}");
        await _reportBalanceService.ReportSaleIntMessage(context.Message);       
    }

    public async Task Consume(ConsumeContext<ReportSyncAccounMessage> context)
    {
        _logger.LogInformation(
            $"{context.Message.UserId}|{context.Message.AccountCode} ReportSyncAccounMessage: {context.Message.ToJson()}");
        await Task.Delay(10000);
        await _reportBalanceService.ReportSyncAccountFullInfoRequest(context.Message);
    }

    public async Task Consume(ConsumeContext<ReportTransStatusMessage> context)
    {
        _logger.LogInformation(
            $"{context.Message.TransCode} ReportTransStatusMessage: {context.Message.ToJson()}");
        await _reportBalanceService.ReportStatusMessage(context.Message);
    }

    public async Task Consume(ConsumeContext<ReportRefundMessage> context)
    {
        _logger.LogInformation($"ReportRefundMessage: {context.Message.ToJson()}");
        await _reportBalanceService.ReportRefundInfoMessage(context.Message);
    }
}