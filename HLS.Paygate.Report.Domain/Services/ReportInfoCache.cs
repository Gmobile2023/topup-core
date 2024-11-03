using System.Collections.Generic;
using HLS.Paygate.Report.Domain.Entities;

namespace HLS.Paygate.Report.Domain.Services;

public class ReportInfoCache
{
    public static List<ReportAccountDto> Accounts { get; set; }

    public static List<ReportProviderDto> Providers { get; set; }    

    public static List<ReportProductDto> Products { get; set; }

    public static List<ReportServiceDto> Services { get; set; }

    public static List<ReportVenderDto> Venders { get; set; }
}