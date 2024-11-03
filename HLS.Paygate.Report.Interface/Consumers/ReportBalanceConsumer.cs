using System;
using System.Threading.Tasks;
using HLS.Paygate.Gw.Model.Events;
using HLS.Paygate.Report.Domain.Services;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace HLS.Paygate.Report.Interface.Consumers;

public class ReportBalanceConsumer : IConsumer<ReportBalanceHistoriesMessage>
{
    private readonly ILogger<ReportBalanceConsumer> _logger;
    private readonly IBalanceReportService _transactionReportService;

    public ReportBalanceConsumer(IBalanceReportService transactionReportService, ILogger<ReportBalanceConsumer> logger)
    {
        _transactionReportService = transactionReportService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ReportBalanceHistoriesMessage> context)
    {
        try
        {        
            await _transactionReportService.CreateReportTransDetail(context.Message);         
        }
        catch (Exception e)
        {
            _logger.LogInformation($"ReportBalanceConsumer error: {e}");
        }
    }
}