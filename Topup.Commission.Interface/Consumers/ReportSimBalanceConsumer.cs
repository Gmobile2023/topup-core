using System;
using System.Threading.Tasks;
using HLS.Paygate.Gw.Model.Events;
using MassTransit;
using ServiceStack;
using NLog;
using HLS.Paygate.Report.Domain.Services;

namespace HLS.Paygate.Report.Interface.Consumers
{
    public class ReportSimBalanceConsumer : IConsumer<SimBalanceMessage>
    {
        private readonly Logger _logger = LogManager.GetLogger("ReportSimBalanceConsumer");
        private readonly ISimBalanceReportService _simBalanceReportService;

        public ReportSimBalanceConsumer(ISimBalanceReportService simBalanceReportService)
        {
            _simBalanceReportService = simBalanceReportService;
        }

        public async Task Consume(ConsumeContext<SimBalanceMessage> context)
        {
            try
            {
                _logger.LogInformation($"ReportSimBalanceConsumer request: {context.Message.ToJson()}");
                var rs = await _simBalanceReportService.SimBalanceReportInsertAsync(context.Message);
                _logger.LogInformation($"ReportSimBalanceConsumer return: {rs.ToJson()}");
                var reportDate = await _simBalanceReportService.CreateOrUpdateReportSimBalanceDate(context.Message);
                _logger.LogInformation($"CreateOrUpdateReportSimBalanceDate return: {reportDate.ToJson()}");
            }
            catch (Exception e)
            {
                _logger.LogInformation($"ReportCardStockConsumer error: {e}");
            }
        }
    }
}
