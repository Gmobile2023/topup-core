using Topup.Contracts.Requests.Commons;

namespace Topup.Contracts.Commands.Commons;

public interface SendBotMessage : ICommand
{
    string Title { get; set; }
    string Message { get; set; }
    string Module { get; set; }
    string Code { get; set; }
    BotMessageType MessageType { get; set; }
    BotType BotType { get; set; }
    string ChatId { get; set; }
}

public interface SendBotMessageToGroup : ICommand
{
    string ChatId { get; set; }
    string Title { get; set; }
    string Message { get; set; }
    string Module { get; set; }
    string Code { get; set; }
    BotMessageType MessageType { get; set; }
    BotType BotType { get; set; }
}