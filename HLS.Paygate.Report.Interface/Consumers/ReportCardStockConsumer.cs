using System;
using System.Threading.Tasks;
using HLS.Paygate.Shared.Contracts.Events.Report;
using HLS.Paygate.Report.Domain.Services;
using MassTransit;
using Microsoft.Extensions.Logging;
using ServiceStack;

namespace HLS.Paygate.Report.Interface.Consumers;

public class ReportCardStockConsumer : IConsumer<ReportCardStockMessage>
{
    private readonly ILogger<ReportCardStockConsumer> _logger;
    private readonly ICardStockReportService _transactionReportService;

    public ReportCardStockConsumer(ICardStockReportService transactionReportService,
        ILogger<ReportCardStockConsumer> logger)
    {
        _transactionReportService = transactionReportService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ReportCardStockMessage> context)
    {
        try
        {
            _logger.LogInformation($"ReportCardStockConsumer request: {context.Message.ToJson()}");
            var rs = await _transactionReportService.CardStockReportInsertAsync(context.Message);
            _logger.LogInformation($"ReportCardStockConsumer return: {rs.ToJson()}");
            var reportDate = await _transactionReportService.CreateOrUpdateReportCardStockDate(context.Message);
            _logger.LogInformation($"CreateOrUpdateReportCardStockDate return: {reportDate.ToJson()}");

            var reportVenderDate =
                await _transactionReportService.CreateOrUpdateReportCardStockProviderDate(context.Message);
            _logger.LogInformation($"CreateOrUpdateReportCardStockProviderDate return: {reportVenderDate.ToJson()}");
        }
        catch (Exception e)
        {
            _logger.LogInformation($"ReportCardStockConsumer error: {e}");
        }
    }
}