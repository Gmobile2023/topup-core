using System.Collections.Generic;

namespace Topup.Shared.ConfigDtos;

public class BotConfig
{
    public string Url { get; set; }
    public string BotName { get; set; }
    public string Token { get; set; }
    public long DefaultChatId { get; set; }
    public List<ChatIdConfig> ChatIds { get; set; }
}

public class ChatIdConfig
{
    public long ChatId { get; set; }
    public string BotType { get; set; }
}