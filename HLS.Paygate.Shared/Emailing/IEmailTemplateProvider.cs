namespace HLS.Paygate.Shared.Emailing;

public interface IEmailTemplateProvider
{
    string GetTemplateByName(string templeateName);
}