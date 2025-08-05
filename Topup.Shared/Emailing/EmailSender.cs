using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace Topup.Shared.Emailing;

public class EmailSender : IEmailSender
{
    private readonly IConfiguration _configuration;

    private readonly IEmailTemplateProvider _emailTemplateProvider;

    //private readonly ILog _logger = LogManager.GetLogger("EmailSender");
    private readonly ILogger<EmailSender> _logger;

    public EmailSender(IEmailTemplateProvider emailTemplateProvider, IConfiguration configuration,
        ILogger<EmailSender> logger)
    {
        _emailTemplateProvider = emailTemplateProvider;
        _configuration = configuration;
        _logger = logger;
    }

    public bool SendEmailNotificationInventoryStock(List<string> emails, string stockCode, string stockType,
        string productCode,
        int inventory)
    {
        try
        {
            var emailTemplate = GetTemplateMail(
                $"Kho thẻ {stockCode} nhà mạng {stockType} sản phẩm {productCode} sắp hết",
                $"Kho thẻ {stockCode} nhà mạng {stockType} sản phẩm {productCode} sắp hết".ToUpper(), "default");
            var mailMessage = new StringBuilder();
            var msgBody =
                $"Kho thẻ {stockCode} nhà mạng {stockType} sản phẩm {productCode} sắp hết. Tồn kho hiện tại còn: {inventory}. Vui lòng bổ sung thêm thẻ vào kho";
            mailMessage.AppendLine(msgBody);
            foreach (var item in emails)
                _ = ReplaceBodyAttachmentsAndSend(item,
                    $"Email cảnh báo kho thẻ {stockCode}-{stockType}-{productCode} sắp hết", emailTemplate,
                    mailMessage);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"SendEmailNotificationInventoryStock error: {ex}");
            return false;
        }
    }

    public bool SendEmailNotificationMinBalanceAccount(List<string> emails, string accountCode, decimal balance)
    {
        try
        {
            var emailTemplate = GetTemplateMail($"Cảnh bảo tài số dư {accountCode} sắp hết",
                $"Số dư tài khoản {accountCode} sắp hết".ToUpper(), "default");
            var mailMessage = new StringBuilder();
            var msgBody =
                $"Tài khoản {accountCode} có số dư sắp hết. Số dư hiện tại tại còn: {balance}. Vui lòng nạp tiền vào tài khoản để giao dịch không bị gián đoạn";
            mailMessage.AppendLine(msgBody);
            foreach (var item in emails)
                ReplaceBodyAttachmentsAndSend(item, "Email cảnh báo số dư tài khoản sắp hết", emailTemplate,
                    mailMessage);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"SendEmailNotificationMinBalanceAccount error: {ex}");
            return false;
        }
    }

    public bool SendEmailReportAuto(List<string> emails, string title, string msgBody, string linkAddtach = "")
    {
        try
        {
            var emailTemplate = new StringBuilder();
            emailTemplate.Append(msgBody);
            var mailMessage = new StringBuilder();
            mailMessage.AppendLine(msgBody);
            foreach (var item in emails)
                _ = ReplaceBodyAttachmentsAndSend(item, title, emailTemplate, mailMessage,
                    !string.IsNullOrEmpty(linkAddtach) ? new[] { linkAddtach } : null);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"SendEmailReportAuto error: {ex}");
            return false;
        }
    }

    private StringBuilder GetTemplateMail(string title, string subTitle, string teamName)
    {
        var emailTemplate = new StringBuilder(_emailTemplateProvider.GetTemplateByName(teamName));
        emailTemplate.Replace("{EMAIL_TITLE}", title);
        emailTemplate.Replace("{EMAIL_SUB_TITLE}", subTitle);
        return emailTemplate;
    }

    // private void ReplaceBodyAttachmentsAndSend(string emailAddress, string subject,
    //     StringBuilder emailTemplate,
    //     StringBuilder mailMessage, IReadOnlyCollection<string> pathAttachments = null)
    // {
    //     try
    //     {
    //         var client = new SmtpClient(_configuration["EmailConfig:SmtpServer"])
    //         {
    //             UseDefaultCredentials = false,
    //             Credentials = new NetworkCredential(_configuration["EmailConfig:EmailAddress"],
    //                 _configuration["EmailConfig:EmailPassword"]),
    //             Port = int.Parse(_configuration["EmailConfig:Port"]),
    //             EnableSsl = bool.Parse(_configuration["EmailConfig:EnableSsl"])
    //         };
    //         emailTemplate.Replace("{EMAIL_BODY}", mailMessage.ToString());
    //         var mess = new MailMessage
    //         {
    //             To = { emailAddress },
    //             Subject = subject,
    //             Body = emailTemplate.ToString(),
    //             IsBodyHtml = true,
    //             From = new MailAddress(_configuration["EmailConfig:EmailAddress"],
    //                 _configuration["EmailConfig:EmailDisplay"])
    //         };
    //         if (pathAttachments != null && pathAttachments.Any())
    //             foreach (var item in pathAttachments)
    //                 mess.Attachments.Add(new Attachment(item));
    //
    //         try
    //         {
    //             client.Send(mess);
    //         }
    //         catch (Exception e)
    //         {
    //             throw;
    //         }
    //         finally
    //         {
    //             mess.Dispose();
    //             client.Dispose();
    //         }
    //     }
    //     catch (Exception ex)
    //     {
    //         _logger.LogError($"{emailAddress} ReplaceBodyAttachmentsAndSend error: {ex}");
    //     }
    // }
    private async Task ReplaceBodyAttachmentsAndSend(
        string emailAddress,
        string subject,
        StringBuilder emailTemplate,
        StringBuilder mailMessage,
        IReadOnlyCollection<string> pathAttachments = null)
    {
        try
        {
            var message = new MimeMessage();

            // From
            message.From.Add(new MailboxAddress(
                _configuration["EmailConfig:EmailDisplay"],
                _configuration["EmailConfig:EmailAddress"]
            ));

            // To
            message.To.Add(MailboxAddress.Parse(emailAddress));
            message.Subject = subject;

            // Body
            emailTemplate.Replace("{EMAIL_BODY}", mailMessage.ToString());

            var builder = new BodyBuilder
            {
                HtmlBody = emailTemplate.ToString()
            };

            // Attachments
            if (pathAttachments != null && pathAttachments.Any())
            {
                foreach (var filePath in pathAttachments)
                {
                    builder.Attachments.Add(filePath);
                }
            }

            message.Body = builder.ToMessageBody();

            // SMTP client
            using var client = new SmtpClient();

            // Optional: override HELO name (fix "Invalid HELO name" error)
            client.LocalDomain = _configuration["EmailConfig:LocalDomain"]; // bạn có thể sửa theo nhu cầu

            var smtpServer = _configuration["EmailConfig:SmtpServer"];
            var port = int.Parse(_configuration["EmailConfig:Port"]);
            var email = _configuration["EmailConfig:EmailAddress"];
            var password = _configuration["EmailConfig:EmailPassword"];
            var enableSsl = bool.Parse(_configuration["EmailConfig:EnableSsl"]);

            await client.ConnectAsync(smtpServer, port,
                enableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None);
            await client.AuthenticateAsync(email, password);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            _logger.LogError($"{emailAddress} ReplaceBodyAttachmentsAndSendAsync error: {ex}");
        }
    }
}