using System.Runtime.Serialization;

namespace HLS.Paygate.Balance.Models.Dtos;

[DataContract]
public class AccountBalanceInfo
{
    [DataMember(Order = 1)]
    public decimal AvailableBalance { get; set; }
    [DataMember(Order = 2)]
    public decimal Balance { get; set; }
    [DataMember(Order = 3)]
    public decimal BlockedMoney { get; set; }
}