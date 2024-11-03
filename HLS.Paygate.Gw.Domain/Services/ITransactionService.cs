using System.Threading.Tasks;
using HLS.Paygate.Gw.Model.Dtos;
using HLS.Paygate.Gw.Model.RequestDtos;
using HLS.Paygate.Shared;
using HLS.Paygate.Shared.Dtos;
using Paygate.Discovery.Requests.Backends;

namespace HLS.Paygate.Gw.Domain.Services;

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