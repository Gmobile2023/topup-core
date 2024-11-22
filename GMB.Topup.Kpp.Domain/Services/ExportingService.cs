using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GMB.Topup.Shared;
using GMB.Topup.Shared.CacheManager;
using GMB.Topup.Shared.Emailing;
using Microsoft.Extensions.Logging;
using ServiceStack;
using System.IO.Compression;
using GMB.Topup.Kpp.Domain.Entities;
using GMB.Topup.Kpp.Domain.Exporting;
using GMB.Topup.Kpp.Domain.Repositories;


namespace GMB.Topup.Kpp.Domain.Services;

public interface IExportingService
{
    Task<MessageResponseBase> KppFilePayment(string accountCode, DateTime date);
    Task<MessageResponseBase> KppFileTransfer(string accountCode, DateTime date);
    Task<MessageResponseBase> KppFileAccount(string accountCode, DateTime date);
    Task<MessageResponseBase> KppAccount(string accountCode);

    Task ProcessKppFile(DateTime date);
}

public class ExportingService : IExportingService
{
    private readonly ICacheManager _cacheManager;
    private readonly IExportDataExcel _dataExcel;
    private readonly IEmailSender _emailSender;
    private readonly IKppPosgreRepository _kppRepository;
    private readonly ILogger<ExportingService> _log;
    private readonly IKppMongoRepository _mongoRepository;
    private static bool _isProcess;
    private readonly string Key = "KPP";

    public ExportingService(ILogger<ExportingService> log,
        IKppPosgreRepository kppRepository,
        IKppMongoRepository mongoRepository,
        IExportDataExcel dataExcel,
        ICacheManager cacheManager,
        IEmailSender emailSender)
    {
        _kppRepository = kppRepository;
        _mongoRepository = mongoRepository;
        _emailSender = emailSender;
        _dataExcel = dataExcel;
        _cacheManager = cacheManager;
        _log = log;
    }


    public async Task ProcessKppFile(DateTime date)
    {
        if (_isProcess == true) return;
        _isProcess = true;
        try
        {
            _log.LogInformation("Start ProcessKppFile Process");
            var register = await _mongoRepository.GetRegisterInfo("KPP_FILE");

            _log.LogInformation($"ProcessKppFile {(register != null ? register.ToJson() : "")}");
            if (register != null && register.IsAuto)
            {
                var dataPayment = await _kppRepository.GetDataTransPayment(string.Empty, date);
                var dataTransfer = await _kppRepository.GetDataTransfer(string.Empty, date);
                var lisAccount = await _kppRepository.GetKppAccount();
                dataPayment = await ChangeTransaction(dataPayment);

                var listPayment = dataPayment.ConvertTo<List<TransactionView>>();
                var listTransfer = dataTransfer.ConvertTo<List<TransferView>>();
                listPayment = ChangerBalance(listPayment);

                var soucePath = await GetForderCreate(Key);
                var filePayment = await ExportFilePayment(listPayment, soucePath, date);
                var fileTransfer = await ExportFileTransfer(listTransfer, soucePath, date);
                var fileAccount = await ExportFileAccount(listPayment, listTransfer, lisAccount, soucePath, date);
                var desPath = await ZipForderCreate(soucePath);
                var listMail = register.EmailSend.Split(',', ';').ToList();
                var tille = register.Name + " ngày " + date.ToString("dd-MM-yyyy");
                _emailSender.SendEmailReportAuto(listMail, tille, tille, desPath);
            }
        }
        catch (Exception ex)
        {
            _log.LogError($"ProcessKppFile Exception: {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
        }

        _isProcess = false;
    }

    #region 1.Phần xuất chi tiết bán hàng

    public async Task<MessageResponseBase> KppFilePayment(string accountCode, DateTime date)
    {
        var register = await _mongoRepository.GetRegisterInfo("KPP_FILE");
        _log.LogInformation($"ProcessKppFile {(register != null ? register.ToJson() : "")}");
        if (register != null && !string.IsNullOrEmpty(register.EmailSend))
        {
            var dataPayment = await _kppRepository.GetDataTransPayment(accountCode, date);
            dataPayment = await ChangeTransaction(dataPayment);
            var lisPayment = dataPayment.ConvertTo<List<TransactionView>>();
            lisPayment = ChangerBalance(lisPayment);
            var soucePath = await GetForderCreate(Key);
            await ExportFilePayment(lisPayment, soucePath, date);
            var desPath = await ZipForderCreate(soucePath);
            var listMail = register.EmailSend.Split(',', ';').ToList();
            var tille = register.Name + " ngày " + date.ToString("dd-MM-yyyy");
            _emailSender.SendEmailReportAuto(listMail, tille, tille, desPath);
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Success,
                ResponseMessage = "Xuất file thành công."
            };
        }

        return new MessageResponseBase
        {
            ResponseCode = ResponseCodeConst.Error,
            ResponseMessage = "Thất bại."
        };
    }

    private bool IsWriteFileTransCsv(string headerfiles, string fileName, List<TransactionView> list)
    {
        try
        {
            using (var file = new StreamWriter(fileName, true, Encoding.UTF8))
            {
                _log.LogInformation($"{fileName} Ghi headerfiles");
                var sb = new StringBuilder();
                sb.Append(headerfiles);
                file.WriteLine(sb.ToString());
                var count = 1;
                _log.LogInformation($"{fileName} TotalRow : {list.Count()}");
                foreach (var item in list.OrderBy(c => c.transDate))
                {
                    try
                    {
                        sb = new StringBuilder();
                        sb.Append(count).Append(",");
                        sb.Append(item.trans_code ?? string.Empty).Append(",");
                        sb.Append(item.account_code).Append(",");
                        sb.Append(item.amount).Append(",");
                        sb.Append(item.trans_amount ?? 0).Append(",");
                        sb.Append(item.receiver).Append(",");
                        sb.Append(item.telco).Append(",");
                        sb.Append(item.created_date.ToString("dd-MM-yyyy HH:mm:ss")).Append(",");
                        sb.Append(item.kpp_trans_id).Append(",");
                        sb.Append(item.status).Append(",");
                        sb.Append(item.balance).Append(",");
                        sb.Append(item.ended_date != null ? item.ended_date.Value.ToString("dd-MM-yyyy HH:mm:ss") : "").Append(",");
                        file.WriteLine(sb.ToString());
                    }
                    catch (Exception exItem)
                    {
                        _log.LogInformation($"{item.ToJson()} Exception: {exItem.Message}|{exItem.InnerException}|{exItem.StackTrace}");
                    }
                    count++;
                }

                _log.LogInformation($"{fileName} Map xong !");
                _log.LogInformation($"{fileName} Ghi Danh sach thanh cong !");
                file.Close();

                return true;
            }
        }
        catch (Exception ex)
        {
            _log.LogError($"{fileName} Ghi Danh sach Exception {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
            return false;
        }
    }

    public async Task<bool> ExportFilePayment(List<TransactionView> lis, IntFile sourcePath, DateTime date)
    {
        try
        {
            var headder = "STT,Ma doi soat,Ma dai ly KPP,Menh gia,Thanh tien,So thu huong,Nha mang,Thoi gian tao,Ma GD KPP,Trang thai,So du sau GD,Thoi gian ket thuc";
            var litNT = lis.Where(c => c.account_code.StartsWith("DLC1") || c.account_code.StartsWith("DLC2") || c.account_code.StartsWith("DLC3")).ToList();
            var litNT_TS = lis.Where(c => !c.account_code.StartsWith("DLC1") && !c.account_code.StartsWith("DLC2") && !c.account_code.StartsWith("DLC3")).ToList();
            var fileName = $"KPP.PAYMENT.{date.ToString("yyyyMMdd")}.{DateTime.Now.ToString("HHmm")}.csv";
            var fileNameTs = $"KPP.PAYMENT_TS.{date.ToString("yyyyMMdd")}.{DateTime.Now.ToString("HHmm")}.csv";

            var pathSave = $"{sourcePath.PathName}/{fileName}";
            var pathSaveTs = $"{sourcePath.PathName}/{fileNameTs}";

            IsWriteFileTransCsv(headder, pathSave, litNT);
            IsWriteFileTransCsv(headder, pathSaveTs, litNT_TS);
            return true;
        }
        catch (Exception ex)
        {
            _log.LogError($"ExportFilePayment Exception: {ex.Message}|{ex.StackTrace}|{ex.InnerException}");
            return false;
        }
    }

    #endregion

    #region 2.Phần xuất chi tiết chuyển tiền

    public async Task<MessageResponseBase> KppFileTransfer(string accountCode, DateTime date)
    {
        var register = await _mongoRepository.GetRegisterInfo("KPP_FILE");
        _log.LogInformation($"KppFileTransfer {(register != null ? register.ToJson() : "")}");
        if (register != null && !string.IsNullOrEmpty(register.EmailSend))
        {
            var dataTransfer = await _kppRepository.GetDataTransfer(accountCode, date);
            var listTransfer = dataTransfer.ConvertTo<List<TransferView>>();
            var soucePath = await GetForderCreate(Key);

            await ExportFileTransfer(listTransfer, soucePath, date);
            var desPath = await ZipForderCreate(soucePath);
            var listMail = register.EmailSend.Split(',', ';').ToList();
            var tille = register.Name + " ngày " + date.ToString("dd-MM-yyyy");
            _emailSender.SendEmailReportAuto(listMail, tille, tille, desPath);
            return new MessageResponseBase
            {
                ResponseCode = ResponseCodeConst.Success,
                ResponseMessage = "Xuất file thành công."
            };
        }

        return new MessageResponseBase
        {
            ResponseCode = ResponseCodeConst.Error,
            ResponseMessage = "Thất bại."
        };
    }

    private bool IsWriteFileTransferCsv(string headerfiles, string fileName, List<TransferView> list)
    {
        try
        {
            using (var file = new StreamWriter(fileName, true, Encoding.UTF8))
            {
                _log.LogInformation($"{fileName} Ghi headerfiles");
                var sb = new StringBuilder();
                sb.Append(headerfiles);
                file.WriteLine(sb.ToString());
                var count = 1;
                _log.LogInformation($"{fileName} TotalRow : {list.Count()}");
                foreach (var item in list.OrderBy(c => c.TransDate))
                {
                    try
                    {
                        sb = new StringBuilder();
                        sb.Append(count).Append(",");
                        sb.Append(item.kpp_trans_id).Append(",");
                        sb.Append(item.created_date.ToString("yyyy-MM-dd HH:mm:ss")).Append(",");
                        sb.Append(item.sender).Append(",");
                        sb.Append(item.amount).Append(",");
                        sb.Append(item.receiver).Append(",");
                        sb.Append(item.status).Append(",");
                        sb.Append(item.ended_date?.ToString("yyyy-MM-dd HH:mm:ss"));
                        file.WriteLine(sb.ToString());
                    }
                    catch (Exception exItem)
                    {
                        _log.LogInformation(
                            $"{item.ToJson()} Exception: {exItem.Message}|{exItem.InnerException}|{exItem.StackTrace}");
                    }

                    count++;
                }

                _log.LogInformation($"{fileName} Map xong !");
                _log.LogInformation($"{fileName} Ghi Danh sach thanh cong !");
                file.Close();

                return true;
            }
        }
        catch (Exception ex)
        {
            _log.LogError($"{fileName} Ghi Danh sach Exception {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
            return false;
        }
    }

    private async Task<bool> ExportFileTransfer(List<TransferView> lis, IntFile sourcePath, DateTime date)
    {
        try
        {
            var litNT = lis.Where(c => c.sender.StartsWith("DLC1") || c.receiver.StartsWith("DLC1") || c.sender.StartsWith("DLC2")
            || c.receiver.StartsWith("DLC2") || c.sender.StartsWith("DLC3") || c.receiver.StartsWith("DLC3")).ToList();

            var litNT_TS = lis.Where(c => !c.sender.StartsWith("DLC1") && !c.sender.StartsWith("DLC2") && !c.sender.StartsWith("DLC3")).ToList();

            var headder = "STT,Ma GD,Thoi gian chuyen,Dai ly KPP chuyen,So tien,Dai ly KPP nhan,Trang thai";
            var fileName = $"KPP.TRANSFER.{date.ToString("yyyyMMdd")}.{DateTime.Now.ToString("HHmm")}.csv";
            var fileName_TS = $"KPP.TRANSFER_TS.{date.ToString("yyyyMMdd")}.{DateTime.Now.ToString("HHmm")}.csv";
            var pathSave = $"{sourcePath.PathName}/{fileName}";
            var pathSave_TS = $"{sourcePath.PathName}/{fileName_TS}";
            IsWriteFileTransferCsv(headder, pathSave, litNT);
            IsWriteFileTransferCsv(headder, pathSave_TS, litNT_TS);
            return true;
        }
        catch (Exception ex)
        {
            _log.LogError($"ExportFileTransfer Exception: {ex.Message}|{ex.StackTrace}|{ex.InnerException}");
            return false;
        }
    }

    #endregion

    #region 3.Phần xuất file tổng hợp

    public async Task<MessageResponseBase> KppFileAccount(string accountCode, DateTime date)
    {
        var register = await _mongoRepository.GetRegisterInfo("KPP_FILE");
        _log.LogInformation($"KppFileTransfer {(register != null ? register.ToJson() : "")}");
        if (register != null && !string.IsNullOrEmpty(register.EmailSend))
        {
            var dataPayment = await _kppRepository.GetDataTransPayment(accountCode, date);
            var dataTransfer = await _kppRepository.GetDataTransfer(accountCode, date);
            var accounts = await _kppRepository.GetKppAccount();

            var check = dataPayment.Where(c => c.trans_amount == 0 && (c.status == 1 || c.status == 0)).Count();
            if (check > 0)
            {
                foreach (var item in dataPayment)
                {
                    if (item.trans_amount == 0)
                    {
                        if ((item.discount_rate ?? 0) != 0)
                            item.trans_amount = Math.Round(item.amount * (1 - (item.discount_rate ?? 0) / 100), 0);
                        else
                        {
                            var ac = accounts.Where(c => c.account_code == item.account_code).FirstOrDefault();
                            if (ac != null)
                                item.trans_amount = Math.Round(item.amount * (1 - ac.airtime_discount_rate / 100), 0);
                        }
                    }
                }
            }

            var lisPayment = dataPayment.ConvertTo<List<TransactionView>>();
            var lisTransfer = dataTransfer.ConvertTo<List<TransferView>>();
            IntFile sourcePath = await GetForderCreate(Key);
            await ExportFileAccount(lisPayment, lisTransfer, accounts, sourcePath, date);
            var desPath = await ZipForderCreate(sourcePath);
            var listMail = register.EmailSend.Split(',', ';').ToList();
            var tille = register.Name + " ngày " + date.ToString("dd-MM-yyyy");
            if (lisTransfer.Count > 0)
            {
                _emailSender.SendEmailReportAuto(listMail, tille, tille, desPath);
                return new MessageResponseBase
                {
                    ResponseCode = ResponseCodeConst.Success,
                    ResponseMessage = "Xuất file thành công."
                };
            }
        }

        return new MessageResponseBase
        {
            ResponseCode = ResponseCodeConst.Error,
            ResponseMessage = "Thất bại."
        };
    }

    private bool IsWriteFileAccountCsv(string headerfiles, string fileName, List<AccountDto> list)
    {
        try
        {
            using (var file = new StreamWriter(fileName))
            {
                _log.LogInformation($"{fileName} Ghi headerfiles");
                var sb = new StringBuilder();
                sb.Append(headerfiles);
                file.WriteLine(sb.ToString());
                var count = 1;
                _log.LogInformation($"{fileName} TotalRow : {list.Count()}");
                foreach (var item in list.OrderBy(c => c.AccountCode))
                {
                    try
                    {
                        sb = new StringBuilder();
                        sb.Append(count).Append(",");
                        sb.Append(item.AccountCode).Append(",");
                        sb.Append(item.Before).Append(",");
                        sb.Append(item.Input).Append(",");
                        sb.Append(item.Transfer).Append(",");
                        sb.Append(item.Payment).Append(",");
                        sb.Append(item.After).Append(",");
                        sb.Append(item.Deviation).Append(",");
                        sb.Append(item.Status).Append(",");
                        file.WriteLine(sb.ToString());
                    }
                    catch (Exception exItem)
                    {
                        _log.LogInformation(
                            $"{item.ToJson()} Exception: {exItem.Message}|{exItem.InnerException}|{exItem.StackTrace}");
                    }

                    count++;
                }

                _log.LogInformation($"{fileName} Map xong !");
                _log.LogInformation($"{fileName} Ghi Danh sach thanh cong !");
                file.Close();

                return true;
            }
        }
        catch (Exception ex)
        {
            _log.LogError($"{fileName} Ghi Danh sach Exception {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
            return false;
        }
    }

    private async Task<bool> ExportFileAccount(List<TransactionView> litSale, List<TransferView> litTransfer,
        List<kpp_account> accountKpp, IntFile sourcePath, DateTime date)
    {
        try
        {
            var transferOut = (from x in litTransfer
                               where x.status == 1 && x.receiver != x.sender
                               group x by new { x.sender } into g
                               select new AccountDto
                               {
                                   AccountCode = g.Key.sender,
                                   Transfer = g.Sum(c => c.amount)
                               }).ToList();


            var transferIn = (from x in litTransfer
                              where x.status == 1
                              group x by new { x.receiver } into g
                              select new AccountDto
                              {
                                  AccountCode = g.Key.receiver,
                                  Input = g.Sum(c => c.amount)
                              }).ToList();

            var sale = (from x in litSale
                        where x.status == 1 || x.status == 0
                        group x by new { x.account_code } into g
                        select new AccountDto
                        {
                            AccountCode = g.Key.account_code,
                            Payment = g.Sum(c => c.trans_amount ?? 0)
                        }).ToList();


            var beforeDate = (from x in litSale
                              where x.status == 1
                              group x by new { x.account_code } into g
                              select new AccountDto
                              {
                                  AccountCode = g.Key.account_code,
                                  MinDate = g.Min(c => c.transDate)
                              }).ToList();


            var beforeBalance = from x in litSale
                                join y in beforeDate on x.account_code equals y.AccountCode
                                where x.transDate == y.MinDate
                                select new AccountDto
                                {
                                    AccountCode = x.account_code,
                                    Before = x.balance + (x.trans_amount ?? 0),
                                    MinDate = y.MinDate
                                };


            var afterDate = (from x in litSale
                             where x.status == 1
                             group x by new { x.account_code } into g
                             select new AccountDto
                             {
                                 AccountCode = g.Key.account_code,
                                 MaxDate = g.Max(c => c.transDate)
                             }).ToList();


            var afterBalance = from x in litSale
                               join y in afterDate on x.account_code equals y.AccountCode
                               where x.transDate == y.MaxDate
                               select new AccountDto
                               {
                                   AccountCode = x.account_code,
                                   After = x.balance,
                                   MaxDate = y.MaxDate
                               };

            var list = new List<AccountDto>();
            if (accountKpp != null || accountKpp.Count > 0)
            {
                list = (from k in accountKpp
                        join bc in afterBalance on k.account_code equals bc.AccountCode into bcg
                        from c in bcg.DefaultIfEmpty()
                        join bd in beforeBalance on k.account_code equals bd.AccountCode into bdg
                        from d in bdg.DefaultIfEmpty()
                        join ss in sale on k.account_code equals ss.AccountCode into ssg
                        from s in ssg.DefaultIfEmpty()
                        join tn in transferIn on k.account_code equals tn.AccountCode into tng
                        from input in tng.DefaultIfEmpty()
                        join to in transferOut on k.account_code equals to.AccountCode into tog
                        from output in tog.DefaultIfEmpty()
                        select new AccountDto
                        {
                            AccountCode = k.account_code,
                            AccountType = k.account_type,
                            Before = d != null ? d.Before : 0,
                            After = c != null ? c.After : 0,
                            Payment = s != null ? s.Payment : 0,
                            Input = input != null ? input.Input : 0,
                            Transfer = output != null ? output.Transfer : 0,
                            MinDate = d != null ? d.MinDate : DateTime.Now.Date.AddDays(-1),
                            MaxDate = c != null ? c.MaxDate : DateTime.Now.Date,
                        }).ToList();
            }
            else
            {
                list = (from a in afterBalance
                        join b in beforeBalance on a.AccountCode equals b.AccountCode
                        join p in sale on a.AccountCode equals p.AccountCode
                        join i in transferIn on a.AccountCode equals i.AccountCode into gi
                        from input in gi.DefaultIfEmpty()
                        join o in transferOut on a.AccountCode equals o.AccountCode into go
                        from output in go.DefaultIfEmpty()
                        select new AccountDto
                        {
                            AccountCode = a.AccountCode,
                            Before = b.Before,
                            After = a.After,
                            Payment = p.Payment,
                            Input = input != null ? input.Input : 0,
                            Transfer = output != null ? output.Transfer : 0,
                            MinDate = b.MinDate,
                            MaxDate = a.MaxDate
                        }).ToList();
            }


            foreach (var item in list)
            {
                var maxlit = litTransfer.Where(c =>
                    (c.sender == item.AccountCode || c.receiver == item.AccountCode) && c.status == 1 &&
                    c.TransDate > item.MaxDate).ToList();
                var receiverAmountMax = maxlit.Where(c => c.receiver == item.AccountCode).Sum(c => c.amount);
                var sendAmountMax = maxlit.Where(c => c.sender == item.AccountCode).Sum(c => c.amount);

                var minlit = litTransfer.Where(c =>
                    (c.sender == item.AccountCode || c.receiver == item.AccountCode) && c.status == 1 &&
                    c.TransDate < item.MinDate).ToList();
                var receiverAmountMin = minlit.Where(c => c.receiver == item.AccountCode).Sum(c => c.amount);
                var sendAmountMin = minlit.Where(c => c.sender == item.AccountCode).Sum(c => c.amount);

                if (item.AccountType == "DLC1" || item.AccountType == "DLC2")
                {
                    item.After = 0;
                    item.Before = 0;
                }
                else
                {
                    item.After = item.After - sendAmountMax + receiverAmountMax;
                    item.Before = item.Before - receiverAmountMin + sendAmountMin;
                }
                item.Deviation = item.Before + item.Input - item.Transfer - item.Payment - item.After;
                if (item.Deviation == 0)
                    item.Status = "Khop";
                else item.Status = "Lech";
            }

            var listFlast = (from x in list
                             select new AccountDto
                             {
                                 AccountCode = x.AccountCode,
                                 After = (x.AccountType == "DLC1" || x.AccountType == "DLC2") ? 0 : x.After,
                                 Before = (x.AccountType == "DLC1" || x.AccountType == "DLC2") ? 0 : x.Before,
                                 Input = x.Input,
                                 Payment = x.Payment,
                                 Transfer = x.Transfer,
                                 Deviation = x.Before + x.Input - x.Transfer - x.Payment - x.After,
                                 Status = x.Before + x.Input - x.Transfer - x.Payment - x.After == 0
                                     ? "Khop"
                                     : "Lech"
                             }).ToList();

            var litNT = listFlast.Where(c => c.AccountCode.StartsWith("DLC1") || c.AccountCode.StartsWith("DLC2") || c.AccountCode.StartsWith("DLC3")).ToList();
            var litNT_TS = listFlast.Where(c => !c.AccountCode.StartsWith("DLC1") && !c.AccountCode.StartsWith("DLC2") && !c.AccountCode.StartsWith("DLC3")).ToList();

            string headder = "STT,Ma dai ly KPP,So du dau ky,So tien nhan,So tien chuyen,So tien ban hang,So du cuoi ky,Chenh lech,Trang thai";
            string fileName = $"KPP.ACCOUNT.{date.ToString("yyyyMMdd")}.{DateTime.Now.ToString("HHmm")}.csv";
            string fileName_TS = $"KPP.ACCOUNT_TS.{date.ToString("yyyyMMdd")}.{DateTime.Now.ToString("HHmm")}.csv";
            var pathSave = $"{sourcePath.PathName}/{fileName}";
            var pathSave_TS = $"{sourcePath.PathName}/{fileName_TS}";
            IsWriteFileAccountCsv(headder, pathSave, litNT);
            IsWriteFileAccountCsv(headder, pathSave_TS, litNT_TS);
            await _mongoRepository.SysAccountKppBalance(listFlast, date);
            var fileNameXls = $"KPP.ACCOUNT.{date.ToString("yyyyMMdd")}.{DateTime.Now.ToString("HHmm")}.xls" ;
            var fileNameXls_TS = $"KPP.ACCOUNT_TS.{date.ToString("yyyyMMdd")}.{DateTime.Now.ToString("HHmm")}.xls";

            var excel = _dataExcel.KppExportToFile(litNT, fileNameXls);
            var excel_TS = _dataExcel.KppExportToFile(litNT_TS, fileNameXls_TS);
            if (excel != null)
            {
                _log.LogInformation($"KppExportToFile: {(!string.IsNullOrEmpty(excel.FileToken) ? "FileToken_Data" : "null")}");
                var fileBytes = await _cacheManager.GetFile(excel.FileToken);
                _log.LogInformation($"KppExportToFile : {(fileBytes != null ? "FileBytes_Data" : "null")}");
                if (fileBytes != null)
                {
                    var pathSaveSlx = $"{sourcePath.PathName}/{fileNameXls}";
                    await File.WriteAllBytesAsync(pathSaveSlx, fileBytes);
                }
            }

            if (excel_TS != null)
            {
                _log.LogInformation($"KppExportToFile: {(!string.IsNullOrEmpty(excel_TS.FileToken) ? "FileToken_Data" : "null")}");
                var fileBytes = await _cacheManager.GetFile(excel_TS.FileToken);
                _log.LogInformation($"KppExportToFile : {(fileBytes != null ? "FileBytes_Data" : "null")}");
                if (fileBytes != null)
                {
                    var pathSaveSlx = $"{sourcePath.PathName}/{fileNameXls_TS}";
                    await File.WriteAllBytesAsync(pathSaveSlx, fileBytes);
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _log.LogError($"ExportFileAccount Exception: {ex.Message}|{ex.StackTrace}|{ex.InnerException}");
            return false;
        }
    }


    public async Task<MessageResponseBase> KppAccount(string accountCode)
    {
        var fileAccount = await _kppRepository.GetKppAccount(accountCode);
        return new MessageResponseBase
        {
            ResponseCode = ResponseCodeConst.Success,
            ResponseMessage = "Thành công",
            ExtraInfo = fileAccount.ToJson(),
            Payload = fileAccount
        };
    }

    #endregion

    private List<TransactionView> ChangerBalance(List<TransactionView> list)
    {
        try
        {
            var lst = list.Where(c => c.status == 1 && c.balance == 0).ToList();
            var lstTransInfo = new List<TransKppInfo>();
            foreach (var item in lst)
            {
                var tmpList = list.Where(c => c.account_code == item.account_code).OrderBy(c => c.transDate).ToList();
                decimal tmpBalance = 0;
                foreach (var x in tmpList)
                {
                    if (x.balance == 0)
                    {
                        if (tmpBalance != 0)
                        {
                            lstTransInfo.Add(new TransKppInfo()
                            {
                                AccountCode = x.account_code,
                                TransCode = x.trans_code,
                                Balance = (tmpBalance - x.trans_amount ?? 0),
                            });
                            x.balance = (tmpBalance - x.trans_amount ?? 0);
                            tmpBalance = x.balance;
                        }
                    }
                }
            }

            if (lstTransInfo.Count > 0)
            {
                foreach (var x in list)
                {
                    if (x.balance == 0 && x.status == 1)
                    {
                        var f = lstTransInfo.FirstOrDefault(c => c.AccountCode == x.account_code && c.TransCode == x.trans_code);
                        if (f != null)
                            x.balance = f.Balance;
                    }
                }
            }

            return list;
        }
        catch (Exception ex)
        {
            _log.LogError($"ChangerBalance Exception: {ex}");
            return list;
        }
    }
    private async Task<List<Transaction>> ChangeTransaction(List<Transaction> dataPayment)
    {

        var check = dataPayment.Where(c => c.trans_amount == 0 && (c.status == 1 || c.status == 0)).Count();
        if (check > 0)
        {
            var accounts = await _kppRepository.GetKppAccount("");
            foreach (var item in dataPayment)
            {
                if (item.trans_amount == 0)
                {
                    if ((item.discount_rate ?? 0) != 0)
                        item.trans_amount = Math.Round(item.amount * (1 - (item.discount_rate ?? 0) / 100), 0);
                    else
                    {
                        var ac = accounts.Where(c => c.account_code == item.account_code).FirstOrDefault();
                        if (ac != null)
                            item.trans_amount = Math.Round(item.amount * (1 - ac.airtime_discount_rate / 100), 0);
                    }
                }
            }
        }

        return dataPayment;
    }

    private async Task<IntFile> GetForderCreate(string key, string extension = "")
    {
        try
        {
            var dto = new IntFile
            {
                Folder = DateTime.Now.ToString("yyMMdd_HHmmss"),
            };
            var sourcePath = Path.Combine("", $"KppFiles/{key}/{dto.Folder}");
            if (!Directory.Exists(sourcePath))
                Directory.CreateDirectory(sourcePath);

            dto.PathName = sourcePath;
            dto.FileZip = dto.Folder + (string.IsNullOrEmpty(extension) ? ".rar" : extension);
            dto.KeySouce = key;
            return dto;
        }
        catch (Exception ex)
        {
            _log.LogError($"key={key} getForderCreate Exception: {ex.Message}|{ex.InnerException}|{ex.InnerException}");
            return null;
        }
    }
    private async Task<string> ZipForderCreate(IntFile sourceFile)
    {
        try
        {
            var desPath = Path.Combine("", $"KppFiles/{ReportConst.ZIP}/{sourceFile.KeySouce}");
            if (!Directory.Exists(desPath))
                Directory.CreateDirectory(desPath);
            ZipFile.CreateFromDirectory($"{sourceFile.PathName}", $"{desPath}/{sourceFile.FileZip}");
            await DeleteFileNow(string.Empty, sourceFile.PathName);
            return desPath + "/" + sourceFile.FileZip;
        }
        catch (Exception ex)
        {
            _log.LogError($"ZipForderCreate Exception: {ex.Message}|{ex.InnerException}|{ex.InnerException}");
            return string.Empty;
        }
    }
    private async Task DeleteFileNow(string key, string sourceFile)
    {
        try
        {
            if (!string.IsNullOrEmpty(key))
            {
                var sourcePath = Path.Combine("", $"KppFiles/{key}");
                if (Directory.Exists(sourcePath))
                {

                    foreach (var note in Directory.GetDirectories(sourcePath))
                    {
                        foreach (var note2 in Directory.GetDirectories(note))
                        {
                            if (Directory.Exists(note2))
                            {
                                await DeleteFileInt(note2);
                                Directory.Delete(note2);
                            }
                        }

                        if (Directory.Exists(note))
                        {
                            await DeleteFileInt(note);
                            Directory.Delete(note);
                        }
                    }

                    await DeleteFileInt(sourcePath);
                    Directory.Delete(sourcePath);
                }
            }

            if (!string.IsNullOrEmpty(sourceFile))
            {
                if (Directory.Exists(sourceFile))
                {
                    await DeleteFileInt(sourceFile);
                    Directory.Delete(sourceFile);
                }
            }

        }
        catch (Exception ex)
        {
            _log.LogError($"DeleteFileNow Exception: {ex.Message}|{ex.InnerException}|{ex.InnerException}");
        }
    }
    private async Task DeleteFileInt(string sourceFile)
    {
        try
        {
            foreach (var f in Directory.GetFiles(sourceFile))
            {
                if (File.Exists(f))
                    File.Delete(f);
            }
        }
        catch (Exception ex)
        {
            _log.LogError($"DeleteFileInt Exception: {ex.Message}|{ex.InnerException}|{ex.InnerException}");
        }
    }
}