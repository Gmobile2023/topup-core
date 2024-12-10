namespace Topup.Shared;

public class ResponseCodeConst
{
    public const string Success = "1"; //Thành công
    public const string InvalidAuth = "401";

    public const string Error = "0"; //Lỗi
    public const string InvalidAuthen = "001";
    public const string InvalidSignature = "002";
    public const string PartnerNotFound = "003";
    public const string PartnerNotActive = "004";
    public const string ServiceNotActive = "005";
    public const string AccountNotAllowService = "006";
    public const string InvalidSecretKey = "007";

    //
    public const string ResponseCode_Success = "1";

    public const string ResponseCode_00 = "0";
    //Phần topup

    public const string ResponseCode_RequestReceived = "008"; //Đã tiếp nhận giao dịch
    public const string ResponseCode_RequestAlreadyExists = "009"; //Giao dịch đối tác đã tồn tại
    public const string ResponseCode_Cancel = "010";
    public const string ResponseCode_Failed = "011";
    public const string ResponseCode_TransactionNotFound = "012"; //Giao dịch không tồn tại
    public const string ResponseCode_TimeOut = "013";
    public const string ResponseCode_InProcessing = "014";
    public const string ResponseCode_WaitForResult = "015";
    public const string ResponseCode_Paid = "016";
    public const string ResponseCode_CardNotInventory = "017"; //KHo thẻ k đủ

    public const string
        ResponseCode_InvoiceHasBeenPaid = "018"; //Hóa đơn đã được thanh toán hoặc chưa phát sinh nợ cước

    public const string ResponseCode_PhoneLocked = "019"; //Số điện thoại đã bị khóa
    public const string ResponseCode_PhoneNotValid = "020"; //Số điện thoại k hợp lệ
    public const string ResponseCode_NotEzpay = "021"; //Chưa có TK ezpay
    public const string ResponseCode_PhoneLockTopup = "022"; //Khóa chiều nạp

    public const string
        ResponseCode_NotValidStatus = "023"; //Giao dịch không thành công. Vui lòng kiểm tra thông tin của thuê bao

    //Thêm mới
    public const string ResponseCode_ErrorProvider = "024"; //Lỗi từ phía nhà cc
    public const string ResponseCode_BillException = "025"; //K thể truy vấn thông tin hóa đơn
    public const string ResponseCode_InvalidPerpaid = "026"; //K phải thuê bao trả trước
    public const string ResponseCode_ServiceConfigNotValid = "027"; //KHông lấy được cấu hình kênh
    public const string ResponseCode_ProductNotFound = "028"; //Sản phẩm không được hỗ trợ
    public const string ResponseCode_InvalidPostpaid = "029"; //Không phải thuê bao trả sau

    // public const string ResponseCode_ProductValueNotValid = "4029"; //Mệnh giá nạp không hợp lệ

    public const string
        ResponseCode_TransactionError =
            "030"; //Mã lỗi quy định các gd đã xác định là lỗi luôn. k qua các kênh khác nữa


    //Balance
    public const string ResponseCode_Balance_Not_Enough = "031";
    public const string ResponseCode_GMB_CODE = "GMB_CODE";
}