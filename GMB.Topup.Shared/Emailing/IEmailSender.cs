using System.Collections.Generic;

namespace GMB.Topup.Shared.Emailing;

public interface IEmailSender
{
    bool SendEmailNotificationInventoryStock(List<string> emails, string stockCode, string stockType,
        string productCode,
        int inventory);

    bool SendEmailNotificationMinBalanceAccount(List<string> emails, string accountCode, decimal balance);

    bool SendEmailReportAuto(List<string> emails, string title, string msgBody, string linkAddtach = "");
}