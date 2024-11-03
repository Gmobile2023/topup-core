using System;
using System.Collections.Generic;
using HLS.Paygate.Shared.Dtos;

namespace HLS.Paygate.Gw.Model.Commands.TopupGw;

public interface PayBillCommand : ICommand
{
    string ServiceCode { get; }
    string CategoryCde { get; }
    string Vendor { get; }
    decimal Amount { get; }
    string ReceiverInfo { get; }
    string TransRef { get; }
    DateTime RequestDate { get; }
    string ProviderCode { get; }
    public List<ProviderConfig> ProviderCodes { get; }
    string ProductCode { get; }
    bool IsInvoice { get; }
    string PartnerCode { get; set; }
    string ReferenceCode { get; set; }
    string TransCodeProvider { get; set; }
}

public interface BillQueryCommand : ICommand
{
    string ServiceCode { get; }
    string CategoryCde { get; }
    string Vendor { get; }
    string ReceiverInfo { get; }
    string TransRef { get; }
    string ProviderCode { get; }
    bool IsInvoice { get; }
    string ProductCode { get; }
}