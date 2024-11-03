using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace HLS.Paygate.Kpp.Domain.Services;

public class AutoKppService : IAutoKppService
{
    private readonly IExportingService _exportingService;

    private readonly ILogger<AutoKppService> _log;
 

    public AutoKppService(IExportingService exportingService,
        ILogger<AutoKppService> log)
    {
        _log = log;
        _exportingService = exportingService;
    }

    #region Xuất file hàng ngày chốt dữ liệu chi tiết

    public async Task SysAutoFile()
    {              
        _log.LogInformation("Start SysAutoFilePayment Process");
        await _exportingService.ProcessKppFile(System.DateTime.Now.Date.AddDays(-1));        
    }

    #endregion
}