using System;
using Topup.Shared;

namespace Topup.Contracts.Commands.Worker;

public interface TopupRequestCommand : ICommand
{
    public string ReceiverInfo { get; set; }
    public int Amount { get; set; }
    public string TransCode { get; set; }
    public string PartnerCode { get; set; }
    public string ParentCode { get; set; }
    public string CategoryCode { get; set; }
    public Channel Channel { get; set; }
    public string StaffAccount { get; set; }
    public string StaffUser { get; set; }
    public string ExtraInfo { get; set; }
    public string RequestIp { get; set; }
    public AgentType AgentType { get; set; }
    public SystemAccountType AccountType { get; set; }
    public string ServiceCode { get; set; }
    public string ProductCode { get; set; }
    public DateTime RequestDate { get; set; }
    public bool IsCheckReceiverType { get; set; }
    public bool IsCheckPhone { get; set; }
    public bool IsNoneDiscount{get;set;}
    public string DefaultReceiverType { get; set; }
    public bool IsCheckAllowTopupReceiverType { get; set; }
}