﻿using System.Collections.Generic;

namespace Topup.Shared.ConfigDtos;

public class RedisSentinelConfig
{
    public bool IsEnable { get; set; }
    public bool IsSentinel { get; set; }
    public List<string> SentinelHosts { get; set; }
    public string MasterName { get; set; }
}