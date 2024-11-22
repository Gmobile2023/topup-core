using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Topup.Report.Domain.Services;

public interface IAutoReportService
{
    Task SysJobReport();

    Task SysJobBalanceReport();

    Task SysJobTest();
}

public class AutoReportService : IAutoReportService
{
    private readonly IExportingService _exportingService;
    private readonly ILogger<AutoReportService> _log;
    private static bool _isProcess;
    private static bool _isBalanceProcess;

    public AutoReportService(IExportingService exportingService,
        ILogger<AutoReportService> log)
    {
        _log = log;
        _exportingService = exportingService;
    }

    #region 1.Job chạy dữ liệu

    public async Task SysJobReport()
    {
        if (_isProcess) return;
        _isProcess = true;
        var date = DateTime.Now;
        //var minute = 60000;
        try
        {
            await _exportingService.ProcessDataCheck(date.Date.AddDays(-1));
            //await Task.Delay(minute);
            await _exportingService.ProcessReportNXT();
            // await Task.Delay(minute);
            await _exportingService.ProcessTotalRevenue();
            //await Task.Delay(minute);
           // await _exportingService.ProcessBalanceSupplier();
            //await Task.Delay(minute);
            await _exportingService.ProcessBatchFile();
            //await Task.Delay(minute);
            await _exportingService.ProcessBatchData();
            //await Task.Delay(minute);
            await _exportingService.ProcessWarning();
            //await Task.Delay(minute);
            await _exportingService.ProcessCompareAgentPartner();
            //await Task.Delay(minute);
            var dateProcess = DateTime.Now.Date.AddDays(-1);
            await _exportingService.ProcessCompareSystemAccount(dateProcess);
            if (date.Day == 1)
            {
                //await Task.Delay(minute);
                await _exportingService.ProcessSms();
                await _exportingService.SysDayOneProcess();
            }

            await _exportingService.SysDeleteFileFpt();
        }
        catch (Exception ex)
        {
            _log.LogError($"SysJobReport_Exception: {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
        }
        _isProcess = false;
    }

    public async Task SysJobBalanceReport()
    {
        if (_isBalanceProcess) return;
        _isBalanceProcess = true;           
        try
        {           
            await _exportingService.ProcessBalanceSupplier();          
        }
        catch (Exception ex)
        {
            _log.LogError($"SysJobBalanceReport_Exception: {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
        }
        _isBalanceProcess = false;
    }

    public async Task SysJobTest()
    {             
        try
        {          
            await _exportingService.ProcessReportNXT();           
        }
        catch (Exception ex)
        {
            _log.LogError($"SysJobTest_Exception: {ex.Message}|{ex.InnerException}|{ex.StackTrace}");
        }
        _isProcess = false;
    }

    #endregion   
}