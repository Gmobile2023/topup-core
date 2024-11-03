namespace Infrastructure.Logging;

public class LoggingConfigDto
{
    public string LogServer { get; set; }
    public string LogFileUrl { get; set; }
    public string OutputTemplate { get; set; }
    public string RetainedFileCountLimit { get; set; }
    public bool AutoRegisterTemplate { get; set; }
    public string Application { get; set; }
    public string IndexFormat { get; set; }
    public string UserName { get; set; }
    public string Password { get; set; }
    public bool IsDisableElk { get; set; }
}