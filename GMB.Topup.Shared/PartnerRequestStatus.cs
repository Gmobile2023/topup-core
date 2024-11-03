namespace GMB.Topup.Shared;

public enum PartnerRequestStatus : byte
{
    Init = 0,
    Success = 1,
    Fail = 2,
    Timeout = 3,
    Cancel = 4,
    Processing = 6
}