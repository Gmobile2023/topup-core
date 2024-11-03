using System;
using GMB.Topup.Shared;

namespace GMB.Topup.Report.Model.Dtos;

public class ReportTransactionDetailDto
{
    public Guid Id { get; set; }
    public double Amount { get; set; }
    public double Decrement { get; set; }
    public double Increment { get; set; }
    public double BalanceBefore { get; set; }
    public double BalanceAfter { get; set; }
    public string CurrencyCode { get; set; }
    public string SrcAccountCode { get; set; }
    public string DesAccountCode { get; set; }
    public double SrcAccountBalanceAfterTrans { get; set; }
    public double DesAccountBalanceAfterTrans { get; set; }
    public double SrcAccountBalanceBeforeTrans { get; set; }
    public double DesAccountBalanceBeforeTrans { get; set; }
    public string TransRef { get; set; }
    public string TransCode { get; set; }
    public byte Status { get; set; }
    public string ModifiedBy { get; set; }
    public string CreatedBy { get; set; }
    public string RevertTransCode { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public DateTime CreatedDate { get; set; }
    public string Description { get; set; }
    public string TransNote { get; set; }
    public string TransactionType { get; set; }
    public byte TransType { get; set; }

    public string ServiceCode { get; set; }
    public string MobileNumber { get; set; }
    public int Timeout { get; set; }
    public SaleRequestStatus SaleRequestStatus { get; set; }
    public SaleRequestType SaleRequestType { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime EndProcessTime { get; set; }
    public string PartnerCode { get; set; }
    public string Vendor { get; set; }
    public string Provider { get; set; }
    public string ShortCode { get; set; }
    public string TopupCommand { get; set; }
    public string PaymentTransCode { get; set; }
    public decimal ProcessedAmount { get; set; }
    public decimal PaymentAmount { get; set; }
    public decimal DiscountRate { get; set; } //Phần trăm chiết khấu thực của TK nhận dc
    public string ProductCode { get; set; } //Sản phẩm
    public string CategoryCode { get; set; }
    public decimal Quantity { get; set; }
    public string Email { get; set; }
    public decimal PriorityDiscountRate { get; set; } //Phần trăm chiết khấu nhập vào để đua giá
    public int Multiples { get; set; } //Bội số. Cái này hiện chưa dùng
    public decimal PriorityFee { get; set; } //Phí ưu tiên

    public string PasswordApp { get; set; }
    public decimal RevertAmount { get; set; }
    public string BatchTransCode { get; set; }
    public string WorkerApp { get; set; }
}