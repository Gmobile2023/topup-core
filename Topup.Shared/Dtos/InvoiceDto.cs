using System;

namespace Topup.Shared.Dtos;

public class InvoiceDto
{
    public DateTime CreatedTime { get; set; }
    public string Email { get; set; }

    public string FullName { get; set; }

    //Max KH
    public string CustomerReference { get; set; }

    public string Address { get; set; }

    //Kỳ thanh toán
    public string Period { get; set; }

    public string PhoneNumber { get; set; }
    public string ProductCode { get; set; }
    public string ProductName { get; set; }

    //Mã giao dịch (TransCode core)
    public string TransCode { get; set; }

    //Mã giao dịch đối tác (Mã trên web)
    public string TransRef { get; set; }
    public string Description { get; set; }
    public string ExtraInfo { get; set; }
}