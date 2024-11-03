namespace Paygate.Contracts.Commands.Commons;

public interface AccountActivitiesCommand
{
    string AccountCode { get; set; }
    string FullName { get; set; }
    int AccountType { get; set; }
    int AgentType { get; set; }
    string PhoneNumber { get; set; }
    string UserName { get; set; }
    string Note { get; set; }
    string Payload { get; set; }
    string SrcValue { get; set; }
    string DesValue { get; set; }
    string Attachment { get; set; }
    byte AccountActivityType { get; set; }
}