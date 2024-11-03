using System.Threading.Tasks;
using ServiceStack;
using HLS.Paygate.Report.Model.Dtos.RequestDto;
using Microsoft.Extensions.Logging;
using HLS.Paygate.Report.Domain.Entities;
using HLS.Paygate.Shared;
using HLS.Paygate.Report.Model.Dtos;
using System.Linq;
using HLS.Paygate.Shared.EsIndexs;
using System;
using System.Collections.Generic;

namespace HLS.Paygate.Report.Interface.Services
{
    public partial class ReportService
    {
        public async Task<object> Get(ReportComparePartnerRequest request)
        {
            _logger.LogInformation($"ReportComparePartnerRequest request: {request.ToJson()}");
            if (string.IsNullOrEmpty(request.AgentCode))
                return new MessagePagedResponseBase()
                {
                    ResponseCode = "00",
                    ResponseMessage = "Chưa truyền mã đại lý."
                };

            var rs = _searchElastich
                ? await _elasticReportService.ReportComparePartnerGetList(request)
                : await _balanceReportService.ReportComparePartnerGetList(request);
            return rs;
        }

        /// <summary>
        /// Gửi mail báo cáo
        /// </summary>
        /// <param name="intput"></param>
        /// <returns></returns>
        public async Task<object> Post(SendMailComparePartnerRequest intput)
        {
            _logger.LogInformation($"{intput.AgentCode} SendMailComparePartnerRequest request: {intput.ToJson()}");

            if (string.IsNullOrEmpty(intput.AgentCode))
                return new MessagePagedResponseBase()
                {
                    ResponseCode = "00",
                    ResponseMessage = "Chưa truyền mã đại lý để thực hiện gửi mail."
                };

            var agent = await _exportingService.GetUserExportingPeriodAsync(intput.AgentCode);
            if (agent == null)
                agent = new UserInfoPeriodDto()
                {
                    AgentCode = "",
                    EmailReceives = "",
                    FullName = "",
                    EmailAddress = "",
                    ContractNumber = "",
                    Period = 0,
                    PhoneNumber = "",
                    UserName = "",
                    SigDate = null,
                };

            return await _exportingService.SendMailComparePartner(agent, intput.FromDate, intput.ToDate, "", 0, intput.IsAuto, email: intput.Email);
        }

        public async Task<object> Get(SyncAgentBalanceRequest request)
        {
            // _logger.LogInformation($"SyncAgentBalanceRequest request: {request.ToJson()}");
            var rs = await _balanceReportService.ReportSyncAgentBalanceRequest(request);
            //_logger.LogInformation($"SyncAgentBalanceRequest return: {rs}");
            return rs;
        }

        public async Task<object> Get(SyncExportBatchRequest request)
        {
            //_logger.LogInformation($"SyncExportBatchRequest request: {request.ToJson()}");
            var register = await _balanceReportService.GetRegisterInfo("BATCH_FILE");
            var rs = await _exportingService.ExportFile(register, request.Date);
            //_logger.LogInformation($"SyncExportBatchRequest return: {rs.ToJson()}");
            return rs;
        }

        public async Task<object> Get(SyncInfoObjectRequest request)
        {
            _logger.LogInformation($"SyncInfoObjectRequest request: {request.ToJson()}");
            if (request.Type == 0)
            {
                //1.Đồng bộ thiếu giao dịch bên Elastich báo cáo chi tiết RequsetRef
                if (request.Status == 1)
                {
                    var startTime = request.Date.Value.Date;
                    var endTime = request.Date.Value.Date;
                    if (request.ToDate != null)
                    {
                        if (request.Date.Value > request.ToDate.Value.Date)
                            return null;

                        endTime = request.ToDate.Value.Date;
                    }

                    if (!string.IsNullOrEmpty(request.TransCode))
                    {
                        foreach (var transCode in request.TransCode.Split('|', ';', ','))
                        {
                            var list = await _balanceReportService.ReportQueryItemRequest(startTime.Date, transCode);
                            var listElast = await _elasticReportService.GetTransPaidList(startTime.Date, 2);
                            var transRefCodes = list.Where(c => !string.IsNullOrEmpty(c.PaidTransCode)).Select(c => c.PaidTransCode).Distinct().ToList();
                            var exItems = transRefCodes.Except(listElast);
                            foreach (var item in exItems)
                            {
                                _logger.LogInformation($"Input SyncInfoObjectRequest request: {item}");
                                var itemRead = list.SingleOrDefault(c => c.PaidTransCode == item);
                                await _elasticReportService.AddReportItemDetail(ReportIndex.ReportItemDetailIndex, itemRead);
                            }
                        }
                    }
                    else
                    {
                        while (startTime <= endTime)
                        {
                            var list = await _balanceReportService.ReportQueryItemRequest(startTime.Date, string.Empty);
                            var listElast = await _elasticReportService.GetTransPaidList(startTime.Date, 2);
                            var transRefCodes = list.Where(c => !string.IsNullOrEmpty(c.PaidTransCode)).Select(c => c.PaidTransCode).Distinct().ToList();
                            var exItems = transRefCodes.Except(listElast);
                            foreach (var item in exItems)
                            {
                                _logger.LogInformation($"Input SyncInfoObjectRequest request: {item}");
                                var itemRead = list.SingleOrDefault(c => c.PaidTransCode == item);
                                await _elasticReportService.AddReportItemDetail(ReportIndex.ReportItemDetailIndex, itemRead);
                            }

                            startTime = startTime.AddDays(1);
                        }
                    }
                }
                //2.Đồng bộ dữ liệu sang Elastich với báo cáo lịch sử số dư
                else if (request.Status == 2)
                {
                    var startTime = request.Date.Value.Date;
                    var endTime = request.Date.Value.Date;
                    if (request.ToDate != null)
                    {
                        if (request.Date.Value > request.ToDate.Value.Date)
                            return null;
                        endTime = request.ToDate.Value.Date;
                    }

                    if (!string.IsNullOrEmpty(request.TransCode))
                    {
                        foreach (var transCode in request.TransCode.Split('|', ';', ','))
                        {
                            var list = await _balanceReportService.ReportQueryHistoryRequest(startTime.Date, transCode);
                            var listElast = await _elasticReportService.GetHistoryTempList(startTime.Date);
                            var transPaidCodes = list.Where(c => !string.IsNullOrEmpty(c.TransCode)).Select(c => c.TransCode).Distinct().ToList();
                            var exItems = transPaidCodes.Except(listElast);
                            foreach (var item in exItems)
                            {
                                _logger.LogInformation($"Input SyncTransDetailRequest request: {item}");
                                var itemRead = list.Where(c => c.TransCode == item);
                                foreach (var itemd in itemRead)
                                    await _elasticReportService.AddReportItemHistory(ReportIndex.ReportBalanceHistoriesIndex, itemd);
                            }
                        }
                    }
                    else
                    {
                        while (startTime <= endTime)
                        {
                            var list = await _balanceReportService.ReportQueryHistoryRequest(startTime.Date, string.Empty);
                            var listElast = await _elasticReportService.GetHistoryTempList(startTime.Date);
                            var transPaidCodes = list.Where(c => !string.IsNullOrEmpty(c.TransCode)).Select(c => c.TransCode).Distinct().ToList();
                            var exItems = transPaidCodes.Except(listElast);
                            foreach (var item in exItems)
                            {
                                _logger.LogInformation($"Input SyncTransDetailRequest request: {item}");
                                var itemRead = list.Where(c => c.TransCode == item);
                                foreach (var itemd in itemRead)
                                    await _elasticReportService.AddReportItemHistory(ReportIndex.ReportBalanceHistoriesIndex, itemd);
                            }

                            startTime = startTime.AddDays(1);
                        }
                    }
                }
                //3.Đồng bộ số dư báo cáo tổng hợp: TransCode:Loại tiền,ProviderCode: Tài khoản cần đồng bộ
                else if (request.Status == 3)
                {
                    var startTime = request.Date.Value.Date;
                    var endTime = request.Date.Value.Date;
                    if (request.ToDate != null)
                    {
                        if (request.Date.Value > request.ToDate.Value.Date)
                            return null;
                        endTime = request.ToDate.Value.Date;
                    }

                    while (startTime <= endTime)
                    {
                        var list = await _balanceReportService.ReportQueryAccountBalanceDayRequest(startTime.Date, request.TransCode, string.Empty);
                        var listElast = await _elasticReportService.GetAccountBalanceDayList(startTime.Date, request.TransCode, string.Empty);
                        var msl = (from x in list select x.AccountCode + "-" + x.CurrencyCode + "-" + x.TextDay).ToList();
                        var mslE = (from x in listElast select x.AccountCode + "-" + x.CurrencyCode + "-" + x.TextDay).ToList();

                        var exceptList = msl.Except(mslE).ToList();
                        foreach (var item in exceptList)
                        {
                            _logger.LogInformation($"Input DataSysnc request: {item}");
                            var x = item.Split('-');
                            var itemRead = list.SingleOrDefault(c => c.AccountCode == x[0] && c.CurrencyCode == x[1] && c.TextDay == x[2]);
                            await _elasticReportService.AddReportAccountBalanceDay(ReportIndex.ReportAccountbalanceDayIndex, itemRead);
                        }

                        startTime = startTime.AddDays(1);
                    }
                }
                //4.Đồng bộ chi tiết cấp tiền
                else if (request.Status == 4)
                {
                    var startTime = request.Date.Value.Date;
                    var endTime = request.Date.Value.Date;
                    if (request.ToDate != null)
                    {
                        if (request.Date.Value > request.ToDate.Value.Date)
                            return null;
                        endTime = request.ToDate.Value.Date;
                    }

                    while (startTime <= endTime)
                    {
                        var list = await _balanceReportService.ReportQueryStaffDetailRequest(startTime.Date, string.Empty);
                        var listElast = await _elasticReportService.GetStaffDetailList(startTime.Date, string.Empty);
                        var transCodes = list.Where(c => !string.IsNullOrEmpty(c.TransCode)).Select(c => c.TransCode).Distinct().ToList();
                        var exItems = transCodes.Except(listElast);
                        foreach (var transCode in exItems)
                        {
                            _logger.LogInformation($"Input DataSysnc request: {transCode}");
                            var itemRead = list.SingleOrDefault(c => c.TransCode == transCode);
                            await _elasticReportService.AddReportStaffDetail(ReportIndex.ReportStaffdetailsIndex, itemRead);
                        }

                        startTime = startTime.AddDays(1);
                    }
                }
                //5.Đồng bộ phần báo cáo NXT mã thẻ
                else if (request.Status == 5)
                {
                    var startTime = request.Date.Value.Date;
                    var endTime = request.Date.Value.Date;
                    if (request.ToDate != null)
                    {
                        if (request.Date.Value > request.ToDate.Value.Date)
                            return null;
                        endTime = request.ToDate.Value.Date;
                    }

                    while (startTime <= endTime)
                    {
                        var list = await _balanceReportService.ReportQueryCardStockByDateRequest(startTime.Date, string.Empty);
                        var listElast = await _elasticReportService.GetCardStockByDateList(startTime.Date);
                        var msl = (from x in list select x.StockCode + "-" + x.ProductCode + "-" + x.ShortDate).ToList();
                        var mslE = (from x in listElast select x.StockCode + "-" + x.ProductCode + "-" + x.ShortDate).ToList();

                        var exceptList = msl.Except(mslE).ToList();
                        foreach (var item in exceptList)
                        {
                            _logger.LogInformation($"Input DataSysnc request: {item}");
                            var x = item.Split('-');
                            var itemRead = list.SingleOrDefault(c => c.StockCode == x[0] && c.ProductCode == x[1] && c.ShortDate == x[2]);
                            await _elasticReportService.AddReportCardStockByDate(ReportIndex.ReportCardstockbydatesIndex, itemRead);
                        }

                        startTime = startTime.AddDays(1);
                    }
                }
                //6.Đồng bộ phần báo cáo NXT mã thẻ theo nhà cung cấp
                else if (request.Status == 6)
                {
                    var startTime = request.Date.Value.Date;
                    var endTime = request.Date.Value.Date;
                    if (request.ToDate != null)
                    {
                        if (request.Date.Value > request.ToDate.Value.Date)
                            return null;
                        endTime = request.ToDate.Value.Date;
                    }

                    while (startTime <= endTime)
                    {
                        var list = await _balanceReportService.ReportQueryCardStockProviderByDateRequest(startTime.Date);
                        var listElast = await _elasticReportService.GetCardStockProviderByDateList(startTime.Date);
                        var msl = (from x in list select x.StockCode + "-" + x.ProviderCode + "-" + x.ProductCode + "-" + x.ShortDate).ToList();
                        var mslE = (from x in listElast select x.StockCode + "-" + x.ProviderCode + "-" + x.ProductCode + "-" + x.ShortDate).ToList();

                        var exceptList = msl.Except(mslE).ToList();
                        foreach (var item in exceptList)
                        {
                            _logger.LogInformation($"Input DataSysnc request: {item}");
                            var x = item.Split('-');
                            var itemRead = list.SingleOrDefault(c => c.StockCode == x[0] && c.ProviderCode == x[1] && c.ProductCode == x[2] && c.ShortDate == x[3]);
                            await _elasticReportService.AddReportCardStockProviderByDate(ReportIndex.ReportCardstockproviderbydates, itemRead);
                        }

                        startTime = startTime.AddDays(1);
                    }
                }
                else if (request.Status == 8)
                {
                    return _elasticReportService.CheckReportBalanceAndHistory(request.Date.Value);
                }
                else if (request.Status == 9)
                {
                    return _autoService.SysJobReport();
                }
                else if (request.Status == 10)
                {
                    return _exportingService.ProcessWarning();
                }
                //11.Lấy dữ liệu của tài khoản
                else if (request.Status == 11)
                {
                    var balance = await _elasticReportService.GetBalanceAgent(request.TransCode, request.Date.Value, request.Date.Value.AddDays(1));
                    var sale = await _elasticReportService.getSaleByAccount(request.TransCode, request.Date.Value);
                    return new
                    {
                        balance = balance.ToJson(),
                        sale = sale.ToJson()
                    };
                }
                //12.Cập nhật dữ liệu vào bảng NXT
                else if (request.Status == 12)
                {
                    var balance = await _elasticReportService.GetBalanceAgent(request.TransCode, request.Date.Value, request.Date.Value.AddDays(1));
                    var sale = await _elasticReportService.getSaleByAccount(request.TransCode, request.Date.Value);
                    if (balance != null && sale != null)
                        await _balanceReportService.UpdateBalanceByInput(new ReportAccountBalanceDay()
                        {
                            AccountCode = request.TransCode,
                            CurrencyCode = "VND",
                            TextDay = request.TransCode + "_" + request.Date.Value.ToString("yyyyMMdd"),
                            Credite = sale.Credite,
                            Debit = sale.Debit,
                            IncDeposit = sale.IncDeposit,
                            IncOther = sale.IncOther,
                            DecPayment = sale.DecPayment,
                            DecOther = sale.DecOther,
                            BalanceBefore = balance.BalanceBefore,
                            BalanceAfter = balance.BalanceAfter,
                        });
                }
                else if (request.Status == 13)
                {
                    var res = await _elasticReportService.ReportTotal0hDateAuto(new ReportTotalAuto0hRequest()
                    {
                        FromDate = request.Date.Value,
                        ToDate = request.ToDate.Value,
                        Limit = int.MaxValue,
                        Offset = 0,
                    });

                    return res;
                }
                else if (request.Status == 14)
                {
                    await _exportingService.ProcessReportNXT();
                }
                else if (request.Status == 15)
                {
                    await _exportingService.ProcessTotalRevenue();
                }
                else if (request.Status == 16)
                {
                    await _exportingService.ProcessCompareSystemAccount(request.Date ?? DateTime.Now.Date.AddDays(-1));
                }

                return "true";
            }
            ////Xuất file báo cáo hàng ngày
            //else if (request.Type == 90)
            //    return await _exportingService.ExportFileTrans(request.Date.Value.Date);
            //else if (request.Type == 91)
            //{
            //    var register = await _balanceReportService.GetRegisterInfo("BATCH_FILE");
            //    return await _exportingService.ExportFile(register, request.Date.Value);
            //}
            else
            {
                if (request.Type == 66)
                {
                    await SysDayOneProcessAfter(request.ProviderCode, request.Date.Value, request.ToDate.Value);
                    return true;
                }
                else
                {

                    var rs = await _balanceReportService.SyncInfoOnjectRequest(request);
                    _logger.LogInformation($"SyncInfoOnjectRequest return: {rs.ResponseCode} - {rs.ResponseMessage}");
                    return rs;
                }
            }
        }

        private async Task SysDayOneProcessAfter(string accountCode, DateTime fromDate, DateTime toDate)
        {
            try
            {

                #region 1.Loại VND
                var litDate = new List<DateTime>();
                var tmp = fromDate;
                while (tmp <= toDate)
                {
                    litDate.Add(tmp);
                    tmp = tmp.AddDays(1);
                }

                foreach (var date in litDate)
                {
                    var textDay = $"{accountCode}_{date.ToString("yyyyMMdd")}";
                    var request = new ReportDetailRequest()
                    {
                        AccountCode = accountCode,
                        FromDate = date,
                        ToDate = date,
                        Offset = 0,
                        Limit = 3,
                    };

                    var reponse = _searchElastich
                         ? await _elasticReportService.ReportDetailGetList(request)
                         : await _balanceReportService.ReportDetailGetList(request);
                    if (reponse.ResponseCode == "01" && reponse.Total >= 1)
                    {
                        var sumData = reponse.SumData.ConvertTo<ReportTransactionDetailDto>();
                        await _balanceReportService.SysAccountBalanceAfter(accountCode, "VND", textDay, sumData);
                    }
                }

                #endregion


            }
            catch (Exception ex)
            {
                _logger.LogError($"SysDayOneProcessAfter Exception: {ex}");
            }
        }

    }
}
