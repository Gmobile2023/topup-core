using System.Collections.Generic;

namespace GMB.Topup.Contracts.Commands.Commons;

public interface NotificationsSendCommand : ICommand
{
    List<string> ReceivingAccounts { get; }
    string Body { get; }
    string Title { get; }
    string Data { get; }
    string Priority { get; }
    string AppNotificationName { get; }
    string Url { get; }
    string Severity { get; }
}

public interface NotificationSendCommand : ICommand
{
    string ReceivingAccount { get; }
    string Body { get; }
    string Title { get; }
    string Data { get; }
    string Priority { get; }
    string AppNotificationName { get; }
    string Url { get; }
    string Severity { get; }
}