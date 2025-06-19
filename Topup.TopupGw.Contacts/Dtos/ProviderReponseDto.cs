namespace Topup.TopupGw.Contacts.Dtos;

public class ProviderReponseDto : DocumentDto
{
    public string Provider { get; set; }
    public string ResponseCode { get; set; }
    public string ResponseName { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
}