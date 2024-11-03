namespace HLS.Paygate.Shared;

public class ResponseCodeConst
{
    public const string Success = "01"; //Thành công

    public const string Error = "00"; //Lỗi
    public const string InvalidAuthen = "401";
    public const string InvalidSignature = "2001";
    public const string PartnerNotFound = "2002";
    public const string PartnerNotActive = "2003";
    public const string ServiceNotActive = "2004";
    public const string AccountNotAllowService = "2005";
    public const string InvalidSecretKey = "2006";

    //
    public const string ResponseCode_Success = "01";

    public const string ResponseCode_00 = "00";
    //Phần topup

    public const string ResponseCode_RequestReceived = "4000"; //Đã tiếp nhận giao dịch
    public const string ResponseCode_RequestAlreadyExists = "4001"; //Giao dịch đối tác đã tồn tại
    public const string ResponseCode_Cancel = "4002";
    public const string ResponseCode_Failed = "4003";
    public const string ResponseCode_TransactionNotFound = "4004"; //Giao dịch không tồn tại
    public const string ResponseCode_TimeOut = "4005";
    public const string ResponseCode_InProcessing = "4006";
    public const string ResponseCode_WaitForResult = "4007";
    public const string ResponseCode_Paid = "4008";
    public const string ResponseCode_CardNotInventory = "4009"; //KHo thẻ k đủ

    public const string
        ResponseCode_InvoiceHasBeenPaid = "4010"; //Hóa đơn đã được thanh toán hoặc chưa phát sinh nợ cước

    public const string ResponseCode_PhoneLocked = "4011"; //Số điện thoại đã bị khóa
    public const string ResponseCode_PhoneNotValid = "4012"; //Số điện thoại k hợp lệ
    public const string ResponseCode_NotEzpay = "4013"; //Chưa có TK ezpay
    public const string ResponseCode_PhoneLockTopup = "4014"; //Khóa chiều nạp
    public const string ResponseCode_NotValidStatus = "4044"; //Giao dịch không thành công. Vui lòng kiểm tra thông tin của thuê bao

    //Thêm mới
    public const string ResponseCode_ErrorProvider = "4023"; //Lỗi từ phía nhà cc
    public const string ResponseCode_BillException = "4024"; //K thể truy vấn thông tin hóa đơn
    public const string ResponseCode_InvalidPerpaid = "4025"; //K phải thuê bao trả trước
    public const string ResponseCode_ServiceConfigNotValid = "4026"; //KHông lấy được cấu hình kênh
    public const string ResponseCode_ProductNotFound = "4027"; //Sản phẩm không được hỗ trợ
    public const string ResponseCode_InvalidPostpaid = "4028"; //Không phải thuê bao trả sau

   // public const string ResponseCode_ProductValueNotValid = "4029"; //Mệnh giá nạp không hợp lệ

    public const string
        ResponseCode_TransactionError =
            "4028"; //Mã lỗi quy định các gd đã xác định là lỗi luôn. k qua các kênh khác nữa


    //Balance
    public const string ResponseCode_Balance_Not_Enough = "6001";
    public const string ResponseCode_NT_CODE = "NTCODE";
}