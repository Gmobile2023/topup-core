using System.Collections.Generic;
using System.Runtime.Serialization;

namespace HLS.Paygate.Shared.Dtos;

public class InvoiceResponseDto
{
    public string InvoiceId { get; set; }


    public string ServiceId { get; set; }


    public string InvoiceReference { get; set; }


    public string CustomerReference { get; set; }


    public string Amount { get; set; }


    public string CashBackAmount { get; set; }


    public string Currency { get; set; }


    public string Info { get; set; }


    public string CreationDate { get; set; }


    public string DueDate { get; set; }


    public object PayDate { get; set; }


    public object ExpirationDate { get; set; }


    public int Status { get; set; }


    public string IsPartialPaymentAllowed { get; set; }


    public List<InvoiceAttributeDto> InvoiceAttributes { get; set; }


    public bool SetCustomerReference { get; set; }


    public bool SetCreationDate { get; set; }


    public bool SetInvoiceReference { get; set; }


    public bool SetExpirationDate { get; set; }


    public bool SetInvoiceAttributes { get; set; }


    public bool SetPayDate { get; set; }


    public bool SetCurrency { get; set; }


    public bool SetStatus { get; set; }


    public bool SetDueDate { get; set; }


    public bool SetAmount { get; set; }
}

public class InvoiceAttributeDto
{
    public string InvoiceId { get; set; }


    public string InvoiceAttributeTypeId { get; set; }


    public string Value { get; set; }


    public object Created { get; set; }
}

[DataContract]
public class InvoiceResultDto
{
    [DataMember(Order = 1)] public decimal Amount { get; set; }
    [DataMember(Order = 2)] public string CustomerReference { get; set; }
    [DataMember(Order = 3)] public string CustomerName { get; set; }
    [DataMember(Order = 4)] public string Address { get; set; }
    [DataMember(Order = 5)] public string Period { get; set; }
    [DataMember(Order = 6)] public string BillType { get; set; }
    [DataMember(Order = 7)] public string BillId { get; set; }
    [DataMember(Order = 8)] public List<PeriodDto> PeriodDetails { get; set; }
}

[DataContract]
public class PeriodDto
{
    [DataMember(Order = 1)] public string Period { get; set; }
    [DataMember(Order = 2)] public decimal Amount { get; set; }
    [DataMember(Order = 3)] public string BillNumber { get; set; }
    [DataMember(Order = 4)] public string BillType { get; set; }
}