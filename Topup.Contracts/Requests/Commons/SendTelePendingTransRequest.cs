namespace Topup.Contracts.Requests.Commons;

public class SendTeleTrasactionRequest
{
    public string ProviderCode { get; set; }
    public string TransCode { get; set; }
    public string Title { get; set; }
    public string Message { get; set; }
    public string TransRef { get; set; }
    public string ReceiverInfo { get; set; }
    public decimal Amount { get; set; }
    public BotType? BotType { get; set; }
    public BotMessageType? BotMessageType { get; set; }    
}