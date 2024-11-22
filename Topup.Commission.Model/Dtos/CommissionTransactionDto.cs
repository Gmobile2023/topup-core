using System;
using Topup.Shared;

namespace Topup.Commission.Model.Dtos;

public class CommissionTransactionDto
{
    public Guid Id { get; set; }
    public string ReceiverInfo { get; set; }
    public decimal CommissionAmount { get; set; }
    public CommissionTransactionStatus Status { get; set; }
    public SaleType SaleType { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public Channel Channel { get; set; }
    public string PartnerCode { get; set; }
    public string ParentCode { get; set; }
    public string TransRef { get; set; }
    public string TransCode { get; set; }
    public string ServiceCode { get; set; }
    public decimal PaymentAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public DateTime? PaymentDate { get; set; }
    public decimal ParentDiscountAmount { get; set; }
    public decimal Amount { get; set; }
    public int Quantity { get; set; }
    public string ProductCode { get; set; } //Sản phẩm
    public string CategoryCode { get; set; }
}