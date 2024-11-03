using System.Threading.Tasks;

namespace HLS.Paygate.Gw.Domain.Services;

public interface IBackgroundService
{
    Task AutoCheckTrans();
    Task CheckLastTrans();
    // Task CheckAutoCloseProvider();

    Task AutoCheckGateTrans();
}