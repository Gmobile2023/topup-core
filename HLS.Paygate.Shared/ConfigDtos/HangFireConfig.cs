﻿namespace HLS.Paygate.Shared.ConfigDtos;

public class HangFireConfig
{
    public bool IsRun { get; set; }
    public bool EnableHangfire { get; set; }
    public string ServerName { get; set; }
}