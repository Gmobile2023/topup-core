using System.Threading.Tasks;
using GMB.Topup.Common.Model.Dtos;
using GMB.Topup.Contracts.Requests.Commons;

namespace GMB.Topup.Common.Domain.Services;

public interface IBotMessageService
{
    Task<bool> SendAlarmMessage(SendAlarmMessageInput input);
}