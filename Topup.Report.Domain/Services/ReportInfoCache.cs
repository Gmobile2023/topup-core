﻿using System.Collections.Generic;
using Topup.Report.Domain.Entities;

namespace Topup.Report.Domain.Services;

public class ReportInfoCache
{
    public static List<ReportAccountDto> Accounts { get; set; }

    public static List<ReportProviderDto> Providers { get; set; }    

    public static List<ReportProductDto> Products { get; set; }

    public static List<ReportServiceDto> Services { get; set; }

    public static List<ReportVenderDto> Venders { get; set; }
}