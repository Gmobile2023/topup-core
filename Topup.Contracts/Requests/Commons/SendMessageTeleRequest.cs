namespace Topup.Contracts.Requests.Commons
{
    public class SendBotMessageRequest
    {
        public string Title { get; set; }
        public string Message { get; set; }
        public string Module { get; set; }
        public string Code { get; set; }
        public BotMessageType MessageType { get; set; }
        public BotType BotType { get; set; }
    }

    public class SendBotMessageToGroupRequest : SendBotMessageRequest
    {
        public string ChatId { get; set; }
    }

    public enum BotMessageType
    {
        Error = 1,
        Wraning = 2,
        Message = 3
    }

    public enum BotType
    {
        Dev = 1,
        Sale = 2,
        CardMapping = 3,
        Provider = 4,
        Transaction = 5,
        Stock = 6,
        Deposit = 7,
        Channel = 8,
        Private = 9,
        Compare = 10
    }
}
