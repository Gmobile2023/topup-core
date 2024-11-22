using System;

namespace Topup.Report.Model.Dtos.ResponseDto;

public class ValueTeam
{
    public double Value { get; set; }
}

public class ValueTeamDate
{
    public DateTime Value { get; set; }
    public DateTime ValueAsString { get; set; }
}