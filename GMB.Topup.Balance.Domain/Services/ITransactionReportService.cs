using System;
using System.Threading.Tasks;
using GMB.Topup.Balance.Models.Dtos;
using GMB.Topup.Shared;
using GMB.Topup.Discovery.Requests.Balance;

namespace GMB.Topup.Balance.Domain.Services;

// public interface ITransactionReportService
// {
//     Task<MessageResponseBase> BalanceHistoryCreateAsync(TransactionDto transaction, SettlementDto settlement);
//     Task<ResponseMesssageObject<string>> BalanceHistoriesGetAsync(BalanceHistoriesRequest request);
//     Task<MessageResponseBase> BalanceHistoryGetAsync(BalanceHistoryRequest request);

//     Task<decimal> GetBalanceDateGetAsync(BalanceMaxDateRequest request);

//     Task UpdateBalanceHistories(string account, DateTime fromDate, DateTime todate,
//         decimal balanceBefore, bool isDesAccount = true);
// }