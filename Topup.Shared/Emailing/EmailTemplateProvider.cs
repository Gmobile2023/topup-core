using System;
using System.Collections.Concurrent;
using System.Text;
using ServiceStack;
using Topup.Shared.Utils;

namespace Topup.Shared.Emailing;

public class EmailTemplateProvider : IEmailTemplateProvider
{
    private readonly ConcurrentDictionary<string, string> _defaultTemplates;

    public EmailTemplateProvider()
    {
        _defaultTemplates = new ConcurrentDictionary<string, string>();
    }

    public string GetTemplateByName(string templeateName)
    {
        return _defaultTemplates.GetOrAdd("Paygate", key =>
        {
            using var stream = typeof(EmailTemplateProvider).Assembly
                .GetManifestResourceStream("Topup.Shared.Emailing.EmailTemplates." + templeateName + ".html");
            var bytes = new StreamUtil().ReadFully(stream);
            var template = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
            return template.Replace("{THIS_YEAR}", DateTime.Now.Year.ToString());
            //template = template.Replace("{LINK_WEB_SITE}", "");
            //return template.Replace("{EMAIL_LOGO_URL}", "");
        });
    }
}