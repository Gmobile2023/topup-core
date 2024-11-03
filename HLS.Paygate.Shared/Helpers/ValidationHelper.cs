using System.Text.RegularExpressions;

namespace HLS.Paygate.Shared.Helpers;

public static class ValidationHelper
{
    private const string EmailRegex = @"^\w+([-+.']\w+)*@\w+([-.]\w+)*\.\w+([-.]\w+)*$";
    private const string PhoneNumberRegex = "^0[0-9]{9}$"; //"^((09|08|03|07|05)+([0-9]{8}))$";
    private const string PhoneNumberRegex01 = "^0[0-9]{10}$";
    private const string PhoneNumberAll = @"\b\d{10,11}\b";

    private const string VietNameseNameRegex =
        "^[a-zA-ZaàáảãạâầấẩẫậăằắẳẵặoòóỏõọơờớởỡợôồốổỗộuùúủũụưừứửữựiìíỉịyỳýỷỹỵeèéẻẽẹêềếểễệAÀÁẢÃẠÂẦẤẨẪẬĂẰẮẲẴẶOÒÓỎÕỌƠỜỚỞỠỢÔỒỐỔỖỘUÙÚỦŨỤƯỪỨỬỮỰIÌÍỈỊYỲÝỶỸỴEÈÉẺẼẸÊỀẾỂỄỆđĐ ]+$";

    private const string UserNameRegex = "^(?=.{3,32}$)(?![_.])(?!.*[_.]{2})[a-zA-Z0-9._]+(?<![_.])$";
    private const string UrlRegex = @"(https?:\/\/)?([\da-z\.-]+)\.([a-z]{2,6})([\/\w\.-]*)*\/?";
    private const string AccountCodeRegex = @"^[a-zA-Z0-9_.-]*$";

    public static bool IsEmail(string value)
    {
        if (string.IsNullOrEmpty(value)) return false;

        var regex = new Regex(EmailRegex);
        return regex.IsMatch(value);
    }

    public static bool IsPhone(string value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        //var regex = new Regex(value.StartsWith("01699") ? PhoneNumberRegex01 : PhoneNumberRegex);
        var regex = new Regex(PhoneNumberAll);
        return regex.IsMatch(value);
    }

    public static bool IsAccountCode(string value)
    {
        if (string.IsNullOrEmpty(value)) return false;

        var regex = new Regex(AccountCodeRegex);
        return regex.IsMatch(value);
    }

    public static string GetOriginAccountCode(this string accountCode)
    {
        return accountCode.Split('*')[0];
    }

    public static bool IsUserName(string value)
    {
        if (string.IsNullOrEmpty(value)) return false;

        var regex = new Regex(UserNameRegex);
        return regex.IsMatch(value);
    }

    public static bool IsUrl(string value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        var regex = new Regex(UrlRegex);
        return regex.IsMatch(value);
    }
}