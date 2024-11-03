using System;
using System.Collections.Generic;
using HLS.Paygate.Shared.Dtos;

namespace HLS.Paygate.Gw.Model.Commands.TopupGw;

public interface TopupCommand : ICommand
{
    string ServiceCode { get; set; }
    string CategoryCode { get; set; }
    string Vendor { get; set; }
    decimal Amount { get; set; }
    string ReceiverInfo { get; set; }
    string TransRef { get; set; }
    string ProviderCode { get; set; }
    List<ProviderConfig> ProviderCodes { get; set; }
    DateTime RequestDate { get; set; }
    string ProductCode { get; set; }
    string PartnerCode { get; set; }
    string ReferenceCode { get; set; }
    string TransCodeProvider { get; set; }
}