using System;

namespace Topup.Contracts.Commands.Commissions;

public interface CommissionReportCommand : ICommand
{
    int Type { get; set; }
    string ParentCode { get; set; }
    string TransCode { get; set; }
    string CommissionCode { get; set; }
    decimal CommissionAmount { get; set; }
    DateTime? CommissionDate { get; set; }
    int Status { get; set; }
}