using ServiceStack.DataAnnotations;

namespace HLS.Paygate.Balance.Models.Enums;

[EnumAsInt]
public enum TransactionType11
{
    Default = 0,
    Transfer = 1,
    Deposit = 2,
    Cashout = 3,
    Payment = 4,
    Revert = 5,
    MasterTopup = 6,
    MasterTopdown = 7,
    CorrectUp = 8,
    CorrectDown = 9,
    Block = 10,
    Unblock = 11,
    Topup = 12,
    Tkc = 13,
    PinCode = 14,
    CollectDiscount = 15,
    FeePriority = 16,
    CancelPayment = 17
}