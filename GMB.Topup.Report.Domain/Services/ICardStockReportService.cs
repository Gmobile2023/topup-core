using System.Threading.Tasks;
using GMB.Topup.Shared.Contracts.Events.Report;
using GMB.Topup.Report.Model.Dtos.RequestDto;
using GMB.Topup.Shared;

namespace GMB.Topup.Report.Domain.Services;

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