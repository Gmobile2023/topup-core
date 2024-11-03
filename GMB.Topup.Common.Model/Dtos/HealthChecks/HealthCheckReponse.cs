using System;
using System.Collections.Generic;

namespace GMB.Topup.Common.Model.Dtos.HealthChecks;

public class HealthCheckReponse
{
    public string Status { get; set; }
    public IEnumerable<HealthCheck> Checks { get; set; }
    public TimeSpan Duration { get; set; }
}