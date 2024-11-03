using System;

namespace GMB.Topup.Report.Model.Dtos;

public class ReportItemTotalDay
{
    public DateTime CreatedDay { get; set; }
    public double BalanceBefore { get; set; }
    public double IncDeposit { get; set; }
    public double IncOther { get; set; }
    public double DecPayment { get; set; }
    public double DecOther { get; set; }
    public double BalanceAfter { get; set; }
}

public class ReportItemTotalDebt
{
    public string SaleCode { get; set; }
    public string SaleInfo { get; set; }
    public double BalanceBefore { get; set; }
    public double IncDeposit { get; set; }
    public double DecPayment { get; set; }
    public double BalanceAfter { get; set; }
}

public class ReportTotalTempDebt : ReportItemTotalDebt
{
    public DateTime MinDate { get; set; }

    public DateTime MaxDate { get; set; }

    public DateTime CreatedDay { get; set; }

    public double LimitBefore { get; set; }

    public double LimitAfter { get; set; }
}

public class ReportCommissionDetailDto
{
    public string AgentSumCode { get; set; }
    public string AgentSumInfo { get; set; }
    public double CommissionAmount { get; set; }
    public string CommissionCode { get; set; }
    public int Status { get; set; }
    public string StatusName { get; set; }

    public string AgentCode { get; set; }

    public string AgentInfo { get; set; }

    public string TransCode { get; set; }

    public string RequestRef { get; set; }

    public string ServiceCode { get; set; }

    public string ServiceName { get; set; }

    public string CategoryName { get; set; }

    public string ProductName { get; set; }

    public DateTime CreateDate { get; set; }

    public DateTime? PayDate { get; set; }
}

public class ReportCommissionAgentDetailDto
{
    public string AgentCode { get; set; }

    public string AgentInfo { get; set; }

    public string RequestRef { get; set; }

    public string TransCode { get; set; }

    public string CommissionCode { get; set; }

    public double CommissionAmount { get; set; }

    public int StatusPayment { get; set; }

    public string StatusPaymentName { get; set; }

    public string ServiceCode { get; set; }

    public string ServiceName { get; set; }

    public string CategoryName { get; set; }

    public string ProductName { get; set; }

    public int Quantity { get; set; }

    public double Amount { get; set; }

    public double Discount { get; set; }

    public double Fee { get; set; }

    public double Price { get; set; }

    public double TotalPrice { get; set; }

    public int Status { get; set; }
    public string StatusName { get; set; }

    public DateTime CreateDate { get; set; }

    public DateTime? PayDate { get; set; }
}

public class ReportCommissionTotalDto
{
    public string AgentCode { get; set; }
    public string AgentName { get; set; }
    public string Commission { get; set; }
    public double Quantity { get; set; }
    public double CommissionAmount { get; set; }
    public double Payment { get; set; }
    public double UnPayment { get; set; }
}

public class ReportCommissionAgentTotalDto
{
    public string AgentCode { get; set; }
    public string AgentName { get; set; }
    public double Before { get; set; }
    public double AmountUp { get; set; }
    public double AmountDown { get; set; }
    public double After { get; set; }
}

public class ItemMobileCheckDto
{
    public string Mobile { get; set; }
    public string CreatedDate { get; set; }
    public string Provider { get; set; }
    public string ReceiveType { get; set; }
    public double Amount { get; set; }
    public int Count { get; set; }
}