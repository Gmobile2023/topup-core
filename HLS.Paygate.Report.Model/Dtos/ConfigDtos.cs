namespace HLS.Paygate.Report.Model.Dtos;

public class SendMailStockMinInventoryDto
{
    public bool IsSendMail { get; set; }
    public bool IsBotMessage { get; set; }
    public int TimeReSend { get; set; }
    public int SendCount { get; set; }
    public string EmailReceive { get; set; }
}

public class ReportFile
{
    public string PathName { get; set; }
    public string Folder { get; set; }
    public string KeySouce { get; set; }
    public string FileZip { get; set; }
}

