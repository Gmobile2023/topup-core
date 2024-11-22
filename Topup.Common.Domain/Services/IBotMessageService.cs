using System.Threading.Tasks;
using Topup.Common.Model.Dtos;
using Topup.Contracts.Requests.Commons;

namespace Topup.Common.Domain.Services;

public interface IBotMessageService
{
    Task<bool> SendAlarmMessage(SendAlarmMessageInput input);
}