using System.Threading.Tasks;
using GMB.Topup.Common.Domain.Services;
using GMB.Topup.Common.Model.Dtos.RequestDto;
using MassTransit;
using Microsoft.Extensions.Logging;
using GMB.Topup.Contracts.Commands.Commons;
using ServiceStack;

namespace GMB.Topup.Common.Interface.Consumers;

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