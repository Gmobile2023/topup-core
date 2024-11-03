using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using HLS.Paygate.Gw.Model.Dtos;
using HLS.Paygate.Report.Domain.Entities;
using HLS.Paygate.Report.Model.Dtos;
using HLS.Paygate.Shared;
using HLS.Paygate.Shared.ConfigDtos;
using MassTransit;
using MassTransit.Middleware;
using Microsoft.Extensions.Logging;
using Paygate.Discovery.Requests.Backends;
using Paygate.Discovery.Requests.Balance;
using Paygate.Discovery.Requests.Reports;
using ServiceStack;

namespace HLS.Paygate.Report.Domain.Services
{
    public partial class ExportingService
    {
        #region 9.Phần xuất batchfile

        public async Task ProcessBatchData()
        {
            try
            {
                var date = DateTime.Now.Date.AddDays(-1);
                _log.LogInformation("Start ProcessBatchData Process");
                var register = await _balanceReportSvc.GetRegisterInfo(ReportRegisterType.TRANSACTION_FILE);
                _log.LogInformation($"ProcessBatchData {(register != null ? register.ToJson() : "")}");
                if (register != null && register.IsAuto)
                {
                    List<ReportItemDetail> saleItems = null;
                    List<ReportAgentBalanceDto> balanceItems = null;
                    List<ReportBalanceHistories> historyItems = null;
                    if (_searchElastich)
                    {
                        saleItems = await _elasticReportService.getSaleReportItem(date);
                        historyItems = await _elasticReportService.getHistoryReportItem(date);
                        balanceItems = await _elasticReportService.getAccountBalanceItem(date);
                    }

                    var sourcePath = await _balanceReportSvc.GetForderCreate(ReportConst.Auto, register.Extend);
                    await _balanceReportSvc.ExportFileDataAgent(date, saleItems, balanceItems, sourcePath);
                    await _balanceReportSvc.ExportFileBalanceHistory(date, historyItems, sourcePath);
                    await _balanceReportSvc.ZipForderCreate(sourcePath);
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"ProcessExportFile Exception: {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
            }

        }

        #endregion

        #region 10.Check Các trường hợp lệch số liệu

        /// <summary>
        /// 1.Thiếu dữ liệu so với SaleRequest
        /// 2.Thiếu lịch sử giao dịch
        /// 3.Lệch nhà cung cấp
        /// 4.Lệch trạng thái
        /// 5.Giao dịch hoàn tiền thiếu thông tin giao dịch gốc        
        /// </summary>
        /// <returns></returns>        
        public async Task<bool> ProcessDataCheck(DateTime date)
        {
            DateTime fromDate = date.Date;
            DateTime toDate = fromDate.AddDays(1).AddSeconds(-1);
            var register = await _balanceReportSvc.GetRegisterInfo(ReportRegisterType.BU_DATA);
            if (register == null || !register.IsAuto)
                return false;

            var listMail = register.EmailSend.Split(',', ';').ToList();
            string tille = register.Name + "Chạy bù dữ liệu ngày " + fromDate.ToString("dd-MM-yyyy");

            var begin = DateTime.Now;
            var processDate = DateTime.Now;
            double totalSeconds = 0;
            try
            {
                //1.Lấy dữ liệu SaleRequest
                var saleRequest = new List<SaleRequestDto>();
                var arrayDates = getArrayTimeDate(date.Date, register.Total);
                try
                {
                    var keyCode = "SaleTopupData_" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
                    if (register.Extend != "1")
                    {
                        foreach (var time in arrayDates)
                        {
                            var item = await getSaleTopupDataTime(keyCode, time);
                            saleRequest.AddRange(item);
                        }
                    }
                    else
                    {
                        Parallel.ForEach(arrayDates, time =>
                        {
                            var item = getSaleTopupDataTime(keyCode, time).Result;
                            saleRequest.AddRange(item);
                        });
                    }

                    totalSeconds = DateTime.Now.Subtract(begin).TotalSeconds;
                    _log.LogInformation($"ProcessDataCheck_Read GetSaleRequest Seconds : {totalSeconds}");

                    if (saleRequest.Count == 0)
                    {
                        _emailSender.SendEmailReportAuto(listMail, tille, "Không lấy được dữ liệu SaleRequest");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    _log.LogInformation($"GetClientCluster GetSaleRequest Exception :  {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
                    _emailSender.SendEmailReportAuto(listMail, tille, $"Không lấy được dữ liệu SaleRequest: {ex.Message}");
                    return false;
                }

                processDate = DateTime.Now;
                var accountHistorys = new List<BalanceHistories>();
                //2.Lấy dữ liệu CustomerHistory                  
                try
                {
                    var keyCode = "BalanceHistoriesData_" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
                    if (register.Extend != "1")
                    {
                        foreach (var time in arrayDates)
                        {
                            var item = await getBalanceHistoryDataTime(keyCode, time);
                            accountHistorys.AddRange(item);
                        }
                    }
                    else
                    {
                        Parallel.ForEach(arrayDates, time =>
                        {
                            var item = getBalanceHistoryDataTime(keyCode, time).Result;
                            accountHistorys.AddRange(item);
                        });
                    }
                    totalSeconds = DateTime.Now.Subtract(processDate).TotalSeconds;
                    _log.LogInformation($"ProcessDataCheck_Read GetBalanceHistotyAsync Seconds : {totalSeconds}");

                    if (accountHistorys.Count == 0)
                    {
                        _emailSender.SendEmailReportAuto(listMail, tille, "Không lấy được dữ liệu CustomerBlanceHistory");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    _log.LogInformation($"GetClientCluster BalanceHistoriesRequest Exception :  {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
                    _emailSender.SendEmailReportAuto(listMail, tille, $"Không lấy được dữ liệu CustomerBlanceHistory: {ex.Message}");
                    return false;
                }

                //3.Lấy dữ liệu ReportItemDetail
                processDate = DateTime.Now;
                Expression<Func<ReportItemDetail, bool>> queryRptItemDetail = p => p.CreatedTime >= fromDate.ToUniversalTime() && p.CreatedTime <= toDate.ToUniversalTime();
                var reportItemDetails = await _reportMongoRepository.GetAllAsync(queryRptItemDetail);
                totalSeconds = DateTime.Now.Subtract(processDate).TotalSeconds;
                _log.LogInformation($"ProcessDataCheck_Read GetReportItemDetail Seconds : {totalSeconds}");

                if (reportItemDetails.Count <= 0)
                {
                    _emailSender.SendEmailReportAuto(listMail, tille, "Không lấy được dữ liệu reportItemDetails");
                    return false;
                }

                //4.Lấy dữ liệu ReportCustomerBalance
                processDate = DateTime.Now;
                Expression<Func<ReportBalanceHistories, bool>> queryRptBalanceHistories = p => p.CreatedDate >= fromDate.ToUniversalTime() && p.CreatedDate < toDate.ToUniversalTime();
                var reportBalanceHistories = await _reportMongoRepository.GetAllAsync(queryRptBalanceHistories);
                totalSeconds = DateTime.Now.Subtract(processDate).TotalSeconds;
                _log.LogInformation($"ProcessDataCheck_Read GetReportBalanceHistories Seconds : {totalSeconds}");
                if (reportBalanceHistories.Count <= 0)
                {
                    _emailSender.SendEmailReportAuto(listMail, tille, "Không lấy được dữ liệu reportItemDetails");
                    return false;
                }
                //6.Process
                processDate = DateTime.Now;
                int buThieuGiaoDichInt = await BuThieuGiaoDich(listMail, tille, saleRequest, reportItemDetails);
                totalSeconds = DateTime.Now.Subtract(processDate).TotalSeconds;
                _log.LogInformation($"ProcessDataCheck_Read BuThieuGiaoDich Seconds : {totalSeconds}");

                processDate = DateTime.Now;
                int buLechProviderStatusInt = await BuLechProviderStatus(listMail, tille, saleRequest, reportItemDetails);
                totalSeconds = DateTime.Now.Subtract(processDate).TotalSeconds;
                _log.LogInformation($"ProcessDataCheck_Read BuLechProviderStatus Seconds : {totalSeconds}");

                processDate = DateTime.Now;
                int buThieuHistoryReportInt = await BuThieuHistoryReport(listMail, tille, accountHistorys, reportBalanceHistories);
                totalSeconds = DateTime.Now.Subtract(processDate).TotalSeconds;
                _log.LogInformation($"ProcessDataCheck_Read BuThieuHistoryReport Seconds : {totalSeconds}");

                processDate = DateTime.Now;
                int buThieuInfoTranSouceInt = await BuThieuInfoTranSouce(listMail, tille, reportItemDetails);
                totalSeconds = DateTime.Now.Subtract(processDate).TotalSeconds;
                _log.LogInformation($"ProcessDataCheck_Read BuThieuInfoTranSouce Seconds : {totalSeconds}");

                processDate = DateTime.Now;
                int buThieuRefunReportInt = await BuThieuRefunReport(listMail, tille, accountHistorys, reportItemDetails);
                totalSeconds = DateTime.Now.Subtract(processDate).TotalSeconds;
                _log.LogInformation($"ProcessDataCheck_Read BuThieuRefunReport Seconds : {totalSeconds}");


                totalSeconds = DateTime.Now.Subtract(begin).TotalSeconds;
                _log.LogInformation($"ProcessDataCheck_Read Total Seconds : {totalSeconds}");

                #region  ==> View:

                StringBuilder strBuilder = new StringBuilder();
                strBuilder.Append(
                    "<table cellpadding='1' cellspacing='1' border='1' class='table-bordered table-hover dataTable' cellspacing='1' cellpadding='1' align='Left' rules='all' style='border-width:0px;width:100%;margin-bottom: 0px'><tr>");
                strBuilder.Append("<th class='align_center' scope='col'>STT</th>");
                strBuilder.Append($"<th class='align_center' scope='col'>Nội dung</th>");
                strBuilder.Append($"<th class='align_center' scope='col'>Số lượng</th>");
                strBuilder.Append("</tr>");
                strBuilder.Append($"<tr><td class='align_left'><b>1</b></td>");
                strBuilder.Append($"<td class='align_right'>Bù GD Từ SaleRequest sang ReportItemDetail</td>");
                strBuilder.Append($"<td class='align_right'>{buThieuGiaoDichInt}</td>");
                strBuilder.Append("</tr>");
                strBuilder.Append($"<tr><td class='align_left'><b>2</b></td>");
                strBuilder.Append($"<td class='align_right'>Đồng bộ GD lệch: Provider,Status từ SaleRequest sang ReportItemDetail</td>");
                strBuilder.Append($"<td class='align_right'>{buLechProviderStatusInt}</td>");
                strBuilder.Append("</tr>");
                strBuilder.Append($"<tr><td class='align_left'><b>3</b></td>");
                strBuilder.Append($"<td class='align_right'>Bù GD thiếu từ CustomerBalanceHistory sang ReportCustomerBalanceHistory</td>");
                strBuilder.Append($"<td class='align_right'>{buThieuHistoryReportInt}</td>");
                strBuilder.Append("</tr>");
                strBuilder.Append($"<tr><td class='align_left'><b>4</b></td>");
                strBuilder.Append($"<td class='align_right'>Bù Thông tin GD gốc cho GD hoàn tiền trong ReportItemDetail</td>");
                strBuilder.Append($"<td class='align_right'>{buThieuInfoTranSouceInt}</td>");
                strBuilder.Append("</tr>");
                strBuilder.Append($"<tr><td class='align_left'><b>5</b></td>");
                strBuilder.Append($"<td class='align_right'>Bù GD hoàn tiền từ CustomerBalanceHistory sang ReportItemDetail</td>");
                strBuilder.Append($"<td class='align_right'>{buThieuRefunReportInt}</td>");
                strBuilder.Append("</tr>");
                strBuilder.Append("</table>");
                string msg = strBuilder.ToString();

                #endregion

                _emailSender.SendEmailReportAuto(listMail, tille, msg);

                return true;

            }
            catch (Exception ex)
            {
                _log.LogError($"ProcessDataCheck Exception: {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
                _emailSender.SendEmailReportAuto(listMail, tille, "Không xử lý được phần bù dữ liệu vì lỗi ngoại lệ. ProcessDataCheck");

                totalSeconds = DateTime.Now.Subtract(begin).TotalSeconds;
                _log.LogInformation($"ProcessDataCheck_Read Total Seconds : {totalSeconds}");
                return false;
            }

        }

        private async Task<int> BuThieuGiaoDich(List<string> listMail, string tille, List<SaleRequestDto> sales, List<ReportItemDetail> reportItems)
        {
            try
            {
                var slit = sales.Where(c => !string.IsNullOrEmpty(c.PaymentTransCode)).Select(c => c.TransCode).ToList();
                var rlit = reportItems.Where(c => c.TransCode != null).Select(c => c.TransCode).ToList();
                var listData = slit.Except(rlit);

                _log.LogInformation($"BuThieuGiaoDich Excep_Total : {listData.Count()}");

                foreach (var sale in listData)
                {
                    var saleRequest = sales.FirstOrDefault(c => c.TransCode == sale);
                    await _balanceReportSvc.SaveReportItemInfoSale(saleRequest);
                }

                _log.LogInformation($"BuThieuGiaoDich: Thanh_cong");
                return listData.Count();
            }
            catch (Exception ex)
            {
                _log.LogError($"BuThieuGiaoDich Exception: {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
                _emailSender.SendEmailReportAuto(listMail, tille, "Bù giao dịch SaleRequest sang ReportItemDetail có lỗi ngoại lệ. BuThieuGiaoDich");
                return 0;
            }
        }

        private async Task<int> BuLechProviderStatus(List<string> listMail, string tille, List<SaleRequestDto> sales, List<ReportItemDetail> reportItems)
        {
            try
            {
                var ssale = (from x in sales
                             select new
                             {
                                 TransCode = x.TransCode,
                                 Status = x.Status == SaleRequestStatus.Success
                                     ? ReportStatus.Success
                                     : x.Status == SaleRequestStatus.Failed || x.Status == SaleRequestStatus.Canceled
                                         ? ReportStatus.Error
                                         : ReportStatus.TimeOut,
                                 Provider = x.Provider,
                                 ProviderTransCode = x.ProviderTransCode,
                             }).ToList();


                var excpetList = (
                    from x in reportItems
                    join y in ssale on x.TransCode equals y.TransCode
                    where x.Status != y.Status || x.ProvidersCode != y.Provider
                    select new
                    {
                        TransCode = x.TransCode,
                        StatusOld = x.Status,
                        Status = y.Status,
                        ProviderOld = x.ProvidersCode,
                        Provider = y.Provider,
                        ProviderTransCode = y.ProviderTransCode,
                    }
                ).ToList();


                _log.LogInformation($"BuLechProviderStatus Excep_Total : {excpetList.Count()}");

                foreach (var item in excpetList)
                {
                    var fReport = reportItems.SingleOrDefault(c => c.TransCode == item.TransCode);
                    if (fReport != null)
                    {
                        if (item.Status != item.StatusOld)
                            fReport.Status = item.Status;

                        if (item.ProviderOld != item.Provider)
                        {
                            var provider = await _balanceReportSvc.GetProviderBackend(item.Provider);
                            fReport.ProvidersCode = item.Provider;
                            fReport.PayTransRef = item.ProviderTransCode;
                            fReport.ProvidersInfo = provider.ProviderName;
                        }
                        await _reportMongoRepository.UpdateOneAsync(fReport);
                    }
                }

                _log.LogInformation($"BuLechProviderStatus: Thanh_cong");
                return excpetList.Count();
            }
            catch (Exception ex)
            {
                _log.LogError($"BuLechProviderStatus Exception: {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
                _emailSender.SendEmailReportAuto(listMail, tille, "Đồng bộ trạng thái giao dich và nhà cung cấp SaleRequest sang ReportItemDetail có lỗi ngoại lệ. BuLechProviderStatus");
                return 0;
            }
        }

        private async Task<int> BuThieuHistoryReport(List<string> listMail, string tille, List<BalanceHistories> listHistorys, List<ReportBalanceHistories> listRptHistorys)
        {
            try
            {
                var slit = listHistorys.Select(c => c.TransCode).ToList();
                var rlit = listRptHistorys.Select(c => c.TransCode).ToList();
                var listData = slit.Except(rlit);
                _log.LogInformation($"BuThieuHistoryReport Excep_Total : {listData.Count()}");

                foreach (var transCode in listData)
                {
                    var history = listHistorys.FirstOrDefault(c => c.TransCode == transCode);
                    await _balanceReportSvc.SaveBalanceHistorySouce(history);
                }

                _log.LogInformation($"BuThieuHistoryReport: Thanh_cong");
                return listData.Count();
            }
            catch (Exception ex)
            {
                _log.LogError($"BuThieuHistoryReport Exception: {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
                _emailSender.SendEmailReportAuto(listMail, tille, "Bù giao dịch từ CustomerBalanceHistory sang ReportItemDetail có lỗi ngoại lệ. BuThieuHistoryReport");
                return 0;
            }
        }

        private async Task<int> BuThieuInfoTranSouce(List<string> listMail, string tille, List<ReportItemDetail> reportItems)
        {
            try
            {
                var list = reportItems.Where(c => c.TransType == ReportServiceCode.REFUND && string.IsNullOrEmpty(c.RequestTransSouce)).ToList();
                _log.LogInformation($"BuThieuInfoTranSouce Excep_Total : {list.Count()}");

                foreach (var item in list)
                {
                    var transCode = item.TransNote.Split(':')[1].Trim(' ').TrimEnd(' ').TrimStart(' ');
                    Expression<Func<ReportItemDetail, bool>> querySouce1 = p => p.TransCode == transCode;
                    Expression<Func<ReportItemDetail, bool>> querySouce2 = p => p.RequestRef == transCode;
                    Expression<Func<ReportItemDetail, bool>> querySouce3 = p => p.PaidTransCode == transCode;
                    var itemSouce = await _reportMongoRepository.GetOneAsync(querySouce1)
                        ?? await _reportMongoRepository.GetOneAsync(querySouce2)
                        ?? await _reportMongoRepository.GetOneAsync(querySouce3);

                    if (itemSouce != null)
                        await _balanceReportSvc.SaveCompensationSouceRefund(item, itemSouce);
                }

                _log.LogInformation($"BuThieuInfoTranSouce: Thanh_cong");
                return list.Count();
            }
            catch (Exception ex)
            {
                _log.LogError($"BuThieuInfoTranSouce Exception: {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
                _emailSender.SendEmailReportAuto(listMail, tille, "Bù giao dịch hoàn tiền từ CustomerBalanceHistory sang ReportItemDetail có lỗi ngoại lệ. BuThieuInfoTranSouce ");
                return 0;
            }
        }

        private async Task<int> BuThieuRefunReport(List<string> listMail, string tille, List<BalanceHistories> listHistorys, List<ReportItemDetail> reportItems)
        {
            try
            {
                var slit = listHistorys.Where(c => c.CurrencyCode == "VND").Select(c => c.TransCode).ToList();
                var rlit = reportItems.Where(c => c.PaidTransCode != null).Select(c => c.PaidTransCode).ToList();
                var listData = slit.Except(rlit).ToList();
                _log.LogInformation($"BuThieuRefunReport Excep_Total : {listData.Count()}");

                foreach (var transCode in listData)
                {
                    Expression<Func<ReportBalanceHistories, bool>> queryHistory = p => p.TransCode == transCode;
                    var history = await _reportMongoRepository.GetOneAsync<ReportBalanceHistories>(queryHistory);
                    if (history != null)
                        await _balanceReportSvc.SaveCompensationReportItem(history);
                }

                _log.LogInformation($"BuThieuRefunReport: Thanh_cong");
                return listData.Count();
            }
            catch (Exception ex)
            {
                _log.LogError($"BuThieuRefunReport Exception: {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
                _emailSender.SendEmailReportAuto(listMail, tille, "Bù giao dịch hoàn tiền từ CustomerBalanceHistory sang ReportItemDetail có lỗi ngoại lệ. BuThieuRefunReport");
                return 0;
            }
        }

        private async Task PushFtpPartnerCsv(string strReadFile, string parentCode, string fileName)
        {
            try
            {
                byte[] fileBytes;
                var fs = new FileStream(strReadFile, FileMode.Open, FileAccess.Read);
                var br = new BinaryReader(fs);
                var numBytes = new FileInfo(strReadFile).Length;
                fileBytes = br.ReadBytes((int)numBytes);
                var linkFile = _uploadFile.UploadFileReportPartnerToDataServer(parentCode, fileBytes, fileName);
                fs.Close();
                await fs.DisposeAsync();
            }
            catch (Exception ex)
            {
                _log.LogError($"PushFtpPartnerCsv ServerFpt Exception: {ex}");
            }
        }

        public async Task PushFtpPartnerXlsx(byte[] fileBytes, string parentCode, string fileName)
        {
            try
            {
                var linkFile = _uploadFile.UploadFileReportPartnerToDataServer(parentCode, fileBytes, fileName);
            }
            catch (Exception ex)
            {
                _log.LogError($"PushFtpPartnerXlsx ServerFpt Exception: {ex}");
            }
        }

        #endregion

        private List<TimeDto> getArrayTimeDateMinutes(DateTime start, DateTime end, int minutesStep)
        {
            var temp = start;
            var ls = new List<TimeDto>();

            while (temp < end)
            {
                ls.Add(new TimeDto()
                {
                    StartTime = temp,
                    EndTime = temp.AddMinutes(minutesStep),
                });
                temp = temp.AddMinutes(minutesStep);
            }
            return ls;
        }

        private async Task<List<SaleRequestDto>> getSaleTopupDataTime(string key, TimeDto dto)
        {
            var begin = DateTime.Now;
            int retry = 0;

            try
            {
                var client = new JsonServiceClient(_apiUrl)
                {
                    Timeout = TimeSpan.FromMinutes(20)
                };
                _log.LogInformation($"key= {key}|getSaleTopupDataTime|retry= {retry}|StartTime= {dto.StartTime.ToString("yyyy-MM-dd HH:mm:ss")} - {dto.EndTime.ToString("yyyy-MM-dd HH:mm:ss")}");
                var reponse = await client.GetAsync<ResponseMesssageObject<string>>(new GetReportSaleTopupRequest { FromDate = dto.StartTime, ToDate = dto.EndTime });
                var totalSeconds = DateTime.Now.Subtract(begin).TotalSeconds;
                _log.LogInformation($"key= {key}|getSaleTopupDataTime|retry= {retry}|ResponseCode= {reponse.ResponseCode}|Total= {reponse.Total}|Seconds= {totalSeconds}");
                return reponse.Payload.FromJson<List<SaleRequestDto>>();
            }
            catch (Exception ex)
            {
                var totalSeconds = DateTime.Now.Subtract(begin).TotalSeconds;
                _log.LogError($"key= {key}|getSaleTopupDataTime|retry= {retry}|Seconds= {totalSeconds}|Exception= {ex.Message}|{ex.StackTrace}|{ex.InnerException}");
                retry += 1;
            }

            if (retry >= 1)
            {
                begin = DateTime.Now;

                var forDate = getArrayTimeDateMinutes(dto.StartTime, dto.EndTime, 20);
                var reponseData = new List<SaleRequestDto>();
                foreach (var date in forDate)
                {
                    var dataItem = await getSaleTopupDataTimePrivate(key, date);
                    reponseData.AddRange(dataItem);
                }

                var totalSeconds = DateTime.Now.Subtract(begin).TotalSeconds;
                _log.LogInformation($"key= {key}|getSaleTopupDataTime|retry= {retry}|Total= {reponseData.Count()}|Seconds= {totalSeconds}");
                return reponseData;
            }

            return new List<SaleRequestDto>();
        }


        private async Task<List<SaleRequestDto>> getSaleTopupDataTimePrivate(string key, TimeDto dto)
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
                    _log.LogInformation($"key= {key}|getSaleTopupDataTime|retry= {retry}|StartTime= {dto.StartTime.ToString("yyyy-MM-dd HH:mm:ss")} - {dto.EndTime.ToString("yyyy-MM-dd HH:mm:ss")}");
                    var reponse = await client.GetAsync<ResponseMesssageObject<string>>(new GetReportSaleTopupRequest { FromDate = dto.StartTime, ToDate = dto.EndTime });
                    //var reponse = await _grpcClient.GetClientCluster(GrpcServiceName.Backend).SendAsync<ResponseMesssageObject<string>>(new GetSaleTopupRequest { FromDate = dto.StartTime, ToDate = dto.EndTime });
                    var totalSeconds = DateTime.Now.Subtract(begin).TotalSeconds;
                    _log.LogInformation($"key= {key}|getSaleTopupDataTime|retry= {retry}|ResponseCode= {reponse.ResponseCode}|Total= {reponse.Total}|Seconds= {totalSeconds}");
                    return reponse.Payload.FromJson<List<SaleRequestDto>>();
                }
                catch (Exception ex)
                {
                    var totalSeconds = DateTime.Now.Subtract(begin).TotalSeconds;
                    _log.LogError($"key= {key}|getSaleTopupDataTime|retry= {retry}|Seconds= {totalSeconds}|Exception= {ex.Message}|{ex.StackTrace}|{ex.InnerException}");
                    retry += 1;
                }
            }

            return new List<SaleRequestDto>();
        }

        private async Task<List<BalanceHistories>> getBalanceHistoryDataTime(string key, TimeDto dto)
        {
            int retry = 0;

            var begin = DateTime.Now;
            try
            {
                var client = new JsonServiceClient(_apiUrl)
                {
                    Timeout = TimeSpan.FromMinutes(20)
                };

                _log.LogInformation($"key= {key}|getBalanceHistoryDataTime|retry= {retry}|StartTime= {dto.StartTime.ToString("yyyy-MM-dd HH:mm:ss")} - {dto.EndTime.ToString("yyyy-MM-dd HH:mm:ss")}");
                var reponse = await client.GetAsync<ResponseMesssageObject<string>>(new BalanceHistoriesRequest { FromDate = dto.StartTime, ToDate = dto.EndTime });
                //var reponse = await _grpcClient.GetClientCluster(GrpcServiceName.Balance).SendAsync<ResponseMesssageObject<string>>(new BalanceHistoriesRequest { FromDate = dto.StartTime, ToDate = dto.EndTime });
                var totalSeconds = DateTime.Now.Subtract(begin).TotalSeconds;
                _log.LogInformation($"key= {key}|getBalanceHistoryDataTime|retry= {retry}|ResponseCode= {reponse.ResponseCode}|Total= {reponse.Total}|Seconds= {totalSeconds}");
                return reponse.Payload.FromJson<List<BalanceHistories>>();
            }
            catch (Exception ex)
            {
                var totalSeconds = DateTime.Now.Subtract(begin).TotalSeconds;
                _log.LogError($"key= {key}|getBalanceHistoryDataTime|retry= {retry}|Seconds= {totalSeconds}|Exception= {ex.Message}|{ex.StackTrace}|{ex.InnerException}");
                retry += 1;
            }

            if (retry >= 1)
            {
                begin = DateTime.Now;
                var forDate = getArrayTimeDateMinutes(dto.StartTime, dto.EndTime, 20);
                var reponseData = new List<BalanceHistories>();
                foreach (var date in forDate)
                {
                    var dataItem = await getBalanceHistoryDataTimePrivate(key, date);
                    reponseData.AddRange(dataItem);
                }

                var totalSeconds = DateTime.Now.Subtract(begin).TotalSeconds;
                _log.LogInformation($"key= {key}|getBalanceHistoryDataTime|retry= {retry}|Total= {reponseData.Count()}|Seconds= {totalSeconds}");
                return reponseData;
            }

            return new List<BalanceHistories>();
        }

        private async Task<List<BalanceHistories>> getBalanceHistoryDataTimePrivate(string key, TimeDto dto)
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

                    _log.LogInformation($"key= {key}|getBalanceHistoryDataTime|retry= {retry}|StartTime= {dto.StartTime.ToString("yyyy-MM-dd HH:mm:ss")} - {dto.EndTime.ToString("yyyy-MM-dd HH:mm:ss")}");
                    var reponse = await client.GetAsync<ResponseMesssageObject<string>>(new BalanceHistoriesRequest { FromDate = dto.StartTime, ToDate = dto.EndTime });
                    //var reponse = await _grpcClient.GetClientCluster(GrpcServiceName.Balance).SendAsync<ResponseMesssageObject<string>>(new BalanceHistoriesRequest { FromDate = dto.StartTime, ToDate = dto.EndTime });
                    var totalSeconds = DateTime.Now.Subtract(begin).TotalSeconds;
                    _log.LogInformation($"key= {key}|getBalanceHistoryDataTime|retry= {retry}|ResponseCode= {reponse.ResponseCode}|Total= {reponse.Total}|Seconds= {totalSeconds}");
                    return reponse.Payload.FromJson<List<BalanceHistories>>();
                }
                catch (Exception ex)
                {
                    var totalSeconds = DateTime.Now.Subtract(begin).TotalSeconds;
                    _log.LogError($"key= {key}|getBalanceHistoryDataTime|retry= {retry}|Seconds= {totalSeconds}|Exception= {ex.Message}|{ex.StackTrace}|{ex.InnerException}");
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