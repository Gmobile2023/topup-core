using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Topup.Report.Model.Dtos;
using Topup.Report.Model.Dtos.RequestDto;
using Topup.Shared;
using Topup.Shared.CacheManager;
using Topup.Shared.Emailing;
using Topup.Shared.Helpers;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Topup.Contracts.Commands.Commons;
using Topup.Contracts.Requests.Commons;
using Topup.Discovery.Requests.Stocks;
using Topup.Gw.Model.Dtos;
using ServiceStack;
using Topup.Report.Domain.Connectors;
using Topup.Report.Domain.Entities;
using Topup.Report.Domain.Exporting;
using Topup.Report.Domain.Repositories;

namespace Topup.Report.Domain.Services
{
    public interface IExportingService
    {
        Task ProcessReportNXT();
        Task ProcessBatchFile();
        Task ProcessSms();
        Task ProcessBalanceSupplier();
        Task ProcessTotalRevenue();
        Task ProcessCompareAgentPartner();
        Task ProcessCompareSystemAccount(DateTime date);
        Task<NotCompleteWarning> SendMailComparePartner(UserInfoPeriodDto account, DateTime fromDate, DateTime toDate,
            string period, int day, bool isAuto = true, string email = "", bool sendAlertSuccess = false, bool isWarning = false);
        Task<UserInfoPeriodDto> GetUserExportingPeriodAsync(string agentCode);
        Task<MessageResponseBase> ExportFile(ReportRegisterInfo register, DateTime date);
        Task ProcessWarning();
        Task ProcessBatchData();
        Task<bool> ProcessDataCheck(DateTime date);
        Task SysDayOneProcess();
        Task SysDeleteFileFpt();
        Task SysDeleteFile();
        Task PushFtpPartnerXlsx(byte[] fileBytes, string parentCode, string fileName);
    }

    public partial class ExportingService : IExportingService
    {
        private readonly IReportMongoRepository _reportMongoRepository;
        private readonly IBalanceReportService _balanceReportSvc;
        private readonly IEmailSender _emailSender;
        private readonly IExportDataExcel _exportData;
        private readonly ICacheManager _cacheManager;
        private readonly IDateTimeHelper _dateHepper;
        private readonly ICompareService _compareSvc;
        private readonly ILogger<ExportingService> _log;
        private readonly WebApiConnector _externalService;
        private readonly IFileUploadRepository _uploadFile;
        private readonly IElasticReportRepository _elasticReportService;
        private readonly IBusControl _bus;
        private readonly string _chatId;
        private readonly string _apiUrl;

        IConfiguration Configuration { get; }

        //private readonly IServiceGateway _gateway; gunner
        private readonly bool _searchElastich;
        private readonly GrpcClientHepper _grpcClient;
        public ExportingService(IEmailSender emailSender,
            IBalanceReportService balanceReportSvc,
            IReportMongoRepository reportMongoRepository,
            ICompareService compareSvc,
            IDateTimeHelper dateHepper,
            IExportDataExcel exportData,
            ICacheManager cacheManager,
            WebApiConnector externalService,
            IBusControl bus,
            IConfiguration configuration,
            IFileUploadRepository uploadFile,
            IElasticReportRepository elasticReportService,
            ILogger<ExportingService> log,
            GrpcClientHepper grpcClient)
        {
            _emailSender = emailSender;
            _exportData = exportData;
            _dateHepper = dateHepper;
            _reportMongoRepository = reportMongoRepository;
            _balanceReportSvc = balanceReportSvc;
            _cacheManager = cacheManager;
            _compareSvc = compareSvc;
            _externalService = externalService;
            _bus = bus;
            _log = log;
            Configuration = configuration;
            _uploadFile = uploadFile;
            _elasticReportService = elasticReportService;
            //_gateway = HostContext.AppHost.GetServiceGateway(); gunner
            var isSearch = Configuration["ElasticSearch:IsSearch"];
            _chatId = Configuration["Telegram:CompareChatId"];
            _apiUrl = Configuration["ServiceUrlConfig:GatewayPrivate"];
            if (isSearch == null)
            {
                _searchElastich = false;
            }
            else
            {
                if (Convert.ToBoolean(isSearch)) _searchElastich = true;
                else _searchElastich = false;
            }
            _grpcClient = grpcClient;
        }

        #region 1.Báo cáo tự động NXT Mã thẻ

        public async Task ProcessReportNXT()
        {
            try
            {
                _log.LogInformation("Start SysAutoReportNXT Process");
                var register = await _balanceReportSvc.GetRegisterInfo(ReportRegisterType.CARD_NXT);
                List<ReportCardStockDayDto> list = null;
                _log.LogInformation($"SysAutoReportNXT {(register != null ? register.ToJson() : "")}");
                if (register != null && register.IsAuto && !string.IsNullOrEmpty(register.EmailSend))
                {
                    list = _searchElastich
                       ? await _elasticReportService.CardStockDateAuto(new CardStockAutoRequest()
                       {
                           FromDate = DateTime.Now.Date.AddDays(-1),
                           ToDate = DateTime.Now.Date,
                           Offset = 0,
                           Limit = int.MaxValue,
                       })
                       : await _balanceReportSvc.CardStockDateAuto(new CardStockAutoRequest()
                       {
                           FromDate = DateTime.Now.Date.AddDays(-1),
                           ToDate = DateTime.Now.Date,
                           Offset = 0,
                           Limit = int.MaxValue,
                       });

                    var listMail = register.EmailSend.Split(',', ';').ToList();
                    var fileName = $"{register.Content}_{DateTime.Now.Date:ddMMyyyy}.xlsx";
                    string sendFile = "";
                    var excel = _exportData.ReportCardStockAutoToFile(list, fileName);
                    if (excel != null)
                    {
                        var fileBytes = await _cacheManager.GetFile(excel.FileToken);
                        if (fileBytes != null)
                        {
                            var sourcePath = await _balanceReportSvc.GetForderCreate(ReportConst.NXT);
                            var pathSave = $"{sourcePath.PathName}/{fileName}";
                            await File.WriteAllBytesAsync(pathSave, fileBytes);
                            sendFile = pathSave;// await _balanceReportSvc.ZipForderCreate(sourcePath);
                        }
                    }

                    var dataTable = FillDataBodyByTableNXT(list);
                    _emailSender.SendEmailReportAuto(listMail, fileName.Split('.')[0], dataTable, sendFile);
                }

                var registerWarning = await _balanceReportSvc.GetRegisterInfo(ReportRegisterType.WarningCard);
                if (registerWarning != null && registerWarning.IsAuto && !string.IsNullOrEmpty(registerWarning.EmailSend))
                {
                    var listProvider = await _elasticReportService.CardStockProviderDateAuto(new CardStockAutoRequest()
                    {
                        FromDate = DateTime.Now.Date.AddDays(-1),
                        ToDate = DateTime.Now.Date,
                        Offset = 0,
                        Limit = int.MaxValue,
                    });

                    if (list == null)
                    {
                        list = _searchElastich
                       ? await _elasticReportService.CardStockDateAuto(new CardStockAutoRequest()
                       {
                           FromDate = DateTime.Now.Date.AddDays(-1),
                           ToDate = DateTime.Now.Date,
                           Offset = 0,
                           Limit = int.MaxValue,
                       })
                       : await _balanceReportSvc.CardStockDateAuto(new CardStockAutoRequest()
                       {
                           FromDate = DateTime.Now.Date.AddDays(-1),
                           ToDate = DateTime.Now.Date,
                           Offset = 0,
                           Limit = int.MaxValue,
                       });
                    }

                    var listMail = registerWarning.EmailSend.Split(',', ';').ToList();
                    var fileName = $"{registerWarning.Content}_{DateTime.Now.Date:ddMMyyyy}.";
                    var dataTableWait = FillBodyByTableCardWarning(list, listProvider);
                    _emailSender.SendEmailReportAuto(listMail, fileName.Split('.')[0], dataTableWait);
                }
            }
            catch (Exception ex)
            {
                _log.LogInformation($"ProcessReportNXT_Exception: {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
            }
        }

        private string FillDataBodyByTableNXT(List<ReportCardStockDayDto> listResult)
        {
            try
            {
                StringBuilder strBuilder = new StringBuilder();
                strBuilder.Append(
                    "<table cellpadding='1' cellspacing='1' border='1' class='table-bordered table-hover dataTable' cellspacing='1' cellpadding='1' align='Left' rules='all' style='border-width:0px;width:100%;margin-bottom: 0px'><tr><th class='align_center' rowspan='2'>STT</th>");
                strBuilder.Append(
                    "<th class='align_center' rowspan='2'>Dịch vụ</th><th class='align_center' rowspan='2'>Loại sản phẩm</th><th class='align_center' rowspan='2'>Sản phẩm</th><th class='align_center' rowspan='2'>Mệnh giá</th><th class='align_center' colspan='5'>Kho Temp</th>");
                strBuilder.Append("<th class='align_center' colspan='5'>Kho Sale</th></tr><tr>");
                strBuilder.Append(
                    "<th class='align_center' scope='col'>Tồn đầu kỳ</th><th class='align_center' scope='col'>SL Nhập</th><th class='align_center' scope='col'>SL Xuất</th><th class='align_center' scope='col'>Tồn cuối kỳ</th>");
                strBuilder.Append(
                    "<th class='align_center' scope='col'>Thành tiền tồn cuối</th><th class='align_center' scope='col'>Tồn đầu kỳ</th><th class='align_center' scope='col'>SL Nhập</th><th class='align_center' scope='col'>SL Xuất</th>");
                strBuilder.Append(
                    "<th class='align_center' scope='col'>Tồn cuối kỳ</th><th class='align_center' scope='col'>Thành tiền tồn cuối</th></tr>");
                strBuilder.Append(
                    string.Format("<tr><td class='align_left'  colspan='5' style='width:200px;'><b>TỔNG</b></td>"));
                strBuilder.Append(
                    $"<td class='align_right'>{listResult.Sum(c => c.Before_Temp):N0}</td>");
                strBuilder.Append(
                    $"<td class='align_right'>{listResult.Sum(c => c.Import_Temp):N0}</td>");
                strBuilder.Append(
                    $"<td class='align_right'>{listResult.Sum(c => c.Export_Temp):N0}</td>");
                strBuilder.Append(
                    $"<td class='align_right'>{listResult.Sum(c => c.After_Temp):N0}</td>");
                strBuilder.Append(
                    $"<td class='align_right'>{listResult.Sum(c => c.Monney_Temp):N0}</td>");
                strBuilder.Append(
                    $"<td class='align_right'>{listResult.Sum(c => c.Before_Sale):N0}</td>");
                strBuilder.Append(
                    $"<td class='align_right'>{listResult.Sum(c => c.Import_Sale):N0}</td>");
                strBuilder.Append(
                    $"<td class='align_right'>{listResult.Sum(c => c.Export_Sale):N0}</td>");
                strBuilder.Append(
                    $"<td class='align_right'>{listResult.Sum(c => c.After_Sale):N0}</td>");
                strBuilder.Append(
                    $"<td class='align_right'>{listResult.Sum(c => c.Monney_Sale):N0}</td></tr>");
                int index = 1;
                foreach (var rpt in listResult)
                {
                    strBuilder.Append($"<tr><td class='align_left' style='width:200px;'><b>{index}</b></td>");
                    strBuilder.Append($"<td class='align_left'>{rpt.ServiceName}</td>");
                    strBuilder.Append($"<td class='align_left'>{rpt.CategoryName}</td>");
                    strBuilder.Append($"<td class='align_left'>{rpt.ProductName}</td>");
                    strBuilder.Append($"<td class='align_right'>{rpt.CardValue:N0}</td>");
                    strBuilder.Append($"<td class='align_right'>{rpt.Before_Temp:N0}</td>");
                    strBuilder.Append($"<td class='align_right'>{rpt.Import_Temp:N0}</td>");
                    strBuilder.Append($"<td class='align_right'>{rpt.Export_Temp:N0}</td>");
                    strBuilder.Append($"<td class='align_right'>{rpt.After_Temp:N0}</td>");
                    strBuilder.Append($"<td class='align_right'>{rpt.Monney_Temp:N0}</td>");
                    strBuilder.Append($"<td class='align_right'>{rpt.Before_Sale:N0}</td>");
                    strBuilder.Append($"<td class='align_right'>{rpt.Import_Sale:N0}</td>");
                    strBuilder.Append($"<td class='align_right'>{rpt.Export_Sale:N0}</td>");
                    strBuilder.Append($"<td class='align_right'>{rpt.After_Sale:N0}</td>");
                    strBuilder.Append($"<td class='align_right'>{rpt.Monney_Sale:N0}</td></tr>");
                    index = index + 1;
                }

                strBuilder.Append("</table>");
                return strBuilder.ToString();
            }
            catch (Exception ex)
            {
                _log.LogInformation($"FillDataBodyByTableNXT Exception: {ex}");
                return null;
            }
        }

        #endregion

        #region 2.Báo cáo tự động tổng hợp

        public async Task ProcessTotalRevenue()
        {
            try
            {
                _log.LogInformation("Start ProcessTotalRevenue Process");
                var register = await _balanceReportSvc.GetRegisterInfo(ReportRegisterType.TOTAL_REVENUE);

                _log.LogInformation($"ProcessTotalRevenue {(register != null ? register.ToJson() : "")}");
                if (register != null && register.IsAuto && !string.IsNullOrEmpty(register.EmailSend))
                {
                    var dateAfter = DateTime.Now.Date.AddDays(-1);
                    var fromDate = new DateTime(dateAfter.Year, dateAfter.Month, 1).Date;
                    var input = new ReportTotalAuto0hRequest()
                    {
                        FromDate = fromDate,
                        ToDate = DateTime.Now.Date,
                        Offset = 0,
                        Limit = int.MaxValue,
                    };
                    var list = _searchElastich
                        ? await _elasticReportService.ReportTotal0hDateAuto(input)
                        : await _balanceReportSvc.ReportTotal0hDateAuto(input);

                    var dataTable = FillDataBodyByTableRevenue(list);
                    var listMail = register.EmailSend.Split(',', ';').ToList();
                    var sendFile = string.Empty;
                    string fileName = $"{register.Content}_{DateTime.Now.Date.ToString("ddMMyyyy")}.xlsx";
                    var excel = _exportData.ReportTotalRevenueAutoToFile(list, fileName);
                    if (excel != null)
                    {
                        var fileBytes = await _cacheManager.GetFile(excel.FileToken);
                        if (fileBytes != null)
                        {
                            var sourcePath = await _balanceReportSvc.GetForderCreate(ReportConst.Revenue);
                            var pathSave = $"{sourcePath.PathName}/{fileName}";
                            await File.WriteAllBytesAsync(pathSave, fileBytes);
                            sendFile = pathSave;// await _balanceReportSvc.ZipForderCreate(sourcePath);
                        }
                    }
                    _emailSender.SendEmailReportAuto(listMail, fileName.Split('.')[0], dataTable, sendFile);
                }
            }
            catch (Exception ex)
            {
                _log.LogInformation($"ProcessTotalRevenue_Exception: {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
            }
        }

        private string FillDataBodyByTableRevenue(List<ReportRevenueTotalAutoDto> listResult)
        {
            try
            {
                StringBuilder strBuilder = new StringBuilder();
                strBuilder.Append(
                    "<table cellpadding='1' cellspacing='1' border='1' class='table-bordered table-hover dataTable' cellspacing='1' cellpadding='1' align='Left' rules='all' style='border-width:0px;width:100%;margin-bottom: 0px'><tr>");
                strBuilder.Append(
                    "<th style='text-align' scope='col'>Ngày</th><th style='text-align' scope='col'>Số lượng ĐL kích hoạt</th><th style='text-align' scope='col'>SL ĐL hoạt động</th><th style='text-align' scope='col'>Số dư ĐL Đầu kỳ</th>");
                strBuilder.Append(
                    "<th style='text-align' scope='col'>DS nạp trong kỳ</th><th class='align_center' scope='col'>Phát sinh tăng khác</th><th style='text-align' scope='col'>DS bán trong kỳ</th><th style='text-align' scope='col'>Phát sinh giảm khác</th>");
                strBuilder.Append("<th style='text-align' scope='col'>Số dư cuối kỳ</th></tr>");
                strBuilder.Append(string.Format("<tr><td style='text-align;width:200px;'><b>Tổng</b></td>"));
                strBuilder.Append($"<td style='right'>{listResult.Sum(c => c.AccountActive):N0}</td>");
                strBuilder.Append($"<td style='right'>{listResult.Sum(c => c.AccountRevenue):N0}</td>");
                strBuilder.Append(string.Format("<td style='right'></td>"));
                strBuilder.Append($"<td style='right'>{listResult.Sum(c => c.InputDeposit):N0}</td>");
                strBuilder.Append($"<td style='right'>{listResult.Sum(c => c.IncOther):N0}</td>");
                strBuilder.Append($"<td style='right'>{listResult.Sum(c => c.Sale):N0}</td>");
                strBuilder.Append($"<td style='right'>{listResult.Sum(c => c.DecOther):N0}</td>");
                strBuilder.Append(string.Format("<td style='right'></td></tr>"));

                foreach (var rpt in listResult)
                {
                    strBuilder.Append(
                        $"<tr><td style='text-align;width:200px;'><b>{rpt.CreatedDay.ToString("dd/MM/yyyy")}</b></td>");
                    strBuilder.Append($"<td style='right'>{rpt.AccountActive:N0}</td>");
                    strBuilder.Append($"<td style='right'>{rpt.AccountRevenue:N0}</td>");
                    strBuilder.Append($"<td style='right'>{Convert.ToDouble(rpt.Before):N0}</td>");
                    strBuilder.Append($"<td style='right'>{Convert.ToDouble(rpt.InputDeposit):N0}</td>");
                    strBuilder.Append($"<td style='right'>{Convert.ToDouble(rpt.IncOther):N0}</td>");
                    strBuilder.Append($"<td style='right'>{Convert.ToDouble(rpt.Sale):N0}</td>");
                    strBuilder.Append($"<td style='right'>{Convert.ToDouble(rpt.DecOther):N0}</td>");
                    strBuilder.Append($"<td style='right'>{Convert.ToDouble(rpt.After):N0}</td></tr>");
                }

                strBuilder.Append("</table>");
                return strBuilder.ToString();
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        #endregion

        #region 3.Báo cáo 0h nhà cung cấp

        public async Task ProcessBalanceSupplier()
        {
            try
            {
                _log.LogInformation("Start ProcessBalanceSupplier Process");
                var register = await _balanceReportSvc.GetRegisterInfo(ReportRegisterType.BALANCE_SUPPLIER);
                _log.LogInformation($"ProcessBalanceSupplier {(register != null ? register.ToJson() : "")}");

                if (register != null && register.IsAuto && !string.IsNullOrEmpty(register.EmailSend))
                {
                    await QuerryBalance(register.Providers.Split(',', ';').ToList());
                    var date = DateTime.Now.Date.AddDays(-1);
                    var input = new ReportBalanceSupplierRequest()
                    {
                        FromDate = new DateTime(date.Year, date.Month, 1).Date,
                        ToDate = DateTime.Now,
                        Offset = 0,
                        Limit = int.MaxValue,
                        Providers = register.Providers,
                    };
                    var list = await _balanceReportSvc.ReportBalanceSupplierAuto(input);

                    var dataTable = FillBodyByTableBalanceSupplier(list, register.Providers);
                    var listMail = register.EmailSend.Split(',', ';').ToList();
                    var sendFile = string.Empty;
                    string fileName = $"{register.Content}_{DateTime.Now.Date:ddMMyyyy}.xlsx";
                    var excel = _exportData.ReportBalanceSupplierAutoToFile(list, fileName);
                    if (excel != null)
                    {
                        var fileBytes = await _cacheManager.GetFile(excel.FileToken);
                        if (fileBytes != null)
                        {
                            var sourcePath = await _balanceReportSvc.GetForderCreate(ReportConst.Balance);
                            var pathSave = $"{sourcePath.PathName}/{fileName}";
                            await File.WriteAllBytesAsync(pathSave, fileBytes);

                            //Chỗ này tạm ko zip file nữa
                            sendFile = pathSave; //await _balanceReportSvc.ZipForderCreate(sourcePath);
                        }
                    }

                    _emailSender.SendEmailReportAuto(listMail, fileName.Split('.')[0], dataTable, sendFile);
                }
            }
            catch (Exception ex)
            {
                _log.LogInformation($"ProcessBalanceSupplier_Exception: {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
            }
        }

        private string FillBodyByTableBalanceSupplier(List<ReportBalanceSupplierDto> listResult, string providers)
        {
            try
            {
                var headders = providers.Split(',', ';', '|');
                StringBuilder strBuilder = new StringBuilder();
                strBuilder.Append(
                    "<table cellpadding='1' cellspacing='1' border='1' class='table-bordered table-hover dataTable' cellspacing='1' cellpadding='1' align='Left' rules='all' style='border-width:0px;width:100%;margin-bottom: 0px'><tr>");
                var headers = listResult.First();
                strBuilder.Append("<th class='align_center' scope='col'>Ngày</th>");
                foreach (var h in headders)
                    strBuilder.Append($"<th class='align_center' scope='col'>{h}</th>");
                strBuilder.Append($"<th class='align_center' scope='col'>Tổng</th>");
                strBuilder.Append("</tr>");

                foreach (var rpt in listResult.OrderByDescending(c => c.CreatedDay))
                {
                    strBuilder.Append(
                        $"<tr><td class='align_left' style='width:200px;'><b>{rpt.CreatedDay.ToString("dd/MM/yyyy")}</b></td>");
                    foreach (var name in headders)
                    {
                        var view = rpt.Items.First(c => c.Name == name);
                        strBuilder.Append(
                            $"<td class='align_right'>{Convert.ToDouble(view.Balance).ToString("N0")}</td>");
                    }

                    var sum = rpt.Items.Sum(c => c.Balance);
                    strBuilder.Append($"<td class='align_right'>{Convert.ToDouble(sum).ToString("N0")}</td>");
                    strBuilder.Append("</tr>");
                }

                strBuilder.Append("</table>");
                return strBuilder.ToString();
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        private async Task QuerryBalance(List<string> accounts)
        {
            foreach (var item in accounts)
            {
                try
                {
                    decimal balance = await _balanceReportSvc.CheckTopupBalance(item);
                    await _balanceReportSvc.UpdateBalanceSupplierInfo(new ReportBalanceSupplierDay()
                    {
                        CreatedDay = DateTime.Now,
                        Balance = balance,
                        SupplierCode = item,
                        SupplierName = item,
                        TextDay = DateTime.Now.ToString("yyyyMMdd")
                    });
                }
                catch (Exception ex)
                {
                    await _balanceReportSvc.UpdateBalanceSupplierInfo(new ReportBalanceSupplierDay()
                    {
                        CreatedDay = DateTime.Now,
                        Balance = 0,
                        SupplierCode = item,
                        SupplierName = item,
                        TextDay = DateTime.Now.ToString("yyyyMMdd")
                    });
                }
            }
        }

        #endregion

        #region 4.Báo cáo chi tiết sms

        public async Task ProcessSms()
        {
            _log.LogInformation("Start ProcessSms Process");
            var register = await _balanceReportSvc.GetRegisterInfo(ReportRegisterType.SMS_MONTH);

            _log.LogInformation($"ProcessSms {(register != null ? register.ToJson() : "")}");

            if (register != null && register.IsAuto && !string.IsNullOrEmpty(register.EmailSend))
            {
                var date = DateTime.Now.Date.AddDays(-1);
                var input = new ReportSmsRequest()
                {
                    FromDate = new DateTime(date.Year, date.Month, 1).Date,
                    ToDate = DateTime.Now.Date,
                    Offset = 0,
                    Limit = int.MaxValue,
                };
                var list = await _balanceReportSvc.ReportSmsAuto(input);

                var dataTable = FillBodyByTableSms(list);
                var listMail = register.EmailSend.Split(',', ';').ToList();
                var sendFile = string.Empty;
                string fileName = $"{register.Content}_{DateTime.Now.Date.ToString("ddMMyyyy")}.xlsx";
                var excel = _exportData.ReportSmsAutoToFile(list, fileName);
                if (excel != null)
                {
                    var fileBytes = await _cacheManager.GetFile(excel.FileToken);
                    if (fileBytes != null)
                    {
                        var sourcePath = await _balanceReportSvc.GetForderCreate(ReportConst.SMS);
                        var pathSave = $"{sourcePath.PathName}\\{fileName}";
                        await File.WriteAllBytesAsync(pathSave, fileBytes);
                        sendFile = await _balanceReportSvc.ZipForderCreate(sourcePath);
                    }
                }

                _emailSender.SendEmailReportAuto(listMail, fileName.Split('.')[0], dataTable, sendFile);
            }
        }

        private string FillBodyByTableSms(List<ReportSmsDto> listResult)
        {
            try
            {
                var list = (from x in listResult
                            group x by x.Channel
                    into g
                            select new
                            {
                                Channel = g.Key,
                                SLTC = g.Sum(c => c.Status == 1 ? 1 : 0),
                                SLTB = g.Sum(c => c.Status == 0 ? 1 : 0),
                            }).ToList();
                StringBuilder strBuilder = new StringBuilder();
                strBuilder.Append(
                    "<table cellpadding='1' cellspacing='1' border='1' class='table-bordered table-hover dataTable' cellspacing='1' cellpadding='1' align='Left' rules='all' style='border-width:0px;width:100%;margin-bottom: 0px'><tr>");
                strBuilder.Append("<th class='align_center' scope='col'>STT</th>");
                strBuilder.Append($"<th class='align_center' scope='col'>Kênh</th>");
                strBuilder.Append($"<th class='align_center' scope='col'>Số lượng thành công</th>");
                strBuilder.Append($"<th class='align_center' scope='col'>Số lượng thất bại</th>");
                strBuilder.Append("</tr>");

                var index = 1;
                foreach (var rpt in list)
                {
                    strBuilder.Append($"<tr><td class='align_left'><b>{index}</b></td>");
                    strBuilder.Append($"<td class='align_right'>{rpt.Channel}</td>");
                    strBuilder.Append($"<td class='align_right'>{Convert.ToDouble(rpt.SLTC):N0}</td>");
                    strBuilder.Append($"<td class='align_right'>{Convert.ToDouble(rpt.SLTB):N0}</td>");
                    strBuilder.Append("</tr>");
                }

                strBuilder.Append("</table>");
                return strBuilder.ToString();
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        #endregion

        #region 5.Phần xuất batchfile tự động

        public async Task ProcessBatchFile()
        {
            try
            {
                _log.LogInformation("Start ProcessBatchFile Process");
                var register = await _balanceReportSvc.GetRegisterInfo(ReportRegisterType.BATCH_FILE);
                if (register != null && register.IsAuto && !string.IsNullOrEmpty(register.Providers))
                {
                    var date = DateTime.Now.Date.AddDays(-1);
                    await ExportFile(register, date);
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"ProcessBatchFile Exception: {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
            }
        }

        private async Task<bool> IsWriteFileTxt(string provider, string headerfiles, string fileName, List<ExportItemData> list)
        {
            try
            {
                using (StreamWriter file = new StreamWriter(fileName))
                {
                    _log.LogInformation($"ProviderCode= {provider} Ghi headerfiles");
                    file.WriteLine(headerfiles);
                    int count = 1;
                    _log.LogInformation($"ProviderCode= {provider} Ghi Danh sach SaleRequest Row: {list.Count()}");
                    foreach (var item in list)
                    {
                        var tmp = $"{count},{item.CreatedTime},{item.RequestRef},{item.AgentType},{item.AgentCode},{item.Providers},{item.Services},{item.Categories},{item.Products},{item.RequestAmount},{item.Discounts},{item.Fees},{item.TotalAmount},{item.Phonenumber},{item.Staff},{item.TransCodePay},{item.Channel},{item.IsRefund},{item.Quantity}";
                        file.WriteLine(tmp);
                        count++;
                    }
                    _log.LogInformation($"ProviderCode= {provider} Ghi Danh sach SaleRequest thanh cong !");
                    file.Close();
                    return true;
                }
            }
            catch (Exception ex)
            {
                _log.LogError(
                    $"{provider} Ghi Danh sach SaleRequest Exception {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
                return false;
            }
        }

        public async Task<MessageResponseBase> ExportFile(ReportRegisterInfo register, DateTime date)
        {
            try
            {
                var sales = new List<SaleRequestDto>();
                // List<CardBatchRequestDto> cardBatchs = null;
                List<StockTransRequestDto> cardBatchs = null;
                _log.LogInformation($"Function= GetSaleTopupRequest|Date= {date.ToString("yyyy-MM-dd HH:mm:ss")}");
                if (register.Extend == "2" || register.Extend == "1")
                {
                    var arrayDates = getArrayTimeDate(date.Date, register.Total);
                    var keyCode = "SaleTopupData_" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
                    if (register.Extend == "2")
                    {
                        foreach (var time in arrayDates)
                        {
                            var item = await getSaleTopupDataTime(keyCode, time);
                            sales.AddRange(item);
                        }
                    }
                    else if (register.Extend == "1")
                    {
                        Parallel.ForEach(arrayDates, time =>
                        {
                            var item = getSaleTopupDataTime(keyCode, time).Result;
                            sales.AddRange(item);
                        });
                    }
                }
                else if (register.Extend == "3")
                {
                    var saleItems = await _elasticReportService.getSaleReportItem(date.Date);
                    sales = (from x in saleItems
                             where (x.ServiceCode == ReportServiceCode.TOPUP
                             || x.ServiceCode == ReportServiceCode.TOPUP_DATA
                             || x.ServiceCode == ReportServiceCode.PAY_BILL
                             || x.ServiceCode == ReportServiceCode.PIN_CODE
                             || x.ServiceCode == ReportServiceCode.PIN_DATA
                             || x.ServiceCode == ReportServiceCode.PIN_GAME)
                             && x.TransType != ReportServiceCode.REFUND
                             select new SaleRequestDto
                             {
                                 PartnerCode = x.AccountCode,
                                 CategoryCode = x.CategoryCode,
                                 Channel = Channel.API,
                                 CreatedTime = x.CreatedTime,
                                 DiscountAmount = Convert.ToDecimal(x.Amount - x.TotalPrice),
                                 Fee = Convert.ToDecimal(x.Fee),
                                 ReceiverInfo = x.ReceivedAccount,
                                 ProductCode = x.ProductCode,
                                 Provider = x.ProvidersCode,
                                 Price = Convert.ToDecimal(x.Price),
                                 ProviderTransCode = x.ProviderTransCode,
                                 ServiceCode = x.ServiceCode,
                                 StaffAccount = x.AccountCode,
                                 PaymentAmount = Convert.ToDecimal(x.TotalPrice),
                                 TransRef = x.RequestRef,
                                 TransCode = x.TransCode,
                                 Status = x.Status == ReportStatus.Success ? SaleRequestStatus.Success : SaleRequestStatus.Failed,
                                 Quantity = x.Quantity,
                             }).ToList();
                }
                try
                {
                    var client = new JsonServiceClient(_apiUrl)
                    {
                        Timeout = TimeSpan.FromMinutes(5)
                    };
                    //var reponseCardBatch = await client.GetAsync<ResponseMesssageObject<string>>(new GetReportCardBatchRequest { Date = date });
                    ////var reponseCardBatch = await _grpcClient.GetClientCluster(GrpcServiceName.Backend).SendAsync<ResponseMesssageObject<string>>(new GetCardBatchRequest { Date = date });
                    //cardBatchs = reponseCardBatch.ResponseCode == ResponseCodeConst.Success
                    //   ? reponseCardBatch.Payload.FromJson<List<CardBatchRequestDto>>()
                    //   : new List<CardBatchRequestDto>();

                    var reponseCardBatch = await client.GetAsync<ResponseMesssageObject<string>>(new GetCardBatchSaleProviderRequest { Date = date });
                    cardBatchs = reponseCardBatch.ResponseCode == ResponseCodeConst.Success
                       ? reponseCardBatch.Payload.FromJson<List<StockTransRequestDto>>()
                       : new List<StockTransRequestDto>();
                }
                catch (Exception ex)
                {
                    _log.LogError($"Date= {date.ToString("yyyy-MM-dd HH:mm:ss")}|Function= GetCardBatchRequest|ExportFile_Backend_Exception:  {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
                    cardBatchs = new List<StockTransRequestDto>();
                }

                string headder = "STT,Created_Time,Transacion_Id,Agent_type,Agent_ID,Providers,Services,Categories,Products,Request_Amount,Discounts,Fees,Total_amount,Phonenumber,Staff,Order_Id,Channel,IsRefund,Quantity";

                var sourcePath = await _balanceReportSvc.GetForderCreate(ReportConst.Batch);

                var providers = register.Providers.Split(',', ';', '|');
                foreach (var provider in providers)
                {
                    var lis = sales.Where(c => c.Provider == provider);
                    var mslist = (from x in lis.OrderBy(c => c.CreatedTime)
                                  select new ExportItemData()
                                  {
                                      AgentCode = x.PartnerCode,
                                      AgentType = "AGENT",
                                      Categories = x.CategoryCode,
                                      Channel = x.Channel.ToString("G"),
                                      CreatedTime = _dateHepper.ConvertToUserTime(x.CreatedTime, DateTimeKind.Utc)
                                          .ToString("yyyyMMddHHmmss"),
                                      Discounts = Convert.ToDouble((x.DiscountAmount ?? 0).ToString()).ToString(),
                                      Fees = Convert.ToDouble((x.Fee ?? 0).ToString()).ToString(),
                                      Phonenumber = x.ReceiverInfo,
                                      Products = x.ProductCode,
                                      Providers = x.Provider,
                                      RequestAmount = Convert.ToDouble(x.Price.ToString()).ToString(),
                                      TransCodePay = x.ProviderTransCode,
                                      Services = x.ServiceCode,
                                      Staff = x.StaffAccount,
                                      TotalAmount = Convert.ToDouble(x.PaymentAmount.ToString()).ToString(),
                                      RequestRef = x.TransRef,
                                      TransCode = x.TransCode,
                                      IsRefund = (x.Status == SaleRequestStatus.Canceled || x.Status == SaleRequestStatus.Failed)
                                          ? "1"
                                          : "",
                                      Quantity = x.Quantity,
                                  }).ToList();

                    var lisBatch = cardBatchs.Where(c => c.Provider == provider);
                    var mslistBatch = (from x in lisBatch
                                       select new ExportItemData()
                                       {
                                           AgentCode = "",
                                           AgentType = "AGENT",
                                           Categories = x.CategoryCode,
                                           Channel = "API",
                                           CreatedTime = _dateHepper.ConvertToUserTime(x.CreatedDate, DateTimeKind.Utc)
                                               .ToString("yyyyMMddHHmmss"),
                                           Discounts = "0",
                                           Fees = "",
                                           Phonenumber = "",
                                           Products = x.ProductCode,
                                           Providers = x.Provider,
                                           RequestAmount = Convert.ToDouble(x.ItemValue.ToString()).ToString(),
                                           TransCodePay = x.TransCodeProvider,
                                           Services = x.ServiceCode,
                                           Staff = "",
                                           TotalAmount = x.TotalPrice.ToString(),
                                           RequestRef = x.TransCodeProvider,
                                           TransCode = x.TransCodeProvider,
                                           IsRefund = "",
                                           Quantity = x.Quantity
                                       }).ToList();

                    mslist.AddRange(mslistBatch);
                    string fileName = $"SALE.NHATTRAN_{provider}.{date.ToString("yyyyMMdd")}.txt";
                    var pathSave = $"{sourcePath.PathName}/{fileName}";
                    await IsWriteFileTxt(provider, headder, pathSave, mslist);
                }

                Thread.Sleep(5000);
                var sendFile = await _balanceReportSvc.ZipForderCreate(sourcePath);
                var listMail = register.EmailSend.Split(',', ';').ToList();
                string tille = register.Name + " ngày " + date.ToString("dd-MM-yyyy");
                _emailSender.SendEmailReportAuto(listMail, tille, tille, sendFile);

                return new MessageResponseBase()
                {
                    ProviderCode = ResponseCodeConst.Success,
                    ResponseMessage = "Đã xuất file"
                };
            }
            catch (Exception ex)
            {
                _log.LogError($"ExportFile Exception: {ex.Message}|{ex.StackTrace}|{ex.InnerException}");
                return new MessageResponseBase()
                {
                    ProviderCode = ResponseCodeConst.Error,
                    ResponseMessage = "Lỗi xuất file"
                };
            }
        }

        #endregion

        #region 6.Phần Đồng bộ mã thanh toán thiếu

        #endregion

        #region 7.Báo cáo tự động tổng hợp

        public async Task ProcessCompareAgentPartner()
        {
            try
            {
                _log.LogInformation("Start SysCompareAgentPartner Process");
                var register = await _balanceReportSvc.GetRegisterInfo(ReportRegisterType.ComparePartner);

                //_log.LogInformation($"SysCompareAgentPartner_Info {(register != null ? register.ToJson() : "")}");
                if (register != null && register.IsAuto)
                {
                    //1.Goi api lay danh sach Agent ve
                    //2.xử lý phần cấu hình đã đến ngày gửi báo cáo chưa
                    //3.Đủ điều kiện là  ngày hôm nay thì gửi mail
                    //4.Kiểm tra danh sách mail
                    //5.Tính khoảng thời gian để lọc dữ liệu               
                    var userPeriods = await _externalService.GetUserPeriodAsync("", AgentType.AgentApi);
                    var dateNow = DateTime.Now;

                    int so_ngay = 0;
                    if (dateNow.Day == 1)
                        so_ngay = dateNow.AddDays(-1).Day;
                    else
                    {
                        if (dateNow.Month < 12)
                            so_ngay = new DateTime(dateNow.Year, dateNow.Month + 1, 1).AddDays(-1).Day;
                        else
                            so_ngay = new DateTime(dateNow.Year + 1, 1, 1).AddDays(-1).Day;
                    }

                    List<ReportSendMailAgentApi> lstSendApi = new List<ReportSendMailAgentApi>();
                    foreach (var account in userPeriods)
                    {
                        if (string.IsNullOrEmpty(account.EmailReceives) && string.IsNullOrEmpty(account.FolderFtp))
                            continue;

                        var toDate = dateNow.Date.AddDays(-1);
                        var period = account.Period;
                        if (period == 0) continue;
                        var day = CheckFromDateSearch(period, so_ngay, toDate.Day);
                        if (day == 0) continue;
                        var fromDate = new DateTime(toDate.Year, toDate.Month, day);
                        _log.LogInformation($"{account.AgentCode} SysCompareAgentPartner_Start:  AgentCode|Period|Day|So_Ngay => {account.AgentCode}|{period}|{day}|{so_ngay}");
                        var balanceHistory = await _elasticReportService.GetReportBalanceHistory(fromDate, toDate, account.AgentCode);
                        var checkBalance = await _elasticReportService.GetBalanceAgent(account.AgentCode, fromDate, toDate);
                        var checkHistory = balanceHistory.Count > 0 ? balanceHistory.First() : null;

                        var checkSend = new ReportSendMailAgentApi() { IsSend = false, AgentInfo = account.AgentCode + "-" + account.FullName };

                        if (checkHistory == null)
                            checkSend.Description = "Không check được lịch sử số dư";
                        else if (checkBalance == null)
                            checkSend.Description = "Không check được số dư báo cáo NXT";
                        else if (checkHistory.BalanceAfter != checkBalance.BalanceAfter)
                            checkSend.Description = $"Số dư sau giao dịch của NXT khác lịch sử số dư {checkBalance.BalanceAfter} - {checkHistory.BalanceAfter}";
                        else if (checkHistory.BalanceBefore != checkBalance.BalanceBefore)
                            checkSend.Description = $"Số dư trước giao dịch của NXT khác lịch sử số dư {checkBalance.BalanceBefore} - {checkHistory.BalanceBefore}";
                        bool isWarning = !string.IsNullOrEmpty(checkSend.Description);

                        var reponseData = await SendMailComparePartner(account, fromDate, toDate, period.ToString(), day, isWarning: isWarning);
                        if (string.IsNullOrEmpty(checkSend.Description))
                        {
                            if (!reponseData.ISend)
                                checkSend.Description = reponseData.Content;

                            checkSend.IsSend = reponseData.ISend;
                        }

                        lstSendApi.Add(checkSend);
                    }

                    var dataTable = FillBodyByTableSendMailAgentApi(lstSendApi);
                    var listMail = register.EmailSend.Split(',', ';').ToList();
                    string fileName = $"{register.Content}_{DateTime.Now.Date:ddMMyyyy}";
                    _emailSender.SendEmailReportAuto(listMail, fileName.Split('.')[0], dataTable);
                }
            }
            catch (Exception ex)
            {
                _log.LogInformation($"ProcessCompareAgentPartner_Exception: {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
            }
        }

        public async Task<NotCompleteWarning> SendMailComparePartner(UserInfoPeriodDto account, DateTime fromDate, DateTime toDate,
            string period, int day, bool isAuto = true, string email = "", bool sendAlertSuccess = true, bool isWarning = false)
        {
            var sendMail = false;
            bool checkIsBefore = true;
            var exMessage = "";
            var dtoReponse = new NotCompleteWarning()
            {
                AgentCode = account.AgentCode,
                AgentName = account.FullName,
                Complete = false,
                ISend = false
            };
            List<ReportBalancePartnerDto> datas = new List<ReportBalancePartnerDto>();
            try
            {
                #region *****Xu ly thong tin cua 1 Agent

                var input = new ReportComparePartnerRequest()
                {
                    FromDate = fromDate,
                    ToDate = toDate,
                    Offset = 0,
                    Limit = int.MaxValue,
                    AgentCode = account.AgentCode,
                    Type = "SENDMAIL"
                };

                _log.LogInformation($"{account.AgentCode} SendMailComparePartner Step_1_DoiSoat :  AgentCode|Period|Day|Auto => {account.AgentCode}|{period}|{day}|{isAuto}");

                var list = _searchElastich
                    ? await _elasticReportService.ReportComparePartnerGetList(input)
                    : await _balanceReportSvc.ReportComparePartnerGetList(input);
                input.Type = "BALANCE";
                var listBalance = _searchElastich
                    ? await _elasticReportService.ReportComparePartnerGetList(input)
                    : await _balanceReportSvc.ReportComparePartnerGetList(input);

                var fromDateFile = DateTime.Now;


                var lst = list.Payload.ConvertTo<List<ReportComparePartnerDto>>();
                var lstBalance = listBalance.Payload.ConvertTo<List<ReportBalancePartnerDto>>();
                datas = lstBalance;
                var unknowtrans = lstBalance.Where(p => p.Index == 6).FirstOrDefault();


                var tang = lstBalance.Where(p => new[] { 1, 2, 3, 5, 8 }.Contains(p.Index)).Sum(p => p.Value);
                var giam = lstBalance.Where(p => new[] { 4, 6, 7, 9, 10 }.Contains(p.Index)).Sum(p => p.Value);
                if (isAuto)
                {
                    if (tang - giam != 0)
                    {
                        _log.LogInformation($"{account.AgentCode} SendMailComparePartner - {account.FullName} -{period} - Đối soát lệch");

                        _ = Task.Run(async () =>
                        {
                            string title = "Cảnh báo đối soát lệch khi gửi đối soát";
                            string msg = $"Đại lý {account.AgentCode} - {account.FullName} đối soát lệch.\n " +
                                $"\nSố tiền lệch : {Math.Abs(tang - giam)}." +
                                $"\nChi tiết đối soát:";
                            foreach (var item in lstBalance)
                            {
                                msg += $"\n{item.Name}: {item.Value}";
                            }
                            try
                            {
                                await _bus.Publish<SendBotMessage>(new
                                {
                                    BotType = BotType.Sale,
                                    Module = "Report",
                                    Title = title,
                                    Message = msg,
                                    MessageType = BotMessageType.Wraning,
                                    ChatId = _chatId
                                });

                            }
                            catch (Exception e)
                            {
                                _log.LogError(e, $"{account.AgentCode} SendMailComparePartner ==> Send Noti  ex : {e.Message}");
                            }

                            dtoReponse.Content = msg;
                        });
                        checkIsBefore = false;
                        //  return dtoReponse;

                    }
                }
                //Danh sách lệch 
                //nhannv: Nếu kết quả đối soát chưa có kết quả 
                if (isAuto)
                {
                    if (unknowtrans != null && unknowtrans.Value > 0)
                    {
                        _log.LogInformation($"{account.AgentCode} SendMailComparePartner - {account.FullName} -{period} - Tồn tại GD chưa rõ kết quả");
                        //send cảnh báo
                        _ = Task.Run(async () =>
                          {
                              string title = "Cảnh báo tồn tại giao dịch chưa rõ kết quả khi gửi đối soát";
                              string msg = $"Đại lý {account.AgentCode} - {account.FullName} tồn tại giao dịch chưa rõ kết quả khi gửi đối soát.\n " +
                                  $"\nVui lòng kiểm tra cập nhật trạng thái giao dịch.\n" +
                                  $" Danh sách giao dịch chưa rõ kết quả";

                              foreach (var transCode in unknowtrans.TransCodes)
                              {
                                  msg = msg + $"\n {transCode}";
                              }

                              try
                              {
                                  await _bus.Publish<SendBotMessage>(new
                                  {
                                      BotType = BotType.Sale,
                                      Module = "Report",
                                      Title = title,
                                      Message = msg,
                                      MessageType = BotMessageType.Message,
                                      ChatId = _chatId
                                  });

                              }
                              catch (Exception e)
                              {
                                  _log.LogError(e, $"{account.AgentCode} SendMailComparePartner ==> Send Noti  ex : {e.Message}");
                              }

                              dtoReponse.Content = msg;
                          });

                        checkIsBefore = false;
                        // return dtoReponse;
                    }
                }

                var sourcePath = await _balanceReportSvc.GetForderCreate(ReportConst.Provider);

                string fileName = $"{account.AgentCode}_DoiSoat_{fromDateFile.ToString("ddMMyyyyHHmmssfff")}.xlsx";
                string content = isAuto ? $":Đối soát thanh toán chu kỳ {period} tháng {fromDate.Month} năm {fromDate.Year} " : "";
                await SendMailCompareAgentPartner(input.FromDate, input.ToDate, isAuto, input.AgentCode,
                    fileName, lst, lstBalance, account, sourcePath, content, fptPartnerCode: account.FolderFtp);

                _log.LogInformation($"{account.AgentCode} SendMailComparePartner Step_2_NapTien :  AgentCode|Period|Day => {account.AgentCode}|{period}|{day}");

                string fileNameDepotit = $"{account.AgentCode}_Deposit_{fromDateFile:ddMMyyyyHHmmssfff}.xlsx";
                await SendDepositAgentPartner(input.FromDate, input.ToDate, isAuto, input.AgentCode, sourcePath, fileNameDepotit, fptParentCode: account.FolderFtp);

                _log.LogInformation($"{account.AgentCode} SendMailComparePartner Step_3_BanHang :  AgentCode|Period|Day => {account.AgentCode}|{period}|{day}");

                string fileNameSale = $"{account.AgentCode}_Sale_Full_{fromDateFile.ToString("ddMMyyyyHHmmssfff")}.csv";
                var lit = await SendSaleAgentPartner(input.FromDate, input.ToDate, input.AgentCode, sourcePath, fileNameSale);

                var litPinCode = lit.Where(c => c.ServiceCode == ReportServiceCode.PIN_CODE).ToList();
                var litPinGame = lit.Where(c => c.ServiceCode == ReportServiceCode.PIN_GAME).ToList();
                //var litTopupPrepa = lit.Where(c => c.ServiceCode == ReportServiceCode.TOPUP && c.ReceiverTypeNote == ReceiverType.PrePaid).ToList();
                //var litTopupPostPaid = lit.Where(c => c.ServiceCode == ReportServiceCode.TOPUP && c.ReceiverTypeNote == ReceiverType.PostPaid).ToList();
                var litTopup = lit.Where(c => c.ServiceCode == ReportServiceCode.TOPUP).ToList();
                var litData = lit.Where(c => c.ServiceCode == ReportServiceCode.PIN_DATA || c.ServiceCode == ReportServiceCode.TOPUP_DATA).ToList();
                var litPayBill = lit.Where(c => c.ServiceCode == ReportServiceCode.PAY_BILL).ToList();

                var listMail = isAuto ? (account.EmailReceives ?? "").Split(',', ';').ToList() : email.Split(',', ';').ToList();
                _log.LogInformation($"{account.AgentCode} SendMailComparePartner Step_4_File :  AgentCode|Period|Day => {account.AgentCode}|{period}|{day}");

                if (litPinCode.Count > 0)
                {
                    string fileNamePinCode = $"{account.AgentCode}_PinCode_{fromDateFile.ToString("ddMMyyyyHHmmssfff")}.csv";
                    var pathSavePinCode = $"{sourcePath.PathName}/{fileNamePinCode}";
                    await _balanceReportSvc.ExportFileSaleByPartner(litPinCode, pathSavePinCode);
                }

                if (litPinGame.Count > 0)
                {
                    string fileNameGame = $"{account.AgentCode}_PinGame_{fromDateFile.ToString("ddMMyyyyHHmmssfff")}.csv";
                    var pathSavePinGame = $"{sourcePath.PathName}/{fileNameGame}";
                    await _balanceReportSvc.ExportFileSaleByPartner(litPinGame, pathSavePinGame);
                }

                //if (litTopupPrepa.Count > 0)
                //{
                //    string fileNamePrepaId = $"{account.AgentCode}_TopupTraTruoc_{fromDateFile.ToString("ddMMyyyyHHmmssfff")}.csv";
                //    var pathSavePrepaId = $"{sourcePath.PathName}/{fileNamePrepaId}";
                //    await _balanceReportSvc.ExportFileSaleByPartner(litTopupPrepa, pathSavePrepaId);
                //}

                //if (litTopupPostPaid.Count > 0)
                //{
                //    string fileNamePostPaid = $"{account.AgentCode}_TopupTrasau_{fromDateFile.ToString("ddMMyyyyHHmmssfff")}.csv";
                //    var pathSavePostPaid = $"{sourcePath.PathName}/{fileNamePostPaid}";
                //    await _balanceReportSvc.ExportFileSaleByPartner(litTopupPostPaid, pathSavePostPaid);
                //}

                if (litTopup.Count > 0)
                {
                    string fileNameTopup = $"{account.AgentCode}_Topup_{fromDateFile.ToString("ddMMyyyyHHmmssfff")}.csv";
                    var pathSaveTopup = $"{sourcePath.PathName}/{fileNameTopup}";
                    await _balanceReportSvc.ExportFileSaleByPartner(litTopup, pathSaveTopup);
                }

                if (litData.Count > 0)
                {
                    string fileNameData = $"{account.AgentCode}_Data_{fromDateFile.ToString("ddMMyyyyHHmmssfff")}.csv";
                    var pathSaveData = $"{sourcePath.PathName}/{fileNameData}";
                    await _balanceReportSvc.ExportFileSaleByPartner(litData, pathSaveData);
                }


                if (litPayBill.Count > 0)
                {
                    string fileNamePayBill = $"{account.AgentCode}_HoaDon_{fromDateFile.ToString("ddMMyyyyHHmmssfff")}.csv";
                    var pathSavePayBill = $"{sourcePath.PathName}/{fileNamePayBill}";
                    await _balanceReportSvc.ExportFileSaleByPartner(litPayBill, pathSavePayBill);
                }

                string tileMail = $"Gtel Mobile - {account.FullName} Đối soát dịch vụ Topup từ ngày {fromDate.ToString("dd/MM/yyyy")} tới ngày {toDate.ToString("dd/MM/yyyy")}";

                if (!string.IsNullOrEmpty(account.FolderFtp) && isAuto)
                {
                    string fileNameFpt = account.AgentCode + "_Sale_" + input.FromDate.ToString("yyyyMMdd") + "000000_" + input.ToDate.ToString("yyyyMMdd") + "235959.csv";
                    await PushFtpPartnerCsv(sourcePath.PathName + "/" + fileNameSale, account.FolderFtp, fileNameFpt);
                }

                var body = await FillBodyByTableCompare(account.AgentCode, account.FullName,
                    fromDate.ToString("dd/MM/yyyy"), toDate.ToString("dd/MM/yyyy"), lstBalance);

                var sendFile = await _balanceReportSvc.ZipForderCreate(sourcePath);

                if (checkIsBefore && !isWarning)
                    sendMail = _emailSender.SendEmailReportAuto(listMail, tileMail, body, sendFile);

                if ((isWarning || !checkIsBefore) && isAuto)
                {
                    var registerComplete = await _balanceReportSvc.GetRegisterInfo(ReportRegisterType.CompareNotComplete);
                    if (registerComplete != null && registerComplete.IsAuto)
                    {
                        var listMailComplete = (registerComplete.EmailSend ?? "").Split(',', ';').ToList();
                        _emailSender.SendEmailReportAuto(listMailComplete, tileMail, body, sendFile);
                    }
                }

                if (isAuto) Thread.Sleep(2000);

                #endregion
            }
            catch (Exception ex)
            {
                _log.LogInformation(
                    $"{account.AgentCode} SendMailComparePartner Period:{period} Exception: {ex.Message}|{ex.InnerException}|{ex.Source}");
                exMessage = ex.Message;
                sendMail = false;
            }
            if (checkIsBefore)
            {
                if (sendMail == false)
                {
                    _log.LogInformation($"{account.AgentCode} SendMailComparePartner - {account.FullName} -{period} - Gửi email thất bại");
                    _ = Task.Run(async () =>
                      {
                          string title = "Gửi mail đối soát thất bại";
                          string msg = $"Gửi mail đối soát cho Đại lý {account.AgentCode}  {account.FullName} không thành công.\n" +
                              $"Lỗi: {exMessage} \n";
                          try
                          {
                              await _bus.Publish<SendBotMessage>(new
                              {
                                  BotType = BotType.Sale,
                                  Module = "Report",
                                  Title = title,
                                  Message = msg,
                                  MessageType = BotMessageType.Wraning,
                                  ChatId = _chatId
                              });
                          }
                          catch (Exception e)
                          {
                              _log.LogError(e, $"{account.AgentCode} SendMailComparePartner ==> Send Noti  ex : {e.Message}");
                          }

                          dtoReponse.Content = msg;
                      });
                }
                else
                {

                    Task.Run(async () =>
                    {
                        string title = isAuto == true ? "Gửi mail đối soát tự động Thành công" : "Gửi mail đối soát thủ công Thành công";
                        string msg = $"Gửi mail đối soát cho Đại lý {account.AgentCode}  {account.FullName} thành công."
                        + $"\nChi tiết đối soát:";
                        foreach (var item in datas)
                        {
                            msg += $"\n{item.Name}: {item.Value}";
                        }
                        try
                        {
                            await _bus.Publish<SendBotMessage>(new
                            {
                                BotType = BotType.Sale,
                                Module = "Report",
                                Title = title,
                                Message = msg,
                                MessageType = BotMessageType.Message,
                                ChatId = _chatId
                            });
                        }
                        catch (Exception e)
                        {
                            _log.LogError(e, $"{account.AgentCode} SendMailComparePartner ==> Send Noti  ex : {e.Message}");
                        }
                        dtoReponse.Content = msg;
                        dtoReponse.ISend = true;
                        dtoReponse.Complete = true;
                    }).Wait();
                }
            }
            return dtoReponse;
        }

        private async Task<bool> SendMailCompareAgentPartner(DateTime fromDate, DateTime toDate, bool isAuto,
            string agentCode, string fileName,
            List<ReportComparePartnerDto> lst,
            List<ReportBalancePartnerDto> lstBalance,
            UserInfoPeriodDto userPeriod,
            ReportFile sourcePath,
            string compareText = "", string fptPartnerCode = "")
        {
            try
            {
                #region *****Xu ly thong tin cua 1 Agent

                var partnerInput = new ReportComparePartnerExportInfo()
                {
                    Title = "BIÊN BẢN ĐỐI SOÁT DỊCH VỤ TOPUP",
                    PeriodCompare = string.Format("Thời gian: Từ ngày {0} đến ngày {1}",
                        fromDate.ToString("dd/MM/yyyy"), toDate.ToString("dd/MM/yyyy")),
                    Contract = $"Căn cứ hợp đồng số {userPeriod.ContractNumber} ký ngày {(userPeriod.SigDate != null ? userPeriod.SigDate?.ToString("dd/MM/yyyy") : "")} giữa công ty CP Viễn thông Di động Toàn Cầu và {userPeriod.FullName}",
                    Provider = $"{agentCode}",
                    PeriodPayment = $"CP Viễn thông Di động Toàn Cầu và {fileName}",
                    PinCodeItems = lst.Where(p => p.ServiceCode == ReportServiceCode.PIN_CODE).ToList(),
                    PinGameItems = lst.Where(p => p.ServiceCode == ReportServiceCode.PIN_GAME).ToList(),
                    TopupItems = lst.Where(p => p.ServiceCode == ReportServiceCode.TOPUP).ToList(),
                    TopupPostpaIdItems = lst.Where(p => p.ServiceCode == ReportServiceCode.TOPUP && p.ReceiverType == ReceiverType.PostPaid).ToList(),
                    TopupPrepaIdItems = lst.Where(p => p.ServiceCode == ReportServiceCode.TOPUP && p.ReceiverType == ReceiverType.PrePaid).ToList(),
                    DataItems = lst.Where(p => p.ServiceCode == ReportServiceCode.TOPUP_DATA || p.ServiceCode == ReportServiceCode.PIN_DATA).ToList(),
                    PayBillItems = lst.Where(p => p.ServiceCode == ReportServiceCode.PAY_BILL).ToList(),
                    BalanceItems = lstBalance,
                    TotalRowsBalance = 0,
                    TotalFeePartner = "0",
                    TotalFeePartnerChu = "0",
                    TotalRowsPayBill = 0,
                    TotalRowsPinCode = 0,
                    TotalRowsTopup = 0,
                    IsAccountApi = userPeriod.AgentType == 2,
                    IsAuto = !string.IsNullOrEmpty(compareText),
                    FullName = userPeriod.FullName,
                };

                partnerInput.SumPinCodes = new ReportComparePartnerDto()
                {
                    Fee = partnerInput.PinCodeItems.Sum(c => c.Fee),
                    Value = partnerInput.PinCodeItems.Sum(c => c.Value),
                    Discount = partnerInput.PinCodeItems.Sum(c => c.Discount),
                    Price = partnerInput.PinCodeItems.Sum(c => c.Price),
                    Quantity = partnerInput.PinCodeItems.Sum(c => c.Quantity),
                };

                partnerInput.SumPinGames = new ReportComparePartnerDto()
                {
                    Fee = partnerInput.PinGameItems.Sum(c => c.Fee),
                    Value = partnerInput.PinGameItems.Sum(c => c.Value),
                    Discount = partnerInput.PinGameItems.Sum(c => c.Discount),
                    Price = partnerInput.PinGameItems.Sum(c => c.Price),
                    Quantity = partnerInput.PinGameItems.Sum(c => c.Quantity),
                };

                partnerInput.SumTopup = new ReportComparePartnerDto()
                {
                    Fee = partnerInput.TopupItems.Sum(c => c.Fee),
                    Value = partnerInput.TopupItems.Sum(c => c.Value),
                    Discount = partnerInput.TopupItems.Sum(c => c.Discount),
                    Price = partnerInput.TopupItems.Sum(c => c.Price),
                    Quantity = partnerInput.TopupItems.Sum(c => c.Quantity),
                };

                //partnerInput.SumTopupPostpaId = new ReportComparePartnerDto()
                //{
                //    Fee = partnerInput.TopupPostpaIdItems.Sum(c => c.Fee),
                //    Value = partnerInput.TopupPostpaIdItems.Sum(c => c.Value),
                //    Discount = partnerInput.TopupPostpaIdItems.Sum(c => c.Discount),
                //    Price = partnerInput.TopupPostpaIdItems.Sum(c => c.Price),
                //    Quantity = partnerInput.TopupPostpaIdItems.Sum(c => c.Quantity),
                //};

                //partnerInput.SumTopupPrepaId = new ReportComparePartnerDto()
                //{
                //    Fee = partnerInput.TopupPrepaIdItems.Sum(c => c.Fee),
                //    Value = partnerInput.TopupPrepaIdItems.Sum(c => c.Value),
                //    Discount = partnerInput.TopupPrepaIdItems.Sum(c => c.Discount),
                //    Price = partnerInput.TopupPrepaIdItems.Sum(c => c.Price),
                //    Quantity = partnerInput.TopupPrepaIdItems.Sum(c => c.Quantity),
                //};

                partnerInput.SumData = new ReportComparePartnerDto()
                {
                    Fee = partnerInput.DataItems.Sum(c => c.Fee),
                    Value = partnerInput.DataItems.Sum(c => c.Value),
                    Discount = partnerInput.DataItems.Sum(c => c.Discount),
                    Price = partnerInput.DataItems.Sum(c => c.Price),
                    Quantity = partnerInput.DataItems.Sum(c => c.Quantity),
                };

                partnerInput.SumPayBill = new ReportComparePartnerDto()
                {
                    Fee = partnerInput.PayBillItems.Sum(c => c.Fee),
                    Value = partnerInput.PayBillItems.Sum(c => c.Value),
                    Discount = partnerInput.PayBillItems.Sum(c => c.Discount),
                    Price = partnerInput.PayBillItems.Sum(c => c.Price),
                    Quantity = partnerInput.PayBillItems.Sum(c => c.Quantity),
                };


                partnerInput.TotalRowsTopup = partnerInput.TopupItems.Count();
                //partnerInput.TotalRowsTopupPostpaId = partnerInput.TopupPostpaIdItems.Count();
                //partnerInput.TotalRowsTopupPrepaId = partnerInput.TopupPrepaIdItems.Count();
                partnerInput.TotalRowsPinCode = partnerInput.PinCodeItems.Count();
                partnerInput.TotalRowsPinGame = partnerInput.PinGameItems.Count();
                partnerInput.TotalRowsData = partnerInput.DataItems.Count();
                partnerInput.TotalRowsPayBill = partnerInput.PayBillItems.Count();
                partnerInput.TotalRowsBalance = partnerInput.BalanceItems.Count();

                _log.LogInformation($"{agentCode} Input_SendMailCompareAgentPartner_True");
                _log.LogInformation($"{agentCode} SendMailCompareAgentPartner_Total PinCode|Topup|PayBll : {partnerInput.PinCodeItems.Count()}|{partnerInput.TopupItems.Count()}|{partnerInput.PayBillItems.Count()}");
                var excel = _exportData.ReportCompareParnerExportToFile(partnerInput, fileName);
                int index = 5;
                bool isbreak = false;
                while (isbreak == false)
                {
                    index = index - 1;
                    Thread.Sleep(1000);
                    _log.LogInformation($"{agentCode} SendMailCompareAgentPartner_Index: {index}");
                    excel = _exportData.ReportCompareParnerExportToFile(partnerInput, fileName);
                    _log.LogInformation($"{agentCode} SendMailCompareAgentPartner_excel: {(excel == null ? "Data" : "null")}");
                    if (excel != null)
                    {
                        _log.LogInformation($"{agentCode} SendMailCompareAgentPartner_FileToken {index}: {(!string.IsNullOrEmpty(excel.FileToken) ? "FileToken_Data" : "null")}");
                        var fileBytes = await _cacheManager.GetFile(excel.FileToken);
                        _log.LogInformation($"{agentCode} SendMailCompareAgentPartner_FileBytes {index}: {(fileBytes != null ? "FileBytes_Data" : "null")}");
                        if (fileBytes != null)
                        {
                            var pathSave = $"{sourcePath.PathName}/{fileName}";
                            File.WriteAllBytesAsync(pathSave, fileBytes).Wait();
                            if (!string.IsNullOrEmpty(fptPartnerCode) && isAuto)
                            {
                                string fileNameFpt = agentCode + "_Balance_" + fromDate.ToString("yyyyMMdd") + "000000_" + toDate.ToString("yyyyMMdd") + "235959.xlsx";
                                await PushFtpPartnerXlsx(fileBytes, fptPartnerCode, fileNameFpt);
                            }
                            break;
                        }
                    }

                    if (index <= -1) break;
                }

                _log.LogInformation($"{agentCode} SendMailCompareAgentPartner_True");
                return true;

                #endregion
            }
            catch (Exception ex)
            {
                _log.LogInformation(
                    $"{agentCode}_SendMailCompareAgentPartner Exception: {ex.Message}|{ex.InnerException}|{ex.Source}");
                return false;
            }
        }

        private async Task<bool> SendDepositAgentPartner(DateTime fromDate, DateTime toDate, bool isAuto,
            string agentCode, ReportFile sourcePath, string fileName, string fptParentCode = "")
        {
            try
            {
                _log.LogInformation($"{agentCode} Start_SendDepositAgentPartner Process");
                var list = _searchElastich
                    ? await _elasticReportService.ReportDepositDetailGetList(new ReportDepositDetailRequest()
                    {
                        AgentCode = agentCode,
                        LoginCode = agentCode,
                        FromDate = fromDate,
                        ToDate = toDate,
                        Offset = 0,
                        Limit = int.MaxValue,
                    })
                    : await _balanceReportSvc.ReportDepositDetailGetList(new ReportDepositDetailRequest()
                    {
                        AgentCode = agentCode,
                        LoginCode = agentCode,
                        FromDate = fromDate,
                        ToDate = toDate,
                        Offset = 0,
                        Limit = int.MaxValue,
                    });

                var lit = list.Payload.ConvertTo<List<ReportDepositDetailDto>>();
                _log.LogInformation($"{agentCode} SendDepositAgentPartner_Total : {lit.Count()}");
                var excel = _exportData.ReportDepositToFile(lit, fileName);
                int index = 5;
                bool isbreak = false;
                while (isbreak == false)
                {
                    index = index - 1;
                    Thread.Sleep(1000);
                    _log.LogInformation($"{agentCode} SendDepositAgentPartner_Next: {index}");
                    excel = _exportData.ReportDepositToFile(lit, fileName);

                    _log.LogInformation($"{agentCode} SendDepositAgentPartner_True {index}: {(excel != null ? "Data" : "Null")}");
                    if (excel != null)
                    {
                        _log.LogInformation($"{agentCode} SendDepositAgentPartner_FileToken {index}: {(!string.IsNullOrEmpty(excel.FileToken) ? "FileToken_Data" : "null")}");
                        var fileBytes = await _cacheManager.GetFile(excel.FileToken);
                        _log.LogInformation($"{agentCode} SendDepositAgentPartner_FileBytes {index}: {(fileBytes != null ? "FileBytes_Data" : "null")}");
                        if (fileBytes != null)
                        {
                            var pathSave = $"{sourcePath.PathName}/{fileName}";
                            await File.WriteAllBytesAsync(pathSave, fileBytes);
                            if (!string.IsNullOrEmpty(fptParentCode) && isAuto)
                            {
                                string fileNameFpt = agentCode + "_Deposit_" + fromDate.ToString("yyyyMMdd") + "000000_" + toDate.ToString("yyyyMMdd") + "235959.xlsx";
                                await PushFtpPartnerXlsx(fileBytes, fptParentCode, fileNameFpt);
                            }
                            break;
                        }
                    }

                    if (index <= -1) break;
                }


                _log.LogInformation($"{agentCode} SendDepositAgentPartner_True");
                return true;
            }
            catch (Exception ex)
            {
                _log.LogError($"{agentCode} SendDepositAgentPartner Exception: {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
                return false;
            }
        }

        private async Task<List<ReportServiceDetailDto>> SendSaleAgentPartner(DateTime fromDate, DateTime toDate,
            string agentCode, ReportFile sourcePath, string fileName)
        {
            try
            {
                _log.LogInformation($"{agentCode} Start_SendSaleAgentPartner Process");
                var list = _searchElastich
                    ? await _elasticReportService.ReportServiceDetailGetList(new ReportServiceDetailRequest()
                    {
                        AgentCode = agentCode,
                        FromDate = fromDate,
                        ToDate = toDate,
                        Offset = 0,
                        Limit = int.MaxValue,
                    })
                    : await _balanceReportSvc.ReportServiceDetailGetList(new ReportServiceDetailRequest()
                    {
                        AgentCode = agentCode,
                        FromDate = fromDate,
                        ToDate = toDate,
                        Offset = 0,
                        Limit = int.MaxValue,
                    });

                var lit = list.Payload.ConvertTo<List<ReportServiceDetailDto>>();
                _log.LogInformation($"{agentCode} SendSaleAgentPartner_Total : {lit.Count()}");
                var pathSave = $"{sourcePath.PathName}/{fileName}";
                await _balanceReportSvc.ExportFileSaleByPartner(lit, pathSave);
                _log.LogInformation($"{agentCode} SendSaleAgentPartner_True");

                return lit;
            }
            catch (Exception ex)
            {
                _log.LogError($"{agentCode} SendSaleAgentPartner_False Exception: {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
                return new List<ReportServiceDetailDto>();
            }
        }

        public async Task<UserInfoPeriodDto> GetUserExportingPeriodAsync(string agentCode)
        {
            try
            {
                _log.LogInformation($"GetUserExportingPeriodAsync request:{agentCode}");
                List<UserInfoPeriodDto> reponse =
                    await _externalService.GetUserPeriodAsync(agentCode, AgentType.Default);
                _log.LogInformation($"GetUserExportingPeriodAsync response:{reponse.ToJson()}");
                return reponse.FirstOrDefault();
            }
            catch (Exception e)
            {
                _log.LogError($"GetUserExportingPeriodAsync error: {e}");
                return null;
            }
        }

        private async Task<string> FillBodyByTableCompare(string agentCode, string agentName, string fromDate,
            string toDate, List<ReportBalancePartnerDto> listResult)
        {
            try
            {
                StringBuilder strBuilder = new StringBuilder();
                strBuilder.Append($"Kính gửi {agentName},<br/>");
                strBuilder.Append($"GTEL MOBILE xin gửi Quý đối tác biên bản đối soát dịch vụ Topup từ ngày {fromDate} tới ngày {toDate}. ( Chi tiết xem trong file đính kèm).<br/>");
                strBuilder.Append($"Dưới đây là số liệu tổng hợp: <br/><br/>");
                strBuilder.Append("<div class='col-xl-12'>");
                strBuilder.Append("<table cellpadding='1' cellspacing='1' border='1' class='table-bordered table-hover dataTable' cellspacing='1' cellpadding='1' align='Left' rules='all' style='border-width:0px;width:100%;margin-bottom: 0px'><tr>");
                strBuilder.Append("<th class='align_center' scope='col'>STT</th>");
                strBuilder.Append($"<th class='align_center' scope='col'>Nội dung</th>");
                strBuilder.Append($"<th class='align_center' scope='col'>Số tiền</th>");
                strBuilder.Append("</tr>");
                foreach (var rpt in listResult)
                {
                    strBuilder.Append($"<tr><td class='align_left'><b>{rpt.Index}</b></td>");
                    strBuilder.Append($"<td class='align_right'>{rpt.Name}</td>");
                    strBuilder.Append($"<td class='align_right'>{Convert.ToDouble(rpt.Value):N0}</td>");
                    strBuilder.Append("</tr>");
                }

                strBuilder.Append("</table></div>");
                strBuilder.Append("<br/><br/>");
                strBuilder.Append("Ghi chú: 10=1+2+3-4+5-6-7+8-9<br/>");
                strBuilder.Append("Quý đối tác vui lòng kiểm tra và phản hồi giúp Gtel Mobile.<br/>");
                strBuilder.Append("Trân trọng./.<br/>");
                return strBuilder.ToString();
            }
            catch (Exception ex)
            {
                _log.LogInformation(
                    $"{agentCode} FillBodyByTableCompare Exception: {ex.Message}|{ex.InnerException}|{ex.Source}");
                return string.Empty;
            }
        }

        private int CheckFromDateSearch(int period, int so_ngay, int day)
        {
            int p = day / period;
            int fromDay = 0;
            if (p * period == day)
                fromDay = day - period + 1;
            else
            {
                if (so_ngay == day)
                    fromDay = day - (so_ngay - p * period) + 1;
                else
                    fromDay = 0;
            }

            return fromDay;
        }

        #endregion

        #region 8.Cảnh báo số dư

        public async Task ProcessWarning()
        {
            try
            {
                _log.LogInformation("Start ProcessWarning");

                #region 1.Số liệu báo cáo tổng hợp bị lệch số liệu

                var registerAgent = await _balanceReportSvc.GetRegisterInfo(ReportRegisterType.AgentBalance);

                if (registerAgent != null && registerAgent.IsAuto)
                {
                    var date = DateTime.Now.Date.AddDays(-1);
                    if (_searchElastich)
                    {
                        var reponse = await _elasticReportService.CheckReportBalanceAndHistory(date);
                        if (reponse.UpdateBalances != null && reponse.UpdateBalances.Count > 0)
                        {
                            foreach (var item in reponse.UpdateBalances.Where(c => c.CurrencyCode == "VND"))
                            {
                                var sale = await _elasticReportService.getSaleByAccount(item.AccountCode, date);
                                await _balanceReportSvc.UpdateBalanceByInput(new ReportAccountBalanceDay()
                                {
                                    AccountCode = item.AccountCode,
                                    CurrencyCode = item.CurrencyCode,
                                    TextDay = item.AccountCode + "_" + date.ToString("yyyyMMdd"),
                                    Credite = sale.Credite,
                                    Debit = sale.Debit,
                                    IncDeposit = sale.IncDeposit,
                                    IncOther = sale.IncOther,
                                    DecPayment = sale.DecPayment,
                                    DecOther = sale.DecOther,
                                    BalanceBefore = item.BalanceBefore,
                                    BalanceAfter = item.BalanceAfter,
                                });
                            }
                        }
                    }
                    else
                    {
                        var list = await _balanceReportSvc.GetCheckAgentBalance(new SyncAgentBalanceRequest()
                        {
                            FromDate = date,
                            ToDate = DateTime.Now.Date,
                            Type = 1,
                        });
                        foreach (var item in list)
                        {
                            await _balanceReportSvc.SysAgentBalance(new SyncAgentBalanceRequest()
                            {
                                AgentCode = item.AgentCode,
                                Type = 2,
                                FromDate = date,
                                ToDate = DateTime.Now.Date
                            });
                        }
                    }
                }

                #endregion

                #region 2.Cảnh báo tài khoản lệch số liệu

                var register = await _balanceReportSvc.GetRegisterInfo(ReportRegisterType.WarningBalance);
                if (register != null && register.IsAuto && !string.IsNullOrEmpty(register.EmailSend))
                {
                    var date = DateTime.Now.Date.AddDays(-1);
                    if (_searchElastich)
                    {
                        //.Lấy số liệu để check               
                        var reponse = await _elasticReportService.CheckReportBalanceAndHistory(date);
                        var dataTable = FillBodyByTableWarningBalance(reponse);
                        var listMail = register.EmailSend.Split(',', ';').ToList();
                        _emailSender.SendEmailReportAuto(listMail, register.Content + $" -{(DateTime.Now.ToString("dd-MM-yyyy"))}", dataTable);
                    }
                    else
                    {
                        var checkDto = new SyncAgentBalanceRequest()
                        {
                            FromDate = date,
                            ToDate = DateTime.Now.Date,
                            Type = 1,
                        };
                        //.Lấy số liệu để check               
                        var list = await _balanceReportSvc.GetCheckAgentBalance(checkDto);
                        var dataTable = FillBodyByTableWarning(list);
                        var listMail = register.EmailSend.Split(',', ';').ToList();
                        _emailSender.SendEmailReportAuto(listMail, register.Content + $" -{(DateTime.Now.ToString("dd-MM-yyyy"))}", dataTable);
                    }

                }

                #endregion
            }
            catch (Exception ex)
            {
                _log.LogInformation($"ProcessWarning_Exception: {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
            }
        }

        private string FillBodyByTableWarning(List<ReportWarning> listResult)
        {
            try
            {
                StringBuilder strBuilder = new StringBuilder();
                strBuilder.Append(
                    "<table cellpadding='1' cellspacing='1' border='1' class='table-bordered table-hover dataTable' cellspacing='1' cellpadding='1' align='Left' rules='all' style='border-width:0px;width:100%;margin-bottom: 0px'><tr>");
                strBuilder.Append("<th class='align_center' scope='col'>STT</th>");
                strBuilder.Append($"<th class='align_center' scope='col'>Tài khoản</th>");
                strBuilder.Append($"<th class='align_center' scope='col'>Ngày</th>");
                strBuilder.Append("</tr>");
                int index = 1;
                foreach (var rpt in listResult)
                {
                    strBuilder.Append(string.Format("<tr><td class='align_left'><b>{0}</b></td>", index));
                    strBuilder.Append(string.Format("<td class='align_right'>{0}</td>",
                        rpt.AgentCode + " - " + rpt.AgentName));
                    strBuilder.Append(string.Format("<td class='align_right'>{0}</td>", rpt.CreatedDay));
                    strBuilder.Append("</tr>");
                    index = index + 1;
                }

                strBuilder.Append("</table>");
                return strBuilder.ToString();
            }
            catch (Exception ex)
            {
                _log.LogInformation($"FillBodyByTableWarning Exception: {ex.Message}|{ex.InnerException}|{ex.Source}");
                return null;
            }
        }

        private string FillBodyByTableWarningBalance(ReportCheckBalance result)
        {
            try
            {
                StringBuilder strBuilder = new StringBuilder();
                strBuilder.Append(
                    "<table cellpadding='1' cellspacing='1' border='1' class='table-bordered table-hover dataTable' cellspacing='1' cellpadding='1' align='Left' rules='all' style='border-width:0px;width:100%;margin-bottom: 0px'><tr>");
                strBuilder.Append("<th class='align_center' scope='col'>STT</th>");
                strBuilder.Append($"<th class='align_center' scope='col'>Tài khoản</th>");
                strBuilder.Append($"<th class='align_center' scope='col'>Ngày</th>");
                strBuilder.Append($"<th class='align_center' scope='col'>Ghi chú:(Before + AmountUp - AmountDown - After)</th>");
                strBuilder.Append("</tr>");
                int index = 1;
                strBuilder.Append(string.Format("<tr><td class='align_left' colspan='4'><b>I.Lệch lịch sử số dư</b></td></tr>"));
                foreach (var rpt in result.Historys)
                {
                    strBuilder.Append(string.Format("<tr><td class='align_left'><b>{0}</b></td>", index));
                    strBuilder.Append(string.Format("<td class='align_right'>{0}</td>", rpt.AccountCode + " - " + rpt.AccountInfo));
                    strBuilder.Append(string.Format("<td class='align_right'>{0}</td>", rpt.TextDay));
                    strBuilder.Append($"<td class='align_right'>{rpt.BalanceBefore} + {rpt.Credited} - {rpt.Debit} - {rpt.BalanceAfter} = {(rpt.BalanceBefore + rpt.Credited - rpt.Debit - rpt.BalanceAfter)}</td>");
                    strBuilder.Append("</tr>");
                    index = index + 1;
                }

                strBuilder.Append(string.Format("<tr><td class='align_left' colspan='4'><b>II.Lệch số dư báo cáo cân đối</b></td></tr>"));
                index = 1;
                foreach (var rpt in result.Balances)
                {
                    strBuilder.Append(string.Format("<tr><td class='align_left'><b>{0}</b></td>", index));
                    strBuilder.Append(string.Format("<td class='align_right'>{0}</td>", rpt.AccountCode + " - " + rpt.AccountInfo));
                    strBuilder.Append(string.Format("<td class='align_right'>{0}</td>", rpt.TextDay));
                    strBuilder.Append($"<td class='align_right'>{rpt.BalanceBefore} + {rpt.Credited} - {rpt.Debit} - {rpt.BalanceAfter} = {(rpt.BalanceBefore + rpt.Credited - rpt.Debit - rpt.BalanceAfter)}</td>");
                    strBuilder.Append("</tr>");
                    index = index + 1;
                }

                strBuilder.Append(string.Format("<tr><td class='align_left' colspan='4'><b>III.Lệch báo cáo cân đối và lịch sử số dư</b></td></tr>"));
                index = 1;
                foreach (var rpt in result.BalanceOtherHistory)
                {
                    strBuilder.Append(string.Format("<tr><td class='align_left'><b>{0}</b></td>", index));
                    strBuilder.Append(string.Format("<td class='align_right'>{0}</td>", rpt.AccountCode + " - " + rpt.AccountInfo));
                    strBuilder.Append(string.Format("<td class='align_right'>{0}</td>", rpt.TextDay));
                    strBuilder.Append($"<td class='align_right'>{rpt.BalanceBefore} >> {rpt.Credited} >> {rpt.Debit} >> {rpt.BalanceAfter}</td>");
                    strBuilder.Append("</tr>");
                    index = index + 1;
                }

                strBuilder.Append("</table>");
                return strBuilder.ToString();
            }
            catch (Exception ex)
            {
                _log.LogInformation($"FillBodyByTableWarningBalance Exception: {ex.Message}|{ex.InnerException}|{ex.Source}");
                return null;
            }
        }

        private string FillBodyByTableSendMailAgentApi(List<ReportSendMailAgentApi> listResult)
        {
            try
            {
                StringBuilder strBuilder = new StringBuilder();
                strBuilder.Append(
                    "<table cellpadding='1' cellspacing='1' border='1' class='table-bordered table-hover dataTable' cellspacing='1' cellpadding='1' align='Left' rules='all' style='border-width:0px;width:100%;margin-bottom: 0px'><tr>");
                strBuilder.Append("<th class='align_center' scope='col'>STT</th>");
                strBuilder.Append($"<th class='align_center' scope='col'>Tài khoản</th>");
                strBuilder.Append($"<th class='align_center' scope='col'>Trạng thái</th>");
                strBuilder.Append($"<th class='align_center' scope='col'>Nội dung</th>");
                strBuilder.Append("</tr>");
                int index = 1;
                foreach (var rpt in listResult)
                {
                    strBuilder.Append(string.Format("<tr><td class='align_left'><b>{0}</b></td>", index));
                    strBuilder.Append(string.Format("<td class='align_right'>{0}</td>", rpt.AgentInfo));
                    strBuilder.Append(string.Format("<td class='align_right'>{0}</td>", (rpt.IsSend ? "Đã gửi" : "Lỗi")));
                    strBuilder.Append(string.Format("<td class='align_right'>{0}</td>", rpt.Description));
                    strBuilder.Append("</tr>");
                    index = index + 1;
                }

                strBuilder.Append("</table>");
                return strBuilder.ToString();
            }
            catch (Exception ex)
            {
                _log.LogInformation($"FillBodyByTableSendMailAgentApi Exception: {ex.Message}|{ex.InnerException}|{ex.Source}");
                return null;
            }
        }

        private string FillBodyByTableCardWarning(List<ReportCardStockDayDto> listResult, List<ReportCardStockProviderByDate> listProviderResult)
        {
            try
            {
                var listStock = (from x in listResult.Where(c => c.Before_Sale + c.Import_Sale - c.Export_Sale - c.After_Sale != 0).ToList()
                                 select new ReportCardStockByDate
                                 {
                                     CardValue = x.CardValue,
                                     CategoryCode = x.CategoryName,
                                     InventoryBefore = x.Before_Sale,
                                     Increase = x.Import_Sale,
                                     Decrease = x.Export_Sale,
                                     InventoryAfter = x.After_Sale,
                                     ProductCode = x.ProductCode,
                                     StockCode = "STOCK_SALE",
                                 }).ToList();

                var listStockTemp = (from x in listResult.Where(c => c.Before_Temp + c.Import_Temp - c.Export_Temp - c.After_Temp != 0).ToList()
                                     select new ReportCardStockByDate
                                     {
                                         CardValue = x.CardValue,
                                         CategoryCode = x.CategoryName,
                                         InventoryBefore = x.Before_Temp,
                                         Increase = x.Import_Temp,
                                         Decrease = x.Export_Temp,
                                         InventoryAfter = x.After_Temp,
                                         ProductCode = x.ProductCode,
                                         StockCode = "STOCK_TEMP",
                                     }).ToList();

                listStock.AddRange(listStockTemp);
                var listStockProvider = listProviderResult.Where(c => c.InventoryBefore + c.IncreaseSupplier + c.IncreaseOther - c.Sale - c.ExportOther - c.InventoryAfter != 0).ToList();
                var listGroup = (from x in listProviderResult
                                 group x by new { x.StockCode, x.ProductCode, x.CategoryCode, x.CardValue } into g
                                 select new ReportCardStockByDate()
                                 {
                                     StockCode = g.Key.StockCode,
                                     ProductCode = g.Key.ProductCode,
                                     CardValue = g.Key.CardValue,
                                     CategoryCode = g.Key.CategoryCode,
                                     InventoryBefore = g.Sum(c => c.InventoryBefore),
                                     InventoryAfter = g.Sum(c => c.InventoryAfter),
                                     IncreaseSupplier = g.Sum(c => c.IncreaseSupplier),
                                     IncreaseOther = g.Sum(c => c.IncreaseOther),
                                     ExportOther = g.Sum(c => c.ExportOther),
                                     Sale = g.Sum(c => c.Sale),
                                 }).ToList();

                var listChenhLech = (from s in listStock
                                     join p in listGroup on s.ProductCode equals p.ProductCode
                                     where s.StockCode == p.StockCode &&
                                     (s.InventoryAfter != p.InventoryAfter
                                     || s.InventoryBefore != p.InventoryBefore
                                     || s.Increase != (p.IncreaseSupplier + p.IncreaseOther)
                                     || s.Decrease != (p.ExportOther + p.Sale))
                                     select new ReportCardStockByDate()
                                     {
                                         StockCode = s.StockCode,
                                         ProductCode = s.ProductCode,
                                         CardValue = s.CardValue,
                                         CategoryCode = s.CategoryCode,
                                         InventoryBefore = s.InventoryBefore - p.InventoryBefore,
                                         InventoryAfter = s.InventoryAfter - p.InventoryAfter,
                                         Increase = s.Increase - (p.IncreaseSupplier + p.IncreaseOther),
                                         Decrease = s.Decrease - (p.ExportOther + p.Sale),
                                     }).ToList();


                StringBuilder strBuilder = new StringBuilder();
                strBuilder.Append("<table cellpadding='1' cellspacing='1' border='1' class='table-bordered table-hover dataTable' cellspacing='1' cellpadding='1' align='Left' rules='all' style='border-width:0px;width:100%;margin-bottom: 0px'><tr>");
                strBuilder.Append("<th class='align_center' scope='col'>STT</th>");
                strBuilder.Append($"<th class='align_center' scope='col'>Loai kho</th>");
                strBuilder.Append($"<th class='align_center' scope='col'>Sản phẩm</th>");
                strBuilder.Append($"<th class='align_center' scope='col'>Note: (Before + Input - Export- After = Lech)</th>");
                strBuilder.Append($"<th class='align_center' scope='col'>Nhà cung cấp</th>");
                strBuilder.Append("</tr>");
                int index = 1;
                strBuilder.Append(string.Format("<tr><td class='align_left' colspan='5'><b>I.Lệch kho</b></td></tr>"));

                foreach (var rpt in listStock)
                {
                    strBuilder.Append(string.Format("<tr><td class='align_left'><b>{0}</b></td>", index));
                    strBuilder.Append(string.Format("<td class='align_right'>{0}</td>", rpt.StockCode));
                    strBuilder.Append(string.Format("<td class='align_right'>{0}</td>", rpt.ProductCode + " ->" + rpt.CategoryCode));
                    strBuilder.Append(string.Format("<td class='align_right'>{0} + {1} + {2} - {3} = {4}</td>",
                        rpt.InventoryBefore, rpt.Increase, rpt.Decrease, rpt.InventoryAfter,
                        rpt.InventoryBefore + rpt.Increase - rpt.Decrease - rpt.InventoryAfter));
                    strBuilder.Append(string.Format("<td class='align_right'></td>"));
                    strBuilder.Append("</tr>");
                    index = index + 1;
                }
                index = 1;
                strBuilder.Append(string.Format("<tr><td class='align_left' colspan='5'><b>II.Lệch kho Theo NCC</b></td></tr>"));
                foreach (var rpt in listStockProvider)
                {
                    strBuilder.Append(string.Format("<tr><td class='align_left'><b>{0}</b></td>", index));
                    strBuilder.Append(string.Format("<td class='align_right'>{0}</td>", rpt.ProductCode + " ->" + rpt.CategoryCode));
                    strBuilder.Append(string.Format("<td class='align_right'>{0} + {1} + {2} - {3} = {4}</td>",
                        rpt.InventoryBefore, rpt.IncreaseSupplier + rpt.IncreaseOther, rpt.Sale + rpt.ExportOther, rpt.InventoryAfter,
                        rpt.InventoryBefore + rpt.IncreaseSupplier + rpt.IncreaseOther - rpt.Sale - rpt.ExportOther - rpt.InventoryAfter));
                    strBuilder.Append(string.Format("<td class='align_right'>{0}</td>", rpt.ProviderCode));
                    strBuilder.Append("</tr>");
                    index = index + 1;
                }
                index = 1;
                strBuilder.Append(string.Format("<tr><td class='align_left' colspan='5'><b>III.Lệch kho giữa 2 kho</b></td></tr>"));
                foreach (var rpt in listChenhLech)
                {
                    strBuilder.Append(string.Format("<tr><td class='align_left'><b>{0}</b></td>", index));
                    strBuilder.Append(string.Format("<td class='align_right'>{0}</td>", rpt.ProductCode + " ->" + rpt.CategoryCode));
                    strBuilder.Append(string.Format("<td class='align_right'>{0} => {1} => {2} => {3}</td>",
                        rpt.InventoryBefore, rpt.Increase, rpt.Decrease, rpt.InventoryAfter));
                    strBuilder.Append(string.Format("<td class='align_right'></td>"));
                    strBuilder.Append("</tr>");
                    index = index + 1;
                }
                strBuilder.Append("</table>");
                return strBuilder.ToString();
            }
            catch (Exception ex)
            {
                _log.LogInformation($"FillBodyByTableCardWarning Exception: {ex.Message}|{ex.InnerException}|{ex.Source}");
                return null;
            }
        }

        public async Task SysDayOneProcess()
        {
            DateTime date = DateTime.Now.Date;
            if (date.Day == 1)
                await _balanceReportSvc.SysDayOneProcess(date);
        }

        public async Task SysDeleteFileFpt()
        {
            await _balanceReportSvc.DeleteFileFpt(DateTime.Now.Date.AddDays(-2));
        }

        public async Task SysDeleteFile()
        {
            await _balanceReportSvc.DeleteFileNow(ReportConst.Balance, string.Empty);
            await _balanceReportSvc.DeleteFileNow(ReportConst.Revenue, string.Empty);
            await _balanceReportSvc.DeleteFileNow(ReportConst.Auto, string.Empty);
            await _balanceReportSvc.DeleteFileNow(ReportConst.NXT, string.Empty);
            await _balanceReportSvc.DeleteFileNow(ReportConst.SMS, string.Empty);
            await _balanceReportSvc.DeleteFileNow(ReportConst.Provider, string.Empty);
            await _balanceReportSvc.DeleteFileNow(ReportConst.Batch, string.Empty);
            await _balanceReportSvc.DeleteFileNow(ReportConst.Agent, string.Empty);
            await _balanceReportSvc.DeleteFileNow(ReportConst.ZIP, string.Empty);
        }

        #endregion

        #region 9.Cảnh báo số dư trên toàn hệ thống

        public async Task ProcessCompareSystemAccount(DateTime date)
        {
            try
            {
                _log.LogInformation("Start ProcessCompareSystemAccount Process");
                var register = await _balanceReportSvc.GetRegisterInfo(ReportRegisterType.AccountSystem);
                if (register != null && !string.IsNullOrEmpty(register.AccountList) && !string.IsNullOrEmpty(register.EmailSend))
                {
                    var dateText = date.Date.ToString("yyyyMMdd");
                    var arrayAccounts = register.AccountList.Split(',', ';', '|').ToList();
                    foreach (var item in arrayAccounts)
                    {
                        var getData = await _balanceReportSvc.GetAccountSystemDayByCode(item, dateText);
                        //1.Control
                        if (item.StartsWith("CONTROL"))
                        {
                            if (getData == null)
                            {
                                var balance = await _balanceReportSvc.getCheckBalance(item);
                                getData = new ReportSystemDay()
                                {
                                    AccountCode = item,
                                    BalanceAfter = balance,
                                    BalanceBefore = balance,
                                    UpdateDate = DateTime.Now,
                                    TextDay = dateText,
                                    CurrencyCode = "VND"
                                };
                                await _balanceReportSvc.UpdateAccountSystemDay(getData);
                            }
                            continue;
                        }

                        //2.Customer
                        if (item.StartsWith("CUSTOMER"))
                        {
                            var agentRequest = new ReportAgentBalanceRequest()
                            {
                                FromDate = date.Date,
                                ToDate = date.Date.AddDays(1).AddMilliseconds(-1),
                                Offset = 0,
                                Limit = 1,
                            };
                            var rsAgent = _searchElastich
                            ? await _elasticReportService.ReportAgentBalanceGetList(agentRequest)
                            : await _balanceReportSvc.ReportAgentBalanceGetList(agentRequest);

                            if (rsAgent.ResponseCode == ResponseCodeConst.Success && rsAgent.Total >= 1)
                            {
                                var sumDataAgent = rsAgent.SumData.ConvertTo<ReportAgentBalanceDto>();
                                if (getData == null)
                                {
                                    getData = new ReportSystemDay()
                                    {
                                        AccountCode = item,
                                        BalanceAfter = sumDataAgent.AfterAmount,
                                        BalanceBefore = sumDataAgent.BeforeAmount,
                                        UpdateDate = DateTime.Now,
                                        TextDay = dateText,
                                        CurrencyCode = "VND"
                                    };

                                    await _balanceReportSvc.UpdateAccountSystemDay(getData);
                                }
                                else
                                {
                                    getData.BalanceBefore = getData.BalanceBefore;
                                    getData.BalanceAfter = getData.BalanceAfter;
                                    getData.UpdateDate = DateTime.Now;
                                    await _balanceReportSvc.UpdateAccountSystemDay(getData);
                                }
                            }
                            continue;
                        }

                        _log.LogInformation($"ProcessCompareSystemAccount Check_Update Account:  {item}|DateText= {dateText}");
                        var request = new ReportDetailRequest()
                        {
                            AccountCode = item,
                            Limit = 1,
                            Offset = 0,
                            FromDate = date.Date,
                            ToDate = date.Date.AddDays(1).AddMilliseconds(-1)
                        };

                        var reponsePayment = _searchElastich
                        ? await _elasticReportService.ReportDetailGetList(request)
                        : await _balanceReportSvc.ReportDetailGetList(request);

                        if (reponsePayment.ResponseCode == ResponseCodeConst.Success && reponsePayment.Total >= 1)
                        {
                            var sumDataPayment = reponsePayment.SumData.ConvertTo<ReportTransactionDetailDto>();
                            if (getData == null)
                            {
                                getData = new ReportSystemDay()
                                {
                                    AccountCode = item,
                                    BalanceAfter = sumDataPayment.BalanceAfter,
                                    BalanceBefore = sumDataPayment.BalanceBefore,
                                    UpdateDate = DateTime.Now,
                                    TextDay = dateText,
                                    CurrencyCode = "VND"
                                };
                                await _balanceReportSvc.UpdateAccountSystemDay(getData);
                            }
                            else
                            {
                                getData.BalanceBefore = sumDataPayment.BalanceBefore;
                                getData.BalanceAfter = sumDataPayment.BalanceAfter;
                                getData.UpdateDate = DateTime.Now;
                                await _balanceReportSvc.UpdateAccountSystemDay(getData);
                            }
                        }
                        else
                        {
                            var balance = await _balanceReportSvc.getCheckBalance(item);
                            if (getData == null)
                            {
                                getData = new ReportSystemDay()
                                {
                                    AccountCode = item,
                                    BalanceAfter = balance,
                                    BalanceBefore = balance,
                                    UpdateDate = DateTime.Now,
                                    TextDay = dateText,
                                    CurrencyCode = "VND"
                                };
                                await _balanceReportSvc.UpdateAccountSystemDay(getData);
                            }
                            else
                            {
                                getData.BalanceAfter = balance;
                                getData.UpdateDate = DateTime.Now;
                                await _balanceReportSvc.UpdateAccountSystemDay(getData);
                            }

                        }
                    }

                    var list = await _balanceReportSvc.GetAccountSystemDay(dateText);

                    #region 9.1.Cảnh báo 1

                    var lst = new List<ReportSystemDay>();
                    var lstBody = new List<ReportSystemDay>();
                    var control = list.Where(c => c.AccountCode.StartsWith("CONTROL")).Sum(c => c.BalanceAfter);
                    lst.Add(new ReportSystemDay() { AccountCode = "CONTROL", BalanceAfter = control });

                    var master = list.Where(c => c.AccountCode.StartsWith("MASTER")).Sum(c => c.BalanceAfter);
                    lst.Add(new ReportSystemDay() { AccountCode = "MASTER", BalanceAfter = master });

                    var payment = list.Where(c => c.AccountCode.StartsWith("PAYMENT")).Sum(c => c.BalanceAfter);
                    lst.Add(new ReportSystemDay() { AccountCode = "PAYMENT", BalanceAfter = payment });

                    var customer = list.Where(c => c.AccountCode.StartsWith("CUSTOMER")).Sum(c => c.BalanceAfter);
                    lst.Add(new ReportSystemDay() { AccountCode = "CUSTOMER", BalanceAfter = customer });

                    var commistion = list.Where(c => c.AccountCode.StartsWith("COMMISSTION")).Sum(c => c.BalanceAfter);
                    lst.Add(new ReportSystemDay() { AccountCode = "COMMISSTION", BalanceAfter = commistion });

                    lstBody.AddRange(lst);
                    var difference1 = master - (payment + customer + commistion);
                    lstBody.Add(new ReportSystemDay() { AccountCode = "[MASTER - (PAYMENT + COMMISSTION + CUSTOMER)]", BalanceAfter = difference1 });

                    var difference2 = control - (master + payment + customer + commistion);
                    lstBody.Add(new ReportSystemDay() { AccountCode = "[CONTROL - (MASTER + PAYMENT + COMMISSTION + CUSTOMER)]", BalanceAfter = difference2 });

                    string tileMail = $"Cảnh báo số dư hệ thống ngày: {date.ToString("dd-MM-yyyy")}";
                    var body = FillBodyByTableSystemAccount(lstBody);
                    var listMailComplete = (register.EmailSend ?? "").Split(',', ';').ToList();
                    _emailSender.SendEmailReportAuto(listMailComplete, tileMail, body);

                    #endregion

                    #region 9.2.Cảnh báo 2

                    string title = "Cảnh báo chệnh lệch số dư hệ thống";
                    string msg = $"[MASTER - (PAYMENT + COMMISSTION + CUSTOMER) = {difference1}]\n"
                     + $"[CONTROL - (MASTER + PAYMENT + COMMISSTION + CUSTOMER) = {difference2}]"
                     + $"\nChi tiết :";
                    foreach (var item in lst)
                    {
                        msg += $"\n{item.AccountCode}: {item.BalanceAfter}";
                    }
                    try
                    {
                        //await _bus.Publish<SendBotMessage>(new
                        //{
                        //    BotType = BotType.Sale,
                        //    Module = "Report",
                        //    Title = title,
                        //    Message = msg,
                        //    MessageType = BotMessageType.Message,
                        //    ChatId = _chatId
                        //});
                    }
                    catch (Exception e)
                    {
                        _log.LogError(e, $"SendBotMessage ==> Send Noti  ex : {e.Message}");
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"ProcessCompareSystemAccount Exception: {ex.Message}|{ex.InnerException}|{ex.Source}");
            }
        }

        private string FillBodyByTableSystemAccount(List<ReportSystemDay> list)
        {
            try
            {
                StringBuilder strBuilder = new StringBuilder();
                strBuilder.Append(
                    "<table cellpadding='1' cellspacing='1' border='1' class='table-bordered table-hover dataTable' cellspacing='1' cellpadding='1' align='Left' rules='all' style='border-width:0px;width:100%;margin-bottom: 0px'><tr>");
                strBuilder.Append("<th class='align_center' scope='col'>STT</th>");
                strBuilder.Append($"<th class='align_center' scope='col'>Tài khoản</th>");
                strBuilder.Append($"<th class='align_center' scope='col'>Số tiền</th>");
                strBuilder.Append("</tr>");
                int index = 1;
                foreach (var rpt in list)
                {
                    strBuilder.Append(string.Format("<tr><td class='align_left'><b>{0}</b></td>", index));
                    strBuilder.Append(string.Format("<td class='align_right'>{0}</td>", rpt.AccountCode));
                    strBuilder.Append(string.Format("<td class='align_right'>{0}</td>", string.Format("{0:N0}", rpt.BalanceAfter)));
                    strBuilder.Append("</tr>");
                    index = index + 1;
                }

                strBuilder.Append("</table>");
                return strBuilder.ToString();
            }
            catch (Exception ex)
            {
                _log.LogInformation($"FillBodyByTableSystemAccount Exception: {ex.Message}|{ex.InnerException}|{ex.Source}");
                return null;
            }
        }

        #endregion
    }
}