using System.Threading.Tasks;
using HLS.Paygate.Common.Domain.Services;
using HLS.Paygate.Common.Model.Dtos.RequestDto;
using MassTransit;
using Microsoft.Extensions.Logging;
using Paygate.Contracts.Commands.Commons;
using ServiceStack;

namespace HLS.Paygate.Common.Interface.Consumers;

public class CommonConsumer : IConsumer<PayBillSaveCommand>
{
    private readonly ICommonAppService _commonService;
    private readonly ILogger<CommonConsumer> _logger;

    public CommonConsumer(ILogger<CommonConsumer> logger,
        ICommonAppService transactionReportService)
    {
        _logger = logger;
        _commonService = transactionReportService;
    }

    public async Task Consume(ConsumeContext<PayBillSaveCommand> context)
    {
        _logger.LogInformation("PayBillSaveMessage request: {Message}", context.Message.ToJson());
        var rs = await _commonService.SavePayBill(context.Message.ConvertTo<SavePayBillRequest>());
        _logger.LogInformation("PayBillSaveMessage response: {Response}", rs.ToJson());
    }
}