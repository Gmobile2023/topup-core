using System.Threading.Tasks;
using Topup.Shared.Contracts.Events.Report;
using Topup.Report.Model.Dtos.RequestDto;
using Topup.Shared;

namespace Topup.Report.Domain.Services;

public interface ICardStockReportService
{
    Task<MessageResponseBase> CardStockReportInsertAsync(ReportCardStockMessage request);
    Task<MessageResponseBase> CreateOrUpdateReportCardStockDate(ReportCardStockMessage request);
    Task<MessageResponseBase> CreateOrUpdateReportCardStockProviderDate(ReportCardStockMessage request);
    Task<MessagePagedResponseBase> CardStockHistories(CardStockHistoriesRequest request);
    Task<MessagePagedResponseBase> CardStockInventory(CardStockInventoryRequest request);
    Task<MessagePagedResponseBase> CardStockImExPort(CardStockImExPortRequest request);
    Task<MessagePagedResponseBase> CardStockImExPortProvider(CardStockImExPortProviderRequest request);

    Task SysCardStockInventoryDay();
}