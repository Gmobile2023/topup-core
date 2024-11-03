using System.Threading.Tasks;
using HLS.Paygate.Common.Model.Dtos;
using Paygate.Contracts.Requests.Commons;

namespace HLS.Paygate.Common.Domain.Services;

public interface IBotMessageService
{
    Task<bool> SendAlarmMessage(SendAlarmMessageInput input);
}