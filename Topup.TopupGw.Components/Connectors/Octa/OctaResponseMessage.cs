using System;
using System.Runtime.Serialization;

namespace Topup.TopupGw.Components.Connectors.Octa;

public class OctaResponseMessage<T>
{
    [DataMember(Name = "Response")] public OctaResponse<T> Response { get; set; }

    [DataMember(Name = "Sign")] public string Sign { get; set; }
}

public class OctaTransactionInfo
{
    [DataMember(Name = "Note")] public string Note { get; set; }

    [DataMember(Name = "TppType")] public int TppType { get; set; }
}
public class OctaCheckTransResponseData
{
    [DataMember(Name = "Accepted")] public decimal Accepted { get; set; }

    [DataMember(Name = "AccountID")] public int AccountID { get; set; }

    [DataMember(Name = "AgentID")] public int AgentID { get; set; }

    [DataMember(Name = "Amount")] public decimal Amount { get; set; }

    [DataMember(Name = "Code")] public int Code { get; set; }

    [DataMember(Name = "Info")] public string Info { get; set; }

    [DataMember(Name = "Proccessed")] public decimal Proccessed { get; set; }

    [DataMember(Name = "ReceiptNumber")] public string ReceiptNumber { get; set; }

    [DataMember(Name = "RecivedDate")] public DateTime RecivedDate { get; set; }

    [DataMember(Name = "RequestDate")] public DateTime RequestDate { get; set; }

    [DataMember(Name = "ServiceID")] public int ServiceID { get; set; }

    [DataMember(Name = "Status")] public int? Status { get; set; }

    [DataMember(Name = "TransactionID")] public int TransactionID { get; set; }

    [DataMember(Name = "TransactionType")] public object TransactionType { get; set; }

    [DataMember(Name = "RecivedAccount")] public string RecivedAccount { get; set; }
}

public class OctaTransactionResponseData
{
    [DataMember(Name = "Transaction")] public OctaCheckTransResponseData Transaction { get; set; }

    [DataMember(Name = "Address")] public string Address { get; set; }

    [DataMember(Name = "AgentID")] public int AgentID { get; set; }

    [DataMember(Name = "AgentName")] public string AgentName { get; set; }

    [DataMember(Name = "Balance")] public decimal Balance { get; set; }

    [DataMember(Name = "Email")] public string Email { get; set; }

    [DataMember(Name = "PhoneNumber")] public string PhoneNumber { get; set; }
}

public class OctaResponse<T>
{
    [DataMember(Name = "Code")] public int Code { get; set; }

    [DataMember(Name = "Message")] public string Message { get; set; }

    [DataMember(Name = "Data")] public T Data { get; set; }
}

public class OctaCardResponse
{
    public string Provider { get; set; }
    public string ExpriedDate { get; set; }
    public string Code { get; set; }
    public string Serial { get; set; }
    public decimal Price { get; set; }
    public int ServiceID { get; set; }
}