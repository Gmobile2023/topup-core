using System;
using HLS.Paygate.Shared;
using ServiceStack;

namespace HLS.Paygate.Common.Model.Dtos.RequestDto;

public class AuditLogRequest
{
}

[Route("/api/v1/common/auditlog/account-activities/add", "POST")]
public class AccountActivityHistoryRequest
{
    public string AccountCode { get; set; }
    public string FullName { get; set; }
    public int AccountType { get; set; }
    public int AgentType { get; set; }
    public string PhoneNumber { get; set; }
    public string UserName { get; set; }
    public string Note { get; set; }
    public string Payload { get; set; }
    public string SrcValue { get; set; }
    public string DesValue { get; set; }

    public string Attachment { get; set; }
    public AccountActivityType AccountActivityType { get; set; }
}

[Route("/api/v1/common/auditlog/account-activities", "GET")]
public class GetAccountActivityHistoryRequest : PaggingBase, IReturn<MessagePagedResponseBase>
{
    public string AccountCode { get; set; }
    public int AccountType { get; set; }
    public int AgentType { get; set; }
    public string PhoneNumber { get; set; }
    public string UserName { get; set; }
    public string Note { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public AccountActivityType AccountActivityType { get; set; }
}