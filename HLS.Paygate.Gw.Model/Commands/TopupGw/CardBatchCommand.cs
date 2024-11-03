using System;

namespace HLS.Paygate.Gw.Model.Commands.TopupGw;

public interface CardBatchCommand1 : ICommand
{
    string ServiceCode { get; }
    string CategoryCde { get; }
    string Vendor { get; }
    decimal Amount { get; }
    int Quantity { get; }
    string ProductCode { get; }
    string TransRef { get; }
    DateTime RequestDate { get; }
    string ProviderCode { get; }
    bool AutoImportToStock { get; }
    string PartnerCode { get; set; }
    string ReferenceCode { get; set; }
    string TransCodeProvider { get; set; }
}