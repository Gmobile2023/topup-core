using System.Collections.Generic;
using System.Threading.Tasks;
using HLS.Paygate.Common.Model.Dtos;
using HLS.Paygate.Common.Model.Dtos.RequestDto;
using HLS.Paygate.Common.Model.Dtos.ResponseDto;
using HLS.Paygate.Shared;

namespace HLS.Paygate.Common.Domain.Services;

public interface ICommonAppService
{
    Task<bool> SavePayBill(SavePayBillRequest request);
    Task<bool> RemoveSavePayBill(RemoveSavePayBillRequest request);
    Task<List<PayBillAccountsDto>> GetSavePayBill(GetSavePayBillRequest request);
    Task<long> GetTotalWaitingBill(GetTotalWaitingBillRequest request);
    Task AutoCheckPayBill();
    Task WarningBalance();
    Task<bool> AlarmBalanceCreateAsync(AlarmBalanceConfigDto request);
    Task<bool> AlarmBalanceUpdateAsync(AlarmBalanceConfigDto request);
    Task<AlarmBalanceConfigDto> AlarmBalanceGetAsync(string accountCode, string currencycode);
    Task<MessagePagedResponseBase> GetListAlarmBalanceGetAsync(GetAllAlarmBalanceRequest request);
}