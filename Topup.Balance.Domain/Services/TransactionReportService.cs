using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Topup.Balance.Domain.Entities;
using Topup.Balance.Domain.Repositories;
using Topup.Balance.Models.Dtos;
using Topup.Shared;
using Microsoft.Extensions.Logging;
using Topup.Discovery.Requests.Balance;
using ServiceStack;

namespace Topup.Balance.Domain.Services;

// public class TransactionReportService : ITransactionReportService
// {
//     private readonly IBalanceMongoRepository _balanceMongoRepository;

//     //private readonly Logger _logger = LogManager.GetLogger("TransactionReportService");
//     private readonly ILogger<TransactionReportService> _logger;

//     public TransactionReportService(IBalanceMongoRepository balanceMongoRepository,
//         ILogger<TransactionReportService> logger)
//     {
//         _balanceMongoRepository = balanceMongoRepository;
//         _logger = logger;
//     }

//     public async Task<MessageResponseBase> BalanceHistoryCreateAsync(TransactionDto transaction,
//         SettlementDto settlement)
//     {
//         try
//         {
//             await _balanceMongoRepository.AddOneAsync(GetBalanceHistoryInsert(transaction, settlement));
//             return new MessageResponseBase
//             {
//                 ResponseCode = ResponseCodeConst.Success,
//                 ResponseMessage = "Thành công"
//             };
//         }
//         catch (Exception ex)
//         {
//             _logger.LogError($"BalanceHistoryCreateAsync error: {ex}");
//             return new MessageResponseBase
//             {
//                 ResponseCode = ResponseCodeConst.Error,
//                 ResponseMessage = "BalanceHistoryCreateAsync error: " + ex
//             };
//         }
//     }

//     public async Task<ResponseMesssageObject<string>> BalanceHistoriesGetAsync(BalanceHistoriesRequest request)
//     {
//         try
//         {
//             Expression<Func<BalanceHistories, bool>> query = p =>
//             p.CreatedDate >= request.FromDate.ToUniversalTime()
//             && p.CreatedDate <= request.ToDate.ToUniversalTime();

//             if (!string.IsNullOrEmpty(request.TransCode))
//             {
//                 Expression<Func<BalanceHistories, bool>> newQuery = p => p.TransCode == request.TransCode;
//                 query = query.And(newQuery);
//             }

//             if (!string.IsNullOrEmpty(request.TransRef))
//             {
//                 Expression<Func<BalanceHistories, bool>> newQuery = p => p.TransRef == request.TransRef;
//                 query = query.And(newQuery);
//             }

//             if (!string.IsNullOrEmpty(request.AccountCode))
//             {
//                 Expression<Func<BalanceHistories, bool>> newQuery = p => p.DesAccountCode == request.AccountCode || p.SrcAccountCode == request.AccountCode;
//                 query = query.And(newQuery);
//             }

//             var lst = await _balanceMongoRepository.GetAllAsync<BalanceHistories, Guid>(query);
//             return new ResponseMesssageObject<string>
//             {
//                 ResponseCode = ResponseCodeConst.Success,
//                 ResponseMessage = "Thành công",
//                 Total = lst.Count(),
//                 Payload = lst.ToJson()
//             };
//         }
//         catch (Exception e)
//         {
//             _logger.LogError("BalanceHistoriesGetAsync error " + e);
//             return new ResponseMesssageObject<string>
//             {
//                 ResponseCode = ResponseCodeConst.Error,
//                 ResponseMessage = "Thất bại",
//                 Total = 0,
//             };
//         }
//     }


//     public async Task<MessageResponseBase> BalanceHistoryGetAsync(BalanceHistoryRequest request)
//     {
//         var item = await _balanceMongoRepository.GetOneAsync<BalanceHistories>(x =>
//             x.TransCode == request.TransCode);
//         return new MessageResponseBase
//         {
//             ResponseCode = ResponseCodeConst.Success,
//             Payload = item.ConvertTo<BalanceHistoryDto>()
//         };
//     }

//     public async Task<decimal> GetBalanceDateGetAsync(BalanceMaxDateRequest request)
//     {
//         return await _balanceMongoRepository.GetAccountBalanceMaxDateAsync(request.AccountCode, request.MaxDate);
//     }

//     private BalanceHistories GetBalanceHistoryInsert(TransactionDto transaction, SettlementDto settlement)
//     {
//         //Chỗ này k dùng automap vì 1 số tham số đặc biệt
//         var item = new BalanceHistories
//         {
//             TransRef = transaction.TransRef,
//             Amount = transaction.Amount,
//             CurrencyCode = settlement.CurrencyCode,
//             Status = transaction.Status,
//             ModifiedDate = transaction.ModifiedDate,
//             ModifiedBy = transaction.ModifiedBy,
//             CreatedBy = transaction.CreatedBy,
//             CreatedDate = transaction.CreatedDate,
//             Description = transaction.Description,
//             TransNote = transaction.TransNote,
//             RevertTransCode = transaction.RevertTransCode,
//             TransactionType = transaction.TransType.ToString("G"),
//             SrcAccountCode = settlement.SrcAccountCode,
//             DesAccountCode = settlement.DesAccountCode,
//             SrcAccountBalance = settlement.SrcAccountBalance,
//             DesAccountBalance = settlement.DesAccountBalance,
//             SrcAccountBalanceBeforeTrans = settlement.SrcAccountBalanceBeforeTrans,
//             DesAccountBalanceBeforeTrans = settlement.DesAccountBalanceBeforeTrans,
//             TransCode = transaction.TransactionCode,
//             TransType = transaction.TransType
//         };
//         if (item.TransType == TransactionType.MasterTopup)
//         {
//             item.SrcAccountBalanceBeforeTrans = 0;
//             item.SrcAccountBalance = 0;
//         }

//         return item;
//     }

//     #region Sys

//     public async Task<List<BalanceHistories>> GetListBalanceHistories(BalanceHistoriesRequest request)
//     {
//         try
//         {
//             Expression<Func<BalanceHistories, bool>> query = p => true;
//             if (!string.IsNullOrEmpty(request.TransCode))
//             {
//                 Expression<Func<BalanceHistories, bool>> newQuery = p => p.TransCode == request.TransCode;
//                 query = query.And(newQuery);
//             }

//             if (!string.IsNullOrEmpty(request.TransRef))
//             {
//                 Expression<Func<BalanceHistories, bool>> newQuery = p => p.TransRef == request.TransRef;
//                 query = query.And(newQuery);
//             }            

//             if (!string.IsNullOrEmpty(request.AccountCode))
//             {
//                 Expression<Func<BalanceHistories, bool>> newQuery = p =>
//                     p.DesAccountCode == request.AccountCode || p.SrcAccountCode == request.AccountCode;
//                 query = query.And(newQuery);
//             }

//             if (request.FromDate != null)
//             {
//                 Expression<Func<BalanceHistories, bool>> newQuery = p =>
//                     p.CreatedDate >= request.FromDate.ToUniversalTime();
//                 query = query.And(newQuery);
//             }

//             if (request.ToDate != null)
//             {
//                 Expression<Func<BalanceHistories, bool>> newQuery = p =>
//                     p.CreatedDate <= request.ToDate.ToUniversalTime();
//                 query = query.And(newQuery);
//             }


//             var lst = await _balanceMongoRepository.GetAllAsync(query);
//             return lst;
//         }
//         catch (Exception e)
//         {
//             Console.WriteLine(e);
//             return null;
//         }
//     }

//     public async Task UpdateBalanceHistories(string account, DateTime fromDate, DateTime todate,
//         decimal balanceBefore, bool isDesAccount = true)
//     {
//         var lst = await GetListBalanceHistories(new BalanceHistoriesRequest
//         {
//             AccountCode = account,
//             FromDate = fromDate,
//             ToDate = todate
//         });
//         if (lst.Count > 0)
//             foreach (var item in lst.OrderBy(x => x.CreatedDate))
//                 if (isDesAccount)
//                 {
//                     item.DesAccountBalanceBeforeTrans = balanceBefore;
//                     item.DesAccountBalance = item.DesAccountBalanceBeforeTrans + item.Amount;
//                     balanceBefore = item.DesAccountBalance; //Gán lại số dư sau giao dịch
//                 }
//                 else
//                 {
//                     item.DesAccountBalanceBeforeTrans = balanceBefore;
//                     item.DesAccountBalance = item.DesAccountBalanceBeforeTrans - item.Amount;
//                     balanceBefore = item.DesAccountBalance; //Gán lại số dư sau giao dịch
//                 }
//     }

//     #endregion
// }