using System.Collections.Generic;

namespace Topup.Shared.ConfigDtos;

public class WorkerConfig
{
    public int TimeOutProcess { get; set; }
    public bool IsEnableCheckMobileSystem { get; set; }
    public int TimeoutCheckMobile { get; set; }
    public bool IsTest { get; set; }
    public bool IsCheckLimit { get; set; }
    public bool IsEnableResponseCode { get; set; }
    public string PartnerAllowResponseConfig { get; set; }
    public string ErrorCodeRefund { get; set; }
    public int MaxNumOfParallelBackgroundOperations { get; set; }
}