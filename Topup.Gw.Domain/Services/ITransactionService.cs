using System.Threading.Tasks;
using Topup.Gw.Model.Dtos;
using Topup.Gw.Model.RequestDtos;
using Topup.Shared;
using Topup.Shared.Dtos;
using Topup.Discovery.Requests.Backends;

namespace Topup.Gw.Domain.Services;

public interface ITransactionService
{
    Task<InvoiceDto> InvoiceCreateAync(InvoiceDto input);
    Task<SaleHistoryDto> GetSaleRequest(GetSaleRequest input);
    Task<MessagePagedResponseBase> GetTopupHistoriesAsync(GetTopupHistoryRequest request);
    Task<MessagePagedResponseBase> GetSaleTransactionDetailAsync(GetTopupItemsRequest request);
    Task<MessagePagedResponseBase> GetPayBatchBillRequest(GetPayBatchBill request);

    Task<decimal> GetTotalAmountPerDay(string accountCode, string serviceCode, string categoryCode,string productCode);

    Task<AccountProductLimitDto> GetLimitProductTransPerDay(string accountCode, string productCode);
    
    Task<ResponseMesssageObject<string>> GetSaleTopupRequest(GetSaleTopupRequest input);

    Task<ResponseMesssageObject<string>> GetCardBatchRequest(GetCardBatchRequest input);

    Task<MessagePagedResponseBase> GetOffsetTopupHistoriesAsync(GetOffsetTopupHistoryRequest request);
}