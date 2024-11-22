namespace Topup.TopupGw.Contacts.Enums;

public enum TransRequestStatus : byte
{
    Init = 0,
    Success = 1,
    Fail = 3,
    Timeout = 4,
    Cancel = 2
}