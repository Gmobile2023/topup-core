using System.Threading.Tasks;
using Topup.Gw.Domain.Services;
using Topup.Shared;
using Topup.Shared.AbpConnector;
using Microsoft.Extensions.Logging;
using ServiceStack;

namespace Topup.Worker.Components.Connectors;

public class CheckLimitTransaction
{
    private readonly ExternalServiceConnector _externalServiceConnector;
    private readonly ILogger<CheckLimitTransaction> _logger;
    private readonly ITransactionService _transactionService;

    public CheckLimitTransaction(ITransactionService transactionService,
        ExternalServiceConnector externalServiceConnector, ILogger<CheckLimitTransaction> logger)
    {
        _transactionService = transactionService;
        _externalServiceConnector = externalServiceConnector;
        _logger = logger;
    }

    public async Task<MessageResponseBase> CheckLimitProductPerDay(string accountcoe, string productcode,
        decimal amount, int quantity,string transcode="")
    {
        _logger.LogInformation($"{transcode} CheckLimitProductPerDay request:{transcode} - {accountcoe}-{productcode}-{amount}-{quantity}");
        var getTotalDay =
            await _transactionService.GetLimitProductTransPerDay(accountcoe, productcode);
        _logger.LogInformation($"{transcode} GetLimitProductTransPerDay return:{getTotalDay.ToJson()}");
        if (getTotalDay == null)
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = "Thông tin hạn mức sản phẩm không tồn tại"
            };
        var totalAmount = getTotalDay.TotalAmount + amount * quantity;
        var totalQuantity = getTotalDay.TotalQuantity + quantity;

        var checkLimit = await _externalServiceConnector.CheckLimitProductPerDay(transcode,accountcoe,
            productcode, totalAmount, totalQuantity);
        _logger.LogInformation($"{transcode} - CheckLimitProductPerDay return:{checkLimit.ToJson()}");
        if (!checkLimit.Success)
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage = checkLimit.Error.Message
            };
        var detail = checkLimit.Result;
        if (detail == null)
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Success,
                ResponseMessage = "Thành công"
            };
        if (detail.LimitQuantity != null && totalQuantity > detail.LimitQuantity)
        {
            _logger.LogInformation($"{transcode} Product over limit total perday");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage =
                    "Sản phẩm đã vượt quá số lượng thanh toán cho phép trong ngày. Vui lòng quay lại sau"
            };
        }

        if (detail.LimitAmount != null && totalAmount > detail.LimitAmount)
        {
            _logger.LogInformation($"{transcode} Product over limit totalAmount perday");
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Error,
                ResponseMessage =
                    "Sản phẩm đã vượt quá số tiền thanh toán cho phép trong ngày. Vui lòng quay lại sau"
            };
        }

        return new MessageResponseBase
        {
            ResponseCode = ResponseCodeConst.Success,
            ResponseMessage = "Thành công"
        };
    }
}