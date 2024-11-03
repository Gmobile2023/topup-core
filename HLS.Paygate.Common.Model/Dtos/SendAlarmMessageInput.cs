using HLS.Paygate.Shared;
using Paygate.Contracts.Requests.Commons;

namespace HLS.Paygate.Common.Model.Dtos;

public class SendAlarmMessageInput
{
    public string Title { get; set; }
    public string Message { get; set; }
    public string Module { get; set; }
    public string Code { get; set; }
    public long ChatId { get; set; }
    public BotMessageType MessageType { get; set; }
    public BotType BotType { get; set; }
}