namespace HLS.Paygate.TopupGw.Contacts.Dtos;

public class ProviderReponseDto : DocumentDto
{
    public string Provider { get; set; }
    public string ReponseCode { get; set; }
    public string ReponseName { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
}