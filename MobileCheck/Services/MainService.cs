using System.Threading.Tasks;
using MobileCheck.Services;
using Topup.Discovery.Requests.Tool;
using ServiceStack;

namespace MobileCheck.Services;

public class MainService : Service
{
    private readonly CheckMobileProcess _checkMobileProcess;

    public MainService(CheckMobileProcess checkMobileProcess)
    {
        _checkMobileProcess = checkMobileProcess;
    }

    public async Task<object> GetAsync(MobileInfoRequest request)
    {
        return await _checkMobileProcess.CheckMobile(request.MsIsdn, request.Telco);
    }
}