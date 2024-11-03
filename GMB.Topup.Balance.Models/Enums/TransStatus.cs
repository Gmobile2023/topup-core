using ServiceStack.DataAnnotations;

namespace GMB.Topup.Balance.Models.Enums;

[EnumAsInt]
public enum TransStatus
{
    Init = 0,
    Done = 1,
    Cancel = 2,
    Error = 3,
    Reverted = 4,
    PartialRevert = 5,
    CorrectUp = 6,
    CorrectDown = 7
}