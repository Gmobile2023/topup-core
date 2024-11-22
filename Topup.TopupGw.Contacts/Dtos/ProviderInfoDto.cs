using System.Collections.Generic;

namespace Topup.TopupGw.Contacts.Dtos;

public class ProviderInfoDto : DocumentDto
{
    public string ProviderCode { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public string ApiUrl { get; set; }
    public string ApiUser { get; set; }
    public string ApiPassword { get; set; }
    public string ExtraInfo { get; set; }
    public List<ProviderServiceDto> ProviderServices { get; set; }
    public int Timeout { get; set; }
    public int TimeoutProvider { get; set; }
    public string PrivateKeyFile { get; set; }
    public string PublicKeyFile { get; set; }
    public string PublicKey { get; set; }
    public int TotalTransError { get; set; }
    public int TimeClose { get; set; }
    public bool IsAutoCloseFail { get; set; }
    public string IgnoreCode { get; set; }
    public int TimeScan { get; set; }
    public int TotalTransScan { get; set; }
    public int TotalTransDubious { get; set; }
    public int TotalTransErrorScan { get; set; }
    public string ParentProvider { get; set; }
    public bool IsAlarm { get; set; }//Bật cảnh báo
    public string ErrorCodeNotAlarm { get; set; }//Bỏ qua các mã lỗi không cảnh báo
    public string MessageNotAlarm { get; set; }//Bỏ qua các message lỗi không cảnh báo
    public string AlarmChannel { get; set; }
    public string AlarmTeleChatId { get; set; }
    public int ProcessTimeAlarm { get; set; }//Cảnh báo thời gian xử lý giao dịch
}

public class ProviderServiceDto
{
    public string ServiceCode { get; set; }
    public string ProductCode { get; set; }
    public string ServiceName { get; set; }
}