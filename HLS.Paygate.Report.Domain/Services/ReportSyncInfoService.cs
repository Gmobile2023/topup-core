using HLS.Paygate.Gw.Model.Commands;
using HLS.Paygate.Gw.Model.Dtos;
using HLS.Paygate.Gw.Model.Events;
using HLS.Paygate.Report.Domain.Entities;
using HLS.Paygate.Report.Model.Dtos;
using HLS.Paygate.Report.Model.Dtos.RequestDto;
using HLS.Paygate.Shared;
using HLS.Paygate.Shared.ConfigDtos;
using MassTransit;
using Microsoft.Extensions.Logging;
using NPOI.HPSF;
using Paygate.Discovery.Requests.Backends;
using Paygate.Discovery.Requests.Balance;
using ServiceStack;
using ServiceStack.MiniProfiler;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using static System.Runtime.CompilerServices.RuntimeHelpers;

namespace HLS.Paygate.Report.Domain.Services
{
    public partial class BalanceReportService
    {
        #region I.Bù giao dịch thiếu ở ReportItem so với SaleRequest
        /// <summary>
        /// Bù giao dịch thiếu từ SaleRequest => ReportItems
        /// 1.Danh sách mã giao dịch
        /// 2.Theo ngày
        /// </summary>
        /// <param name="inputTrans"></param>
        /// <param name="date"></param>
        /// <returns></returns>
        private async Task CompensationTranSale(string inputTrans, DateTime? date)
        {
            if (!string.IsNullOrEmpty(inputTrans))
            {
                var tranCodes = inputTrans.Split('|', ';', ',').ToList();
                await CompensationTranSale_Items(tranCodes);
            }
            else if (date != null)
                await CompensationTranSale_Date(date.Value);
        }

        private async Task CompensationTranSale_Items(List<string> tranCodes)
        {
            foreach (var tranCode in tranCodes)
            {
                Expression<Func<ReportItemDetail, bool>> query = p => p.TransCode == tranCode;
                var firstItem = await _reportMongoRepository.GetOneAsync(query);
                if (firstItem == null)
                {
                    var saleRequest = await getSaleTopupSingleByTransCode(tranCode);
                    if (saleRequest != null)
                    {
                        string payTransCode = "";
                        if (string.IsNullOrEmpty(saleRequest.PaymentTransCode))
                            return;

                        var paidTrans = await _reportMongoRepository.GetReportBalanceHistoriesByTransCode(saleRequest.PaymentTransCode);
                        var item = await ConvertInfoSale(new ReportSaleMessage()
                        {
                            CorrelationId = saleRequest.Id,
                            PerformAccount = saleRequest.StaffAccount,
                            AccountCode = saleRequest.PartnerCode,
                            CreatedDate = saleRequest.CreatedTime,
                            ProductCode = saleRequest.ProductCode,
                            Amount = saleRequest.Amount,
                            Price = saleRequest.Price,
                            Quantity = saleRequest.Quantity,
                            PaymentAmount = saleRequest.PaymentAmount,
                            Discount = saleRequest.DiscountAmount ?? 0,
                            Fee = saleRequest.Fee ?? 0,
                            ReceivedAccount = saleRequest.ReceiverInfo,
                            ServiceCode = saleRequest.ServiceCode,
                            TransRef = saleRequest.TransRef,
                            TransCode = saleRequest.TransCode,
                            Balance = Convert.ToDecimal(paidTrans != null ? paidTrans.SrcAccountBalanceAfterTrans : 0),
                            PaidTransCode = saleRequest.PaymentTransCode,
                            PayTransCode = string.IsNullOrEmpty(payTransCode) ? saleRequest.ProviderTransCode : payTransCode,
                            Status = (byte)saleRequest.Status,
                            ProviderCode = saleRequest.Provider,
                            VendorCode = saleRequest.Vendor,
                            NextStep = 0,
                            Channel = saleRequest.Channel.ToString("G"),
                            ExtraInfo = saleRequest.ExtraInfo,
                            ParentProvider = saleRequest.ParentProvider,
                            ProviderReceiverType = saleRequest.ReceiverTypeResponse,
                            ProviderTransCode = saleRequest.ProviderTransCode,
                            ReceiverType = saleRequest.ReceiverType,
                            ParentCode = saleRequest.ParentCode,
                        });

                        if (saleRequest.Status == SaleRequestStatus.Canceled
                            || saleRequest.Status == SaleRequestStatus.Failed
                            || saleRequest.Status == SaleRequestStatus.Undefined)
                            item.Status = ReportStatus.Error;
                        else if (saleRequest.Status == SaleRequestStatus.Success)
                            item.Status = ReportStatus.Success;
                        else item.Status = ReportStatus.TimeOut;

                        await _reportMongoRepository.AddOneAsync(item);
                    }
                }
            }
        }

        private async Task CompensationTranSale_Date(DateTime date)
        {
            var toDate = date.Date.AddDays(1).AddSeconds(-1);
            var sales = await getSaleTopupDataTimeFill(date.Date);
            _logger.LogInformation($"Sale Total: {sales.Count()}");
            Expression<Func<ReportItemDetail, bool>> querySearch = p =>
                p.CreatedTime >= date.ToUniversalTime()
                && p.CreatedTime < toDate.ToUniversalTime();

            var lstSearch = await _reportMongoRepository.GetAllAsync(querySearch);

            _logger.LogInformation($"Sale TotalSearch: {lstSearch.Count()}");

            var slit = sales.Select(c => c.TransCode).ToList();
            var rlit = lstSearch.Where(c => c.TransCode != null).Select(c => c.TransCode).ToList();
            var listData = slit.Except(rlit);

            _logger.LogInformation($"Except: {listData.Count()}");

            foreach (var sale in listData)
            {
                Expression<Func<ReportItemDetail, bool>> query = p => p.TransCode == sale;
                var lstOne = await _reportMongoRepository.GetAllAsync(query);
                if (!lstOne.Any())
                {
                    var saleRequest = sales.FirstOrDefault(c => c.TransCode == sale);
                    if (string.IsNullOrEmpty(saleRequest.PaymentTransCode))
                        continue;

                    var paidTrans =
                        await _reportMongoRepository.GetReportBalanceHistoriesByTransCode(saleRequest
                            .PaymentTransCode);
                    //_logger.LogInformation($"{saleRequest.TransCode} paidTrans : {(paidTrans != null ? paidTrans.ToJson() : "")}");
                    var item = await ConvertInfoSale(new ReportSaleMessage()
                    {
                        CorrelationId = saleRequest.Id,
                        PerformAccount = saleRequest.StaffAccount,
                        AccountCode = saleRequest.PartnerCode,
                        CreatedDate = saleRequest.CreatedTime,
                        ProductCode = saleRequest.ProductCode,
                        Amount = saleRequest.Amount,
                        Price = saleRequest.Price,
                        Quantity = saleRequest.Quantity,
                        PaymentAmount = saleRequest.PaymentAmount,
                        Discount = saleRequest.DiscountAmount ?? 0,
                        Fee = saleRequest.Fee ?? 0,
                        ReceivedAccount = saleRequest.ReceiverInfo,
                        ServiceCode = saleRequest.ServiceCode,
                        TransRef = saleRequest.TransRef,
                        TransCode = saleRequest.TransCode,
                        Balance = Convert.ToDecimal(paidTrans?.SrcAccountBalanceAfterTrans ?? 0),
                        PaidTransCode = saleRequest.PaymentTransCode,
                        PayTransCode = saleRequest.ProviderTransCode,
                        Status = (byte)saleRequest.Status,
                        ProviderCode = saleRequest.Provider,
                        VendorCode = saleRequest.Vendor,
                        NextStep = 0,
                        ExtraInfo = saleRequest.ExtraInfo,
                        Channel = saleRequest.Channel.ToString("G"),
                        ParentProvider = saleRequest.ParentProvider,
                        ReceiverType = saleRequest.ReceiverType,
                        ProviderReceiverType = saleRequest.ReceiverTypeResponse,
                        ProviderTransCode = saleRequest.ProviderResponseCode,
                        ParentCode = saleRequest.ParentCode,
                    });

                    if (saleRequest.Status == SaleRequestStatus.Canceled
                        || saleRequest.Status == SaleRequestStatus.Failed
                        || saleRequest.Status == SaleRequestStatus.Undefined)
                        item.Status = ReportStatus.Error;
                    else if (saleRequest.Status == SaleRequestStatus.Success)
                        item.Status = ReportStatus.Success;
                    else item.Status = ReportStatus.TimeOut;

                    await _reportMongoRepository.AddOneAsync(item);
                }
            }
        }

        #endregion

        #region II.Bù thông tin giao dịch hoàn tiền bị thiếu

        private async Task<bool> CompensationTranRefundSouce(string inputPaidTranCodes, DateTime? date)
        {
            try
            {
                Expression<Func<ReportItemDetail, bool>> query = p => p.TransType == ReportServiceCode.REFUND;
                if (!string.IsNullOrEmpty(inputPaidTranCodes))
                {
                    List<string> paidTranCodes = inputPaidTranCodes.Split('|', ';', ',').ToList();
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                       paidTranCodes.Contains(p.PaidTransCode);
                    query = query.And(newQuery);
                }

                if (date != null)
                {
                    var fDate = date.Value.Date;
                    var tDate = date.Value.Date.AddDays(1);
                    Expression<Func<ReportItemDetail, bool>> newQuery = p =>
                    p.CreatedTime >= fDate.ToUniversalTime() && p.CreatedTime < tDate.ToUniversalTime();
                    query = query.And(newQuery);
                }

                var lstSearch = await _reportMongoRepository.GetAllAsync(query);
                foreach (var item in lstSearch)
                {
                    var transCode = item.TransNote.Split(':')[1].Trim(' ').TrimEnd(' ').TrimStart(' ');
                    Expression<Func<ReportItemDetail, bool>> query1 = p => p.TransCode == transCode;
                    Expression<Func<ReportItemDetail, bool>> query2 = p => p.PaidTransCode == transCode;
                    var itemSouce = await _reportMongoRepository.GetOneAsync(query1)
                        ?? await _reportMongoRepository.GetOneAsync(query2);

                    if (itemSouce != null)
                    {
                        item.TransTransSouce = itemSouce.TransCode;
                        //  item.PaidTransSouce = itemSouce.PaidTransCode;
                        item.RequestTransSouce = itemSouce.RequestRef;
                        item.ProvidersCode = itemSouce.ProvidersCode;
                        item.ProvidersInfo = itemSouce.ProvidersInfo;
                        item.ReceivedAccount = itemSouce.ReceivedAccount;
                        item.ServiceCode = itemSouce.ServiceCode;
                        item.ServiceName = itemSouce.ServiceName;
                        item.ProductCode = itemSouce.ProductCode;
                        item.ProductName = itemSouce.ProductName;
                        item.CategoryCode = itemSouce.CategoryCode;
                        item.CategoryName = itemSouce.CategoryName;
                        item.VenderCode = itemSouce.VenderCode;
                        item.VenderName = itemSouce.VenderName;
                        await _reportMongoRepository.UpdateOneAsync(item);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"{inputPaidTranCodes} CompensationTranRefundSouce Exception: {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
            }

            return false;
        }

        public async Task SaveCompensationSouceRefund(ReportItemDetail item, ReportItemDetail itemRef)
        {
            item = await ConvertInfoSouceRefund(item, itemRef);
            await _reportMongoRepository.UpdateOneAsync(item);
        }


        #endregion

        #region III.Bù lệch trạng thái, nhà cung cấp

        private async Task CompensationTranProviderOrStatus(string inputTrans, DateTime? date)
        {
            if (!string.IsNullOrEmpty(inputTrans))
            {
                var tranCodes = inputTrans.Split('|', ';', ',').ToList();
                await CompensationTranProviderOrStatus_Item(tranCodes);
            }
            else if (date != null)
                await CompensationTranProviderOrStatus_Date(date.Value);
        }
        private async Task CompensationTranProviderOrStatus_Item(List<string> tranCodes)
        {
            foreach (var tranCode in tranCodes)
                await CompensationTranProviderOrStatus_Single(tranCode, null);
        }

        public async Task<bool> CompensationTranProviderOrStatus_Single(string tranCode, ReportItemDetail item)
        {
            try
            {
                Expression<Func<ReportItemDetail, bool>> query = p => p.TransCode == tranCode;
                var firstItem = item ?? await _reportMongoRepository.GetOneAsync(query);
                if (firstItem != null)
                {
                    var saleRequest = await getSaleTopupSingleByTransCode(firstItem.TransCode);
                    bool isUpate = false;
                    if (saleRequest.Provider != firstItem.ProvidersCode)
                    {
                        isUpate = true;
                        var provider = await GetProviderBackend(saleRequest.Provider);
                        firstItem.ProvidersCode = saleRequest.Provider;
                        firstItem.ProvidersInfo = provider.ProviderName;
                        firstItem.PayTransRef = saleRequest.ProviderTransCode;
                    }

                    if (saleRequest.Status == SaleRequestStatus.Success && firstItem.Status != ReportStatus.Success)
                    {
                        isUpate = true;
                        firstItem.Status = ReportStatus.Success;
                    }
                    else if ((saleRequest.Status == SaleRequestStatus.Failed
                        || saleRequest.Status == SaleRequestStatus.Canceled) && firstItem.Status != ReportStatus.Error)
                    {
                        isUpate = true;
                        firstItem.Status = ReportStatus.Error;
                    }

                    if (isUpate)
                        await _reportMongoRepository.UpdateOneAsync(firstItem);

                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"{tranCode} CompensationTranProviderOrStatus_Single Exception: {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
            }

            return false;
        }

        private async Task CompensationTranProviderOrStatus_Date(DateTime date)
        {
            DateTime toDate = date.AddDays(1);
            string key = DateTime.Now.ToString("yyyyMMddHHmmss");
            var sales = await getSaleTopupDataTime(key, new TimeDto()
            {
                StartTime = date,
                EndTime = toDate
            });

            Expression<Func<ReportItemDetail, bool>> query = p => p.TransType != ReportServiceCode.REFUND
                                                                  && p.CreatedTime >=
                                                                  date.ToUniversalTime()
                                                                  && p.CreatedTime < toDate.ToUniversalTime();

            var lstSearch = await _reportMongoRepository.GetAllAsync(query);

            foreach (var item in lstSearch)
            {
                var f = sales.FirstOrDefault(c => c.TransCode == item.TransCode);
                if (f != null)
                {
                    var isUpate = false;
                    if (f.Provider != item.ProvidersCode)
                    {
                        isUpate = true;
                        var provider = await GetProviderBackend(f.Provider);
                        item.ProvidersCode = f.Provider;
                        item.ProvidersInfo = provider.ProviderName;
                        item.PayTransRef = f.ProviderTransCode;
                    }

                    if (f.Status == SaleRequestStatus.Success && item.Status != ReportStatus.Success)
                    {
                        isUpate = true;
                        item.Status = ReportStatus.Success;
                    }
                    else if (f.Status == SaleRequestStatus.Failed || f.Status == SaleRequestStatus.Canceled &&
                        item.Status != ReportStatus.Error)
                    {
                        isUpate = true;
                        item.Status = ReportStatus.Error;
                    }

                    if (isUpate)
                        await _reportMongoRepository.UpdateOneAsync(item);
                }
            }

        }


        #endregion

        #region IV.Bù thiếu lịch sử giao dịch

        private async Task CompensationTransHistory(DateTime fromDate, DateTime toDate)
        {
            var listHistorys = await GetBalanceHistories(fromDate);
            Expression<Func<ReportBalanceHistories, bool>> querySearch = p =>
                p.CreatedDate >= fromDate.ToUniversalTime()
                && p.CreatedDate < toDate.ToUniversalTime();

            var lstSearch = await _reportMongoRepository.GetAllAsync(querySearch);
            var slit = listHistorys.Select(c => c.TransRef).ToList();
            var rlit = lstSearch.Select(c => c.TransCode).ToList();
            var listData = slit.Except(rlit);

            foreach (var transCode in listData)
            {
                try
                {
                    Expression<Func<ReportBalanceHistories, bool>> query = p => p.TransCode == transCode;
                    var lstOne = await _reportMongoRepository.GetOneAsync(query);
                    if (lstOne == null)
                    {
                        var history = listHistorys.FirstOrDefault(c => c.TransCode == transCode);
                        var reportHistory = GetBalanceHistoryInsert(history);
                        reportHistory.Id = Guid.NewGuid();
                        await _reportMongoRepository.AddOneAsync(reportHistory);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"{transCode}|CompensationTransHistory.Exception => {ex.Message}|{ex.StackTrace}|{ex.InnerException}");
                }
            }
        }

        #endregion

        #region V.Bù thiếu dữ liệu ReportItem từ BalanceHistory

        private async Task CompensationTranHistoryToItem(DateTime date)
        {

            DateTime toDate = date.AddDays(1);
            Expression<Func<ReportBalanceHistories, bool>> querySearch = p =>
                p.CreatedDate >= date.ToUniversalTime()
                && p.CreatedDate < toDate.ToUniversalTime()
                && p.ServiceCode == ReportServiceCode.REFUND;

            Expression<Func<ReportItemDetail, bool>> queryItem = p =>
              p.CreatedTime >= date.ToUniversalTime()
              && p.CreatedTime < toDate.ToUniversalTime()
              && p.ServiceCode == ReportServiceCode.REFUND;

            var lstSearch = await _reportMongoRepository.GetAllAsync(querySearch);
            var lstItem = await _reportMongoRepository.GetAllAsync(queryItem);

            var slit = lstSearch.Select(c => c.TransCode).ToList();
            var rlit = lstItem.Select(c => c.PaidTransCode).ToList();
            var listData = slit.Except(rlit);

            foreach (var transCode in listData)
            {
                try
                {
                    Expression<Func<ReportItemDetail, bool>> query = p => p.PaidTransCode == transCode;
                    var lstOne = await _reportMongoRepository.GetOneAsync(query);
                    if (lstOne == null)
                    {
                        var history = lstSearch.FirstOrDefault(c => c.TransCode == transCode);
                        var reportHistory = await ReportItemHistoryBuMessage(history);
                        await _reportMongoRepository.AddOneAsync(reportHistory);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"{transCode}|CompensationTransHistoryToItem.Exception => {ex.Message}|{ex.StackTrace}|{ex.InnerException}");
                }
            }
        }

        public async Task SaveCompensationReportItem(ReportBalanceHistories history)
        {
            Expression<Func<ReportItemDetail, bool>> query = p => p.PaidTransCode == history.TransCode;
            var itemReport = await _reportMongoRepository.GetOneAsync<ReportItemDetail>(query);
            if (itemReport == null)
            {
                if (history.TransType == TransactionType.Deposit
                || history.TransType == TransactionType.PayBatch
                || history.TransType == TransactionType.PayCommission
                || history.TransType == TransactionType.AdjustmentIncrease
                || history.TransType == TransactionType.AdjustmentDecrease
                || history.TransType == TransactionType.CancelPayment)
                {
                    itemReport = await ConvertInfoDeposit(new ReportDepositMessage()
                    {
                        AccountCode = history.TransType == TransactionType.AdjustmentDecrease
                        ? history.SrcAccountCode : history.DesAccountCode,
                        Balance = history.TransType == TransactionType.AdjustmentDecrease
                        ? history.SrcAccountBalanceAfterTrans : history.DesAccountBalanceAfterTrans,
                        CreatedDate = history.CreatedDate,
                        ServiceCode = history.TransType == TransactionType.Deposit
                        ? ReportServiceCode.DEPOSIT
                        : history.TransType == TransactionType.PayBatch
                         ? ReportServiceCode.PAYBATCH
                        : history.TransType == TransactionType.AdjustmentIncrease
                        ? ReportServiceCode.CORRECTUP
                        : history.TransType == TransactionType.AdjustmentDecrease
                            ? ReportServiceCode.CORRECTDOWN
                            : history.TransType == TransactionType.CancelPayment
                             ? ReportServiceCode.REFUND
                            : history.TransType == TransactionType.PayCommission
                            ? ReportServiceCode.PAYCOMMISSION : "",
                        Amount = history.Amount,
                        TransRef = history.TransRef,
                        TransCode = history.TransCode,
                        Price = history.Amount,
                        SaleProcess = history.Description,
                        TransNote = history.TransNote,
                        Description = history.Description,
                        ExtraInfo = string.Empty,
                    }, history.TransType);

                    if (history.TransType == TransactionType.CancelPayment)
                    {
                        var transOld = await _reportMongoRepository.GetReportItemByTransCode(history.TransRef);
                        if (transOld != null)
                        {
                            transOld.Status = ReportStatus.Error;
                            await _reportMongoRepository.UpdateOneAsync(transOld);
                        }
                    }
                }
                else if (history.TransType == TransactionType.Transfer)
                {
                    itemReport = await ConvertInfoTransfer(new ReportTransferMessage()
                    {
                        AccountCode = history.SrcAccountCode,
                        Balance = history.SrcAccountBalanceAfterTrans,
                        ReceivedAccountCode = history.DesAccountCode,
                        ReceivedBalance = history.DesAccountBalanceAfterTrans,
                        CreatedDate = history.CreatedDate,
                        ServiceCode = ReportServiceCode.TRANSFER,
                        Amount = history.Amount,
                        TransRef = history.TransRef,
                        TransCode = history.TransCode,
                        Price = history.Amount,
                        TransNote = history.TransNote,
                        Description = history.Description,
                    });
                }

                if (itemReport != null)
                    await _reportMongoRepository.AddOneAsync(itemReport);

            }
        }

        public async Task SaveReportItemInfoSale(SaleRequestDto saleRequest)
        {
            Expression<Func<ReportItemDetail, bool>> query = p => p.TransCode == saleRequest.TransCode;
            var lstOne = await _reportMongoRepository.GetOneAsync(query);
            if (lstOne == null)
            {
                if (string.IsNullOrEmpty(saleRequest.PaymentTransCode))
                    return;

                var paidTrans = await _reportMongoRepository.GetReportBalanceHistoriesByTransCode(saleRequest.PaymentTransCode);
                //_logger.LogInformation($"{saleRequest.TransCode} paidTrans : {(paidTrans != null ? paidTrans.ToJson() : "")}");
                var item = await ConvertInfoSale(new ReportSaleMessage()
                {
                    CorrelationId = saleRequest.Id,
                    PerformAccount = saleRequest.StaffAccount,
                    AccountCode = saleRequest.PartnerCode,
                    CreatedDate = saleRequest.CreatedTime,
                    ProductCode = saleRequest.ProductCode,
                    Amount = saleRequest.Amount,
                    Price = saleRequest.Price,
                    Quantity = saleRequest.Quantity,
                    PaymentAmount = saleRequest.PaymentAmount,
                    Discount = saleRequest.DiscountAmount ?? 0,
                    Fee = saleRequest.Fee ?? 0,
                    ReceivedAccount = saleRequest.ReceiverInfo,
                    ServiceCode = saleRequest.ServiceCode,
                    TransRef = saleRequest.TransRef,
                    TransCode = saleRequest.TransCode,
                    Balance = Convert.ToDecimal(paidTrans?.SrcAccountBalanceAfterTrans ?? 0),
                    PaidTransCode = saleRequest.PaymentTransCode,
                    PayTransCode = saleRequest.ProviderTransCode,
                    Status = (byte)saleRequest.Status,
                    ProviderCode = saleRequest.Provider,
                    VendorCode = saleRequest.Vendor,
                    NextStep = 0,
                    ExtraInfo = saleRequest.ExtraInfo,
                    Channel = saleRequest.Channel.ToString("G"),
                });

                if (saleRequest.Status == SaleRequestStatus.Canceled
                    || saleRequest.Status == SaleRequestStatus.Failed
                    || saleRequest.Status == SaleRequestStatus.Undefined)
                    item.Status = ReportStatus.Error;
                else if (saleRequest.Status == SaleRequestStatus.Success)
                    item.Status = ReportStatus.Success;
                else item.Status = ReportStatus.TimeOut;

                await _reportMongoRepository.AddOneAsync(item);
            }
        }

        #endregion       

        /// <summary>
        /// 2.Đồng bộ số dư: {RequestRef}
        /// </summary>
        /// <param name="transCode"></param>
        /// <returns></returns>
        /// 
        private async Task<MessagePagedResponseBase> CompensationTranBalance(string transCode)
        {
            try
            {
                Expression<Func<ReportItemDetail, bool>> query = p => p.TransCode == transCode;
                var firstItem = await _reportMongoRepository.GetOneAsync(query);
                if (firstItem == null)
                {
                    var paidTrans = await _reportMongoRepository.GetReportBalanceHistoriesByTransCode(firstItem.PaidTransCode);
                    //_logger.LogInformation($"{transCode} paidTrans : {(paidTrans != null ? paidTrans.ToJson() : "")}");
                    if (paidTrans != null)
                    {
                        firstItem.Balance = paidTrans.SrcAccountBalanceAfterTrans;
                        firstItem.PaidAmount = -Convert.ToDouble(paidTrans.Amount);
                        await _reportMongoRepository.UpdateOneAsync(firstItem);

                        return new MessagePagedResponseBase()
                        {
                            ResponseCode = "01",
                            ResponseMessage = $"Dong bo so du thanh cong: {transCode}.",
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"CompensationTranBalance  Error: {ex.Message}|{ex.StackTrace}|{ex.InnerException}");
            }

            return new MessagePagedResponseBase()
            {
                ResponseCode = "00",
                ResponseMessage = $"Dong bo so du that bai: {transCode}.",
            };
        }

        public async Task CompensationTransError(DateTime fDate, DateTime tDate)
        {
            try
            {
                Expression<Func<ReportItemWarning, bool>> query = p => p.Status == 0
                && p.CreatedDate >= fDate.ToUniversalTime()
                && p.CreatedDate <= tDate.ToUniversalTime();
                var list = await _reportMongoRepository.GetAllAsync(query);
                _logger.LogInformation($"CompensationTransError Count : {list.Count()}");
                foreach (var item in list)
                {
                    item.Status = 2;
                    var isUpdate = await _reportMongoRepository.UpdateWaringInfo(item);
                    if (isUpdate)
                    {
                        if (item.Type == ReportWarningType.Type_Status)
                        {
                            if (await CompensationTranProviderOrStatus_Single(item.TransCode, null))
                                item.Status = 1;
                        }
                        else if (item.Type == ReportWarningType.Type_SouceTrans)
                        {
                            if (await CompensationTranRefundSouce(item.TransCode, DateTime.Now.Date))
                                item.Status = 1;
                        }
                        else if (item.Type == ReportWarningType.Type_ErrorInsert)
                        {
                            //if (await CompensationTranRefundSouce(item.TransCode, DateTime.Now.Date))
                            //    item.Status = 1;
                        }
                        else if (item.Type == ReportWarningType.Type_ErrorConvertInfo)
                        {
                            //if (item.TransType == ReportServiceCode.TOPUP)
                            //{
                            //    var message = item.Message.FromJson<ReportSaleMessage>();
                            //    await ReportSaleIntMessage(message);
                            //    item.Status = 1;
                            //}
                        }

                        if (item.Retry >= 5)
                            item.Status = 1;
                        else
                        {
                            if (item.Status == 2)
                            {
                                item.Status = 0;
                                item.Retry = +1;
                            }
                        }
                        await _reportMongoRepository.UpdateWaringInfo(item);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"CompensationTransError  Error: {ex.Message}|{ex.StackTrace}|{ex.InnerException}");
            }
        }

        private async Task SysAccountBalance(string accountCode, string currencyCode, DateTime fromDate, DateTime curDate, DateTime toDate)
        {
            try
            {
                var txtDay = accountCode + "_" + curDate.ToString("yyyyMMdd");
                Expression<Func<ReportAccountBalanceDay, bool>> querySingle = p => p.CurrencyCode == currencyCode &&
                                                                             p.AccountType == "CUSTOMER" &&
                                                                             p.TextDay == txtDay &&
                                                                             p.AccountCode == accountCode;

                var singleAccount = await _reportMongoRepository.GetOneAsync<ReportAccountBalanceDay>(querySingle);
                if (singleAccount != null)
                    return;

                Expression<Func<ReportAccountBalanceDay, bool>> queryFirst = p => p.CurrencyCode == currencyCode &&
                                                                            p.AccountType == "CUSTOMER" &&
                                                                            p.CreatedDay < toDate.ToUniversalTime() &&
                                                                            p.CreatedDay >= fromDate.ToUniversalTime() &&
                                                                            p.AccountCode == accountCode;

                var lstAccountDay = await _reportMongoRepository.GetAllAsync<ReportAccountBalanceDay>(queryFirst);
                if (lstAccountDay.Count > 0)
                {
                    singleAccount = lstAccountDay.OrderByDescending(c => c.CreatedDay).FirstOrDefault();
                    if (singleAccount.TextDay != txtDay)
                    {
                        var itemNew = new ReportAccountBalanceDay()
                        {
                            AccountCode = singleAccount.AccountCode,
                            AccountInfo = singleAccount.AccountInfo,
                            AccountType = BalanceAccountTypeConst.CUSTOMER,
                            Credite = 0,
                            Debit = 0,
                            BalanceBefore = singleAccount.BalanceAfter,
                            BalanceAfter = singleAccount.BalanceAfter,
                            CreatedDay = curDate.Date,
                            CurrencyCode = currencyCode,
                            TextDay = txtDay,
                            DecPayment = 0,
                            DecOther = 0,
                            IncDeposit = 0,
                            IncOther = 0,
                            SaleCode = singleAccount.SaleCode,
                            SaleLeaderCode = singleAccount.SaleLeaderCode
                        };

                        await _reportMongoRepository.AddOneAsync(itemNew);
                    }
                }
                else
                {
                    var account = await GetAccountBackend(accountCode);
                    var itemNew = new ReportAccountBalanceDay()
                    {
                        AccountCode = account.AccountCode,
                        AccountInfo = "",
                        AccountType = BalanceAccountTypeConst.CUSTOMER,
                        Credite = 0,
                        Debit = 0,
                        BalanceBefore = 0,
                        BalanceAfter = 0,
                        CreatedDay = curDate.Date,
                        CurrencyCode = currencyCode,
                        TextDay = txtDay,
                        DecPayment = 0,
                        DecOther = 0,
                        IncDeposit = 0,
                        IncOther = 0,
                        SaleCode = account.SaleCode,
                        SaleLeaderCode = account.LeaderCode,

                    };

                    MappingSaleLeader(ref itemNew);
                    await _reportMongoRepository.AddOneAsync(itemNew);
                }
            }
            catch (Exception e)
            {
                _logger.LogError($"{accountCode} SysAccountBalance error: {e}");
            }
        }

        public async Task SysAccountBalanceAfter(string accountCode, string currencyCode, string txtDay, ReportTransactionDetailDto dto)
        {
            try
            {

                Expression<Func<ReportAccountBalanceDay, bool>> querySingle = p => p.CurrencyCode == currencyCode &&
                                                                             p.AccountType == "CUSTOMER" &&
                                                                             p.TextDay == txtDay &&
                                                                             p.AccountCode == accountCode;

                var singleAccount = await _reportMongoRepository.GetOneAsync<ReportAccountBalanceDay>(querySingle);
                if (singleAccount == null)
                    return;

                if (singleAccount.BalanceBefore != dto.BalanceBefore)
                    singleAccount.BalanceBefore = dto.BalanceBefore;

                if (singleAccount.BalanceAfter != dto.BalanceAfter)
                    singleAccount.BalanceAfter = dto.BalanceAfter;

                if (singleAccount.Debit != dto.Decrement)
                    singleAccount.Debit = dto.Decrement;

                if (singleAccount.Credite != dto.Increment)
                    singleAccount.Credite = dto.Increment;

                if (singleAccount.DecPayment != dto.Decrement)
                    singleAccount.DecPayment = dto.Decrement;               

                await _reportMongoRepository.UpdateOneAsync(singleAccount);

                _logger.LogInformation($"accountCode= {accountCode}|currencyCode= {currencyCode}|TxtDay= {txtDay}|SysAccountBalanceAfter_Done");
            }
            catch (Exception e)
            {
                _logger.LogError($"accountCode= {accountCode}|currencyCode= {currencyCode}|TxtDay= {txtDay}|SysAccountBalanceAfter error: {e}");
            }
        }

        private async Task<MessagePagedResponseBase> CheckAccountBalance(DateTime date)
        {
            //.Check Sum dữ liệu giữa báo cáo chi tiết vào báo cáo cân đối tài khoản theo các dòng tiền
            //*Nạp tiền
            //*Chuyển tiền
            //*Điều chỉnh
            //*Hoàn tiền
            //*Thưởng tiền
            //*Bán hàng
            DateTime toDate = date.Date.AddDays(1);
            //=>Dữ liệu chi tiết
            Expression<Func<ReportItemDetail, bool>> querySearch = p =>
               p.CreatedTime >= date.Date.ToUniversalTime()
               && p.CreatedTime < toDate.ToUniversalTime()
               && !string.IsNullOrEmpty(p.PaidTransCode);

            var lstSearch = await _reportMongoRepository.GetAllAsync(querySearch);

            //=>Dữ liệu tổng hợp
            Expression<Func<ReportAccountBalanceDay, bool>> queryBalanceSearch = p =>
               p.CreatedDay >= date.Date.ToUniversalTime()
               && p.CreatedDay < toDate.ToUniversalTime()
               && p.CurrencyCode == "VND";
            var lstBalanceSearch = await _reportMongoRepository.GetAllAsync(queryBalanceSearch);

            //=>Group dữ liệu báo cáo chi tiết
            var listGpInAmount = (from x in lstSearch
                                  group x by x.AccountCode into g
                                  select new ReportAccountBalanceDay
                                  {
                                      AccountCode = g.Key,
                                      IncDeposit = g.Sum(c => c.ServiceCode == ReportServiceCode.DEPOSIT ? Math.Abs(c.PaidAmount ?? 0) : 0),
                                      IncOther = g.Sum(c => (c.ServiceCode == ReportServiceCode.TRANSFER
                                      || c.ServiceCode == ReportServiceCode.CORRECTUP
                                      || c.ServiceCode == ReportServiceCode.PAYBATCH
                                      || c.ServiceCode == ReportServiceCode.REFUND) ? Math.Abs(c.PaidAmount ?? 0) : 0),
                                  }).ToList();


            var listGpOutSale = (from x in lstSearch
                                 where (x.ServiceCode == ReportServiceCode.TOPUP
                                 || x.ServiceCode == ReportServiceCode.TOPUP_DATA
                                 || x.ServiceCode == ReportServiceCode.PAY_BILL
                                 || x.ServiceCode == ReportServiceCode.PIN_CODE
                                 || x.ServiceCode == ReportServiceCode.PIN_DATA
                                 || x.ServiceCode == ReportServiceCode.PIN_GAME
                                 ) && x.TransType != ReportServiceCode.REFUND
                                 group x by x.AccountCode into g
                                 select new ReportAccountBalanceDay
                                 {
                                     AccountCode = g.Key,
                                     DecPayment = g.Sum(c => Math.Abs(c.PaidAmount ?? 0))
                                 }).ToList();

            var listGpOutTransfer = (from x in lstSearch
                                     where x.ServiceCode == ReportServiceCode.TRANSFER
                                     group x by x.PerformAccount into g
                                     select new ReportAccountBalanceDay
                                     {
                                         AccountCode = g.Key,
                                         DecOther = g.Sum(c => Math.Abs(c.PaidAmount ?? 0)),
                                     }).ToList();

            var listGpOutCorrect = (from x in lstSearch
                                    where x.ServiceCode == ReportServiceCode.CORRECTDOWN
                                    group x by x.AccountCode into g
                                    select new ReportAccountBalanceDay
                                    {
                                        AccountCode = g.Key,
                                        DecOther = g.Sum(c => Math.Abs(c.PaidAmount ?? 0))
                                    }).ToList();


            var listGpMapping = (from b in lstBalanceSearch
                                 join i in listGpInAmount on b.AccountCode equals i.AccountCode into gi
                                 from input in gi.DefaultIfEmpty()
                                 join s in listGpOutSale on b.AccountCode equals s.AccountCode into gs
                                 from sale in gs.DefaultIfEmpty()
                                 join t in listGpOutTransfer on b.AccountCode equals t.AccountCode into gt
                                 from transfer in gt.DefaultIfEmpty()
                                 join c in listGpOutCorrect on b.AccountCode equals c.AccountCode into gc
                                 from correct in gc.DefaultIfEmpty()
                                 select new
                                 {
                                     AccountCode = b.AccountCode,
                                     BalanceBefore = b.BalanceBefore,
                                     BalanceAfter = b.BalanceAfter,
                                     IncDeposit = b.IncDeposit ?? 0,
                                     //IncCorrect = b.IncCorrect ?? 0,
                                     //IncPayBatch = b.IncPayBatch ?? 0,
                                     //IncRefund = b.IncRefund ?? 0,
                                     //IncTransfer = b.IncTransfer ?? 0,
                                     //DecCorrect = b.DecCorrect ?? 0,
                                     //DecPayment = b.DecPayment ?? 0,
                                     //DecTransfer = b.DecTransfer ?? 0,
                                     //IncDepositSale = input != null ? input.IncDeposit ?? 0 : 0,
                                     //IncCorrectSale = input != null ? input.IncCorrect ?? 0 : 0,
                                     //IncPayBatchSale = input != null ? input.IncPayBatch ?? 0 : 0,
                                     //IncRefundSale = input != null ? input.IncRefund ?? 0 : 0,
                                     //IncTransferSale = input != null ? input.IncTransfer ?? 0 : 0,
                                     //DecCorrectSale = correct != null ? correct.DecCorrect ?? 0 : 0,
                                     //DecPaymentSale = sale != null ? sale.DecPayment ?? 0 : 0,
                                     //DecTransferSale = transfer != null ? transfer.DecTransfer ?? 0 : 0,
                                 }).ToList();


            //var checkList = (from x in listGpMapping
            //                 where x.IncDeposit != x.IncDepositSale
            //                 || x.IncCorrect != x.IncCorrectSale
            //                 || x.IncPayBatch != x.IncPayBatchSale
            //                 || x.IncRefund != x.IncRefundSale
            //                 || x.IncTransfer != x.IncTransferSale
            //                 || x.DecCorrect != x.DecCorrectSale
            //                 || x.DecPayment != x.DecPaymentSale
            //                 || x.DecTransfer != x.DecTransferSale
            //                 select x).Select(c => c.AccountCode).ToJson();


            //var checkList2 = (from x in listGpMapping
            //                  where x.BalanceBefore + x.IncDeposit + x.IncPayBatch + x.IncRefund + x.IncTransfer + x.IncCorrect
            //                  - x.DecCorrect - x.DecPayment - x.DecTransfer - x.BalanceAfter != 0
            //                  select x).Select(c => c.AccountCode).ToJson();

            return new MessagePagedResponseBase()
            {
                ResponseCode = "01",
                //ExtraInfo = checkList,
                //ResponseMessage = checkList2,
            };
        }

        public async Task<MessagePagedResponseBase> DeleteFileFpt(DateTime date)
        {

            DateTime toDate = date.Date.AddDays(1);
            Expression<Func<ReportFileFpt, bool>> querySearch = p =>
               p.TextDay == date.ToString("yyyyMMdd");

            var lst = await _reportMongoRepository.GetAllAsync(querySearch);
            foreach (var item in lst)
            {
                _fileUploadsv.DeleteFileOnFtpServer(item.FileName, true);
                await _reportMongoRepository.DeleteOneAsync(item);
            }

            return new MessagePagedResponseBase()
            {
                ResponseCode = "01",

            };
        }

        public async Task<MessagePagedResponseBase> SyncInfoOnjectRequest(SyncInfoObjectRequest request)
        {
            try
            {
                //1.Bù giao dịch bán hàng
                if (request.Type == 1)
                    await CompensationTranSale(request.TransCode, request.Date);
                //2.Bù thông tin giao dịch hoàn tiền bị thiếu
                else if (request.Type == 2)
                    await CompensationTranRefundSouce(request.TransCode, request.Date);
                //3.Bù lệch trạng thái, nhà cung cấp
                else if (request.Type == 3)
                    await CompensationTranProviderOrStatus(request.TransCode, request.Date);
                //4.Bù thiếu lịch sử giao dịch
                else if (request.Type == 4)
                    await CompensationTransHistory(request.Date.Value, request.ToDate.Value);
                //5.Bù thiếu dữ liệu ReportItem từ BalanceHistory
                else if (request.Type == 5)
                    await CompensationTranHistoryToItem(request.Date ?? DateTime.Now.Date);
                else if (request.Type == 6)
                    return await CheckAccountBalance(request.Date.Value);
                else if (request.Type == 8)
                    await SysPriceInOut_Date(request.Date.Value, request.ToDate.Value);
                else if (request.Type == 9)
                    await SysDayOneProcess(request.Date.Value);
                else if (request.Type == 77)
                    return await DeleteFileFpt(request.Date.Value);
                else if (request.Type == 88)
                {
                    var soucePath = await GetForderCreate(ReportConst.Agent);
                    await ExportFileDataAgent(request.Date.Value, null, null, soucePath);
                    await ZipForderCreate(soucePath);
                }
                else if (request.Type == 89)
                {
                    var listHistorys = await GetBalanceHistories(request.Date.Value);

                    try
                    {
                        string fileName = "balance_" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".csv";
                        using (StreamWriter file = new StreamWriter(fileName))
                        {
                            int count = 1;
                            foreach (var item in listHistorys)
                            {
                                var tmp = $"{count},{item.SrcAccountCode},{item.DesAccountCode},{item.Amount},{item.TransRef},{item.TransactionType},{item.Status}";
                                file.WriteLine(tmp);
                                count++;
                            }
                            file.Close();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Ghi Danh sach GetBalanceHistories Exception {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
                    }
                }
                ////99.Hoàn tiền danh sách cho khách hàng
                //else if (request.Type == 99)
                //{
                //    var list = request.TransCode.Split(';', ',', '|');
                //    foreach (var item in list)
                //    {
                //        var sale = await getSaleTopupSingleByTransCode(item);
                //        if (sale != null)
                //        {
                //            var reponseStatus = await _grpcClient.GetClientCluster(GrpcServiceName.Backend).SendAsync(new TopupUpdateStatusRequest()
                //            {
                //                TransCode = sale.TransCode,
                //                Status = SaleRequestStatus.WaitForResult,
                //            });

                //            if (reponseStatus.ResponseCode == "01")
                //            {
                //                await _bus.Publish<TransactionRefundCommand>(new
                //                {
                //                    TransCode = sale.TransCode,
                //                });
                //            }
                //        }
                //    }
                //}

                return new MessagePagedResponseBase
                {
                    ResponseCode = "01",
                    ResponseMessage = "Thành công",
                };
            }
            catch (Exception e)
            {
                _logger.LogError($"SyncInfoOnjectRequest error: {e}");
                return new MessagePagedResponseBase
                {
                    ResponseCode = "00",
                    ResponseMessage = $"{e.Message}|{e.StackTrace}|{e.InnerException}"
                };
            }
        }

        private async Task SysPriceInOut_Date(DateTime fromDate, DateTime toDate)
        {
            try
            {
                Expression<Func<ReportItemDetail, bool>> querySearch = p =>
                    p.CreatedTime >= fromDate.ToUniversalTime() && p.CreatedTime < toDate.AddDays(1).ToUniversalTime();

                var listData = await _reportMongoRepository.GetAllAsync(querySearch);
                _logger.LogInformation($"Report Item: {listData.Count()}");

                foreach (var sale in listData)
                {
                    if (sale.TransType == ReportServiceCode.TOPUP
                        || sale.TransType == ReportServiceCode.TOPUP_DATA
                        || sale.TransType == ReportServiceCode.PIN_CODE
                        || sale.TransType == ReportServiceCode.PIN_DATA
                        || sale.TransType == ReportServiceCode.PIN_GAME)
                    {
                        sale.PriceIn = 0;
                        sale.PriceOut = Math.Abs(sale.TotalPrice);
                    }
                    else
                    {
                        sale.PriceIn = Math.Abs(sale.TotalPrice);
                        sale.PriceOut = 0;
                    }

                    await _reportMongoRepository.UpdateOneAsync(sale);
                }
            }
            catch (Exception ex)
            {

            }
        }

        private async Task<List<SaleRequestDto>> getSaleTopupDataTimeFill(DateTime date)
        {
            string key = DateTime.Now.ToString("yyyyMMddHHmmss");
            DateTime begin = DateTime.Now;
            try
            {
                List<SaleRequestDto> saleRequest = new List<SaleRequestDto>();
                var arrayDates = getArrayTimeDate(date, 3);
                Parallel.ForEach(arrayDates, time =>
                {
                    var item = getSaleTopupDataTime(key, time).Result;
                    saleRequest.AddRange(item);
                });

                var totalSeconds = DateTime.Now.Subtract(begin).TotalSeconds;
                _logger.LogError($"key= {key}|getSaleTopupDataTimeFill|Seconds= {totalSeconds}");
                return saleRequest;
            }
            catch (Exception ex)
            {
                var totalSeconds = DateTime.Now.Subtract(begin).TotalSeconds;
                _logger.LogError($"key= {key}|getSaleTopupDataTimeFill|Seconds= {totalSeconds}|Exception= {ex.Message}|{ex.StackTrace}|{ex.InnerException}");
                return new List<SaleRequestDto>();
            }
        }
        private async Task<List<SaleRequestDto>> getSaleTopupDataTime(string key, TimeDto dto)
        {
            var begin = DateTime.Now;
            int retry = 0;
            while (retry <= 3)
            {
                try
                {
                    var client = new JsonServiceClient(_apiUrl)
                    {
                        Timeout = TimeSpan.FromMinutes(20)
                    };
                    _logger.LogInformation($"key= {key}|getSaleTopupDataTime|retry= {retry}|StartTime= {dto.StartTime.ToString("yyyy-MM-dd HH:mm:ss")} - {dto.EndTime.ToString("yyyy-MM-dd HH:mm:ss")}");
                    var reponse = await client.GetAsync<ResponseMesssageObject<string>>(new GetReportSaleTopupRequest { FromDate = dto.StartTime, ToDate = dto.EndTime });
                    var totalSeconds = DateTime.Now.Subtract(begin).TotalSeconds;
                    _logger.LogInformation($"key= {key}|getSaleTopupDataTime|retry= {retry}|ResponseCode= {reponse.ResponseCode}|Total= {reponse.Total}|Seconds= {totalSeconds}");
                    return reponse.Payload.FromJson<List<SaleRequestDto>>();
                }
                catch (Exception ex)
                {
                    var totalSeconds = DateTime.Now.Subtract(begin).TotalSeconds;
                    _logger.LogError($"key= {key}|getSaleTopupDataTime|retry= {retry}|Seconds= {totalSeconds}|Exception= {ex.Message}|{ex.StackTrace}|{ex.InnerException}");
                    retry += 1;
                }
            }

            return new List<SaleRequestDto>();
        }

        private async Task<SaleRequestDto> getSaleTopupSingleByTransCode(string transCode)
        {
            var begin = DateTime.Now;
            int retry = 0;
            while (retry <= 3)
            {
                try
                {
                    var client = new JsonServiceClient(_apiUrl)
                    {
                        Timeout = TimeSpan.FromMinutes(20)
                    };
                    _logger.LogInformation($"getSaleTopupSingleByTransCode|retry= {retry}|TransCode= {transCode}");
                    var reponse = await client.GetAsync<SaleRequestDto>(new CallBackEndRequest { Filter = transCode });
                    var totalSeconds = DateTime.Now.Subtract(begin).TotalSeconds;
                    _logger.LogInformation($"getSaleTopupSingleByTransCode|retry= {retry}|Response= {reponse.ToJson()}|Seconds= {totalSeconds}");
                    return reponse;
                }
                catch (Exception ex)
                {
                    var totalSeconds = DateTime.Now.Subtract(begin).TotalSeconds;
                    _logger.LogError($"getSaleTopupSingleByTransCode|retry= {retry}|Seconds= {totalSeconds}|Exception= {ex.Message}|{ex.StackTrace}|{ex.InnerException}");
                    retry += 1;
                }
            }

            return null;
        }

        private async Task<List<BalanceHistories>> getBalanceHistoryDataTimeFull(DateTime date)
        {
            string key = DateTime.Now.ToString("yyyyMMddHHmmss");
            DateTime begin = DateTime.Now;
            try
            {
                List<BalanceHistories> historyData = new List<BalanceHistories>();
                var arrayDates = getArrayTimeDate(date, 3);
                Parallel.ForEach(arrayDates, time =>
                {
                    var item = getBalanceHistoryDataTime(key, time).Result;
                    historyData.AddRange(item);
                });

                var totalSeconds = DateTime.Now.Subtract(begin).TotalSeconds;
                _logger.LogError($"key= {key}|getBalanceHistoryDataTimeFull|Seconds= {totalSeconds}");
                return historyData;
            }
            catch (Exception ex)
            {
                var totalSeconds = DateTime.Now.Subtract(begin).TotalSeconds;
                _logger.LogError($"key= {key}|getBalanceHistoryDataTimeFull|Seconds= {totalSeconds}|Exception= {ex.Message}|{ex.StackTrace}|{ex.InnerException}");
                return new List<BalanceHistories>();
            }
        }
        private async Task<List<BalanceHistories>> getBalanceHistoryDataTime(string key, TimeDto dto)
        {
            int retry = 0;
            while (retry <= 3)
            {
                var begin = DateTime.Now;
                try
                {
                    var client = new JsonServiceClient(_apiUrl)
                    {
                        Timeout = TimeSpan.FromMinutes(20)
                    };

                    _logger.LogInformation($"key= {key}|getBalanceHistoryDataTime|retry= {retry}|StartTime= {dto.StartTime.ToString("yyyy-MM-dd HH:mm:ss")} - {dto.EndTime.ToString("yyyy-MM-dd HH:mm:ss")}");
                    var reponse = await client.GetAsync<ResponseMesssageObject<string>>(new BalanceHistoriesRequest { FromDate = dto.StartTime, ToDate = dto.EndTime });
                    var totalSeconds = DateTime.Now.Subtract(begin).TotalSeconds;
                    _logger.LogInformation($"key= {key}|getBalanceHistoryDataTime|retry= {retry}|ResponseCode= {reponse.ResponseCode}|Total= {reponse.Total}|Seconds= {totalSeconds}");
                    return reponse.Payload.FromJson<List<BalanceHistories>>();
                }
                catch (Exception ex)
                {
                    var totalSeconds = DateTime.Now.Subtract(begin).TotalSeconds;
                    _logger.LogError($"key= {key}|getBalanceHistoryDataTime|retry= {retry}|Seconds= {totalSeconds}|Exception= {ex.Message}|{ex.StackTrace}|{ex.InnerException}");
                    retry += 1;
                }
            }

            return new List<BalanceHistories>();
        }
        private List<TimeDto> getArrayTimeDate(DateTime date, int total)
        {
            var temp = date.Date;
            if (total == 0) total = 1;
            var ls = new List<TimeDto>();
            int i = 0;
            while (i < 24)
            {
                ls.Add(new TimeDto()
                {
                    StartTime = temp.AddHours(i),
                    EndTime = temp.AddHours(i + total),
                });
                i = i + total;
            }
            return ls;
        }

    }


}