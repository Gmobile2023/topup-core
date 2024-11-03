using System.Threading.Tasks;

namespace GMB.Topup.Gw.Domain.Services;

public interface IBackgroundService
{
    Task AutoCheckTrans();
    Task CheckLastTrans();
    // Task CheckAutoCloseProvider();

    Task AutoCheckGateTrans();
}