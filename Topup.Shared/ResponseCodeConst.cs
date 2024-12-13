namespace Topup.Shared;

public class ResponseCodeConst
{
    public const string Success = "1"; //Thành công
    public const string InvalidAuth = "401"; //Thành công

    public const string Error = "001"; //Lỗi
    public const string InvalidAccount = "002";
    public const string InvalidSignature = "003";
    public const string PartnerNotFound = "004";
    public const string PartnerNotActive = "005";
    public const string ServiceNotActive = "006";
    public const string AccountNotAllowService = "007";
    public const string InvalidSecretKey = "008";

    //
    public const string ResponseCode_Success = "1";
    //Phần topup

    public const string ResponseCode_RequestReceived = "009"; //Đã tiếp nhận giao dịch
    public const string ResponseCode_RequestAlreadyExists = "010"; //Giao dịch đối tác đã tồn tại
    public const string ResponseCode_Cancel = "011";
    public const string ResponseCode_Failed = "012";
    public const string ResponseCode_TransactionNotFound = "013"; //Giao dịch không tồn tại
    public const string ResponseCode_TimeOut = "014";
    public const string ResponseCode_InProcessing = "015";
    public const string ResponseCode_WaitForResult = "016";
    public const string ResponseCode_Paid = "017";
    public const string ResponseCode_CardNotInventory = "018"; //KHo thẻ k đủ

    public const string
        ResponseCode_InvoiceHasBeenPaid = "019"; //Hóa đơn đã được thanh toán hoặc chưa phát sinh nợ cước

    public const string ResponseCode_PhoneLocked = "020"; //Số điện thoại đã bị khóa
    public const string ResponseCode_PhoneNotValid = "021"; //Số điện thoại k hợp lệ
    public const string ResponseCode_NotEzpay = "022"; //Chưa có TK ezpay
    public const string ResponseCode_PhoneLockTopup = "023"; //Khóa chiều nạp

    public const string
        ResponseCode_NotValidStatus = "024"; //Giao dịch không thành công. Vui lòng kiểm tra thông tin của thuê bao

    //Thêm mới
    public const string ResponseCode_ErrorProvider = "025"; //Lỗi từ phía nhà cc
    public const string ResponseCode_BillException = "026"; //K thể truy vấn thông tin hóa đơn
    public const string ResponseCode_InvalidPerpaid = "027"; //K phải thuê bao trả trước
    public const string ResponseCode_ServiceConfigNotValid = "028"; //KHông lấy được cấu hình kênh
    public const string ResponseCode_ProductNotFound = "029"; //Sản phẩm không được hỗ trợ
    public const string ResponseCode_InvalidPostpaid = "030"; //Không phải thuê bao trả sau

    // public const string ResponseCode_ProductValueNotValid = "4029"; //Mệnh giá nạp không hợp lệ

    public const string
        ResponseCode_TransactionError =
            "031"; //Mã lỗi quy định các gd đã xác định là lỗi luôn. k qua các kênh khác nữa


    //Balance
    public const string ResponseCode_Balance_Not_Enough = "032";
    public const string ResponseCode_GMB_CODE = "GMB_CODE";
}