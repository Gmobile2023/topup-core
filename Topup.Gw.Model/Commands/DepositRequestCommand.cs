using System;

namespace Topup.Gw.Model.Commands;

public interface SimDepositRequestCommands
{
    Guid Id { get; set; }
    string SimNumber { get; set; }
    string Description { get; set; }
    decimal Amount { get; set; }
    string Vendor { get; set; }
    string ProviderCode { get; set; }
    string TransRef { get; set; }
    string TransCode { get; set; }
    string Serial { get; set; }
    string CardCode { get; set; }
}