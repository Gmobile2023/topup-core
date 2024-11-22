namespace Topup.Shared.Emailing;

public interface IEmailTemplateProvider
{
    string GetTemplateByName(string templeateName);
}