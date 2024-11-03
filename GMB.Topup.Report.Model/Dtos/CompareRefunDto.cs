using System;
using System.Collections.Generic;

namespace GMB.Topup.Report.Model.Dtos;

public class CompareRefunDto
{
    public DateTime TransDate { get; set; }

    public string Provider { get; set; }

    public decimal Quantity { get; set; }

    public decimal Amount { get; set; }

    public int RefundWaitQuantity { get; set; }

    public decimal RefundWaitAmount { get; set; }

    public int RefundQuantity { get; set; }

    public decimal RefundAmount { get; set; }

    public string KeyCode { get; set; }
}

public class CompareRefunDetailDto
{
    public DateTime TransDate { get; set; }

    public string AgentCode { get; set; }

    public string TransCode { get; set; }

    public string TransPay { get; set; }

    public decimal ProductValue { get; set; }

    public decimal Amount { get; set; }

    public string ReceivedAccount { get; set; }
    public string ProductCode { get; set; }
    public string ProductName { get; set; }
    public int Status { get; set; }
    public string StatusName { get; set; }

    public string TransCodeRefund { get; set; }
}

public class CompareReponseDetailDto
{
    public DateTime TransDate { get; set; }

    public string AgentCode { get; set; }

    public string TransCode { get; set; }

    public string TransPay { get; set; }

    public decimal ProductValue { get; set; }

    public decimal Amount { get; set; }

    public string ReceivedAccount { get; set; }
    public string ProductCode { get; set; }
    public string ProductName { get; set; }
}

public class CompareReponseDto
{
    public string CompareType { get; set; }

    public decimal Quantity { get; set; }

    public decimal AmountSys { get; set; }

    public decimal AmountProvider { get; set; }

    public decimal Deviation { get; set; }

    public decimal Amount { get; set; }
}

public class ReportComparePartnerExportInfo
{
    public string Title { get; set; }
    public string PeriodCompare { get; set; }
    public string Provider { get; set; }
    public string Contract { get; set; }
    public string FullName { get; set; }
    public string PeriodPayment { get; set; }
    public List<ReportComparePartnerDto> PinCodeItems { get; set; }
    public ReportComparePartnerDto SumPinCodes { get; set; }
    public int TotalRowsPinCode { get; set; }

    public List<ReportComparePartnerDto> PinGameItems { get; set; }
    public ReportComparePartnerDto SumPinGames { get; set; }
    public int TotalRowsPinGame { get; set; }

    public List<ReportComparePartnerDto> TopupItems { get; set; }
    public ReportComparePartnerDto SumTopup { get; set; }
    public int TotalRowsTopup { get; set; }

    public List<ReportComparePartnerDto> TopupPrepaIdItems { get; set; }
    public ReportComparePartnerDto SumTopupPrepaId { get; set; }
    public int TotalRowsTopupPrepaId { get; set; }

    public List<ReportComparePartnerDto> TopupPostpaIdItems { get; set; }
    public ReportComparePartnerDto SumTopupPostpaId { get; set; }
    public int TotalRowsTopupPostpaId { get; set; }

    public List<ReportComparePartnerDto> DataItems { get; set; }
    public ReportComparePartnerDto SumData { get; set; }
    public int TotalRowsData { get; set; }

    public List<ReportComparePartnerDto> PayBillItems { get; set; }

    public ReportComparePartnerDto SumPayBill { get; set; }

    public List<ReportBalancePartnerDto> BalanceItems { get; set; }

    public int TotalRowsBalance { get; set; }

    public int TotalRowsPayBill { get; set; }

    public string TotalFeePartner { get; set; }

    public string TotalFeePartnerChu { get; set; }

    public bool IsAccountApi { get; set; }

    public bool IsAuto { get; set; }

    public static string GetIndex(int index)
    {
        var txt = "";
        switch (index)
        {
            case 1:
                txt = "I";
                break;
            case 2:
                txt = "II";
                break;
            case 3:
                txt = "III";
                break;
            case 4:
                txt = "IV";
                break;
            case 5:
                txt = "V";
                break;
            case 6:
                txt = "VI";
                break;
            case 7:
                txt = "VII";
                break;
            case 8:
                txt = "VIII";
                break;
            case 9:
                txt = "IX";
                break;
            case 10:
                txt = "X";
                break;
        }

        return txt;
    }
}