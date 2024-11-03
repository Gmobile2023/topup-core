using System.Threading.Tasks;
using GMB.Topup.Gw.Model.Dtos;
using GMB.Topup.Gw.Model.RequestDtos;
using GMB.Topup.Shared;
using GMB.Topup.Shared.Dtos;
using GMB.Topup.Discovery.Requests.Backends;

namespace GMB.Topup.Gw.Domain.Services;

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