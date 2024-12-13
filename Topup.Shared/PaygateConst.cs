using ServiceStack.DataAnnotations;

namespace Topup.Shared;

public static class BalanceConst
{
    public const string MASTER_ACCOUNT = "MASTER";

    public const string PAYMENT_ACCOUNT = "PAYMENT";

    public const string CONTROL_ACCOUNT = "CONTROL";

    public const string CASHOUT_ACCOUNT = "CASHOUT";
    public const string COMMISSION_ACCOUNT = "COMMISSION";
}

public static class BalanceAccountTypeConst
{
    public const string SYSTEM = "SYSTEM";
    public const string CUSTOMER = "CUSTOMER";
}

public static class VendorConst
{
    public const string Viettel = "VTE";
    public const string VinaPhone = "VNA";
    public const string MobiPhone = "VMS";
    public const string VietnamMobile = "VNM";
    public const string Gmobile = "GMOBILE";
    public const string Zing = "ZING";
    public const string Gate = "GATE";
}

public static class CategoryConst
{
    public const string VTE_TOPUP = "VTE_TOPUP";
    public const string MOBILE_BILL = "MOBILE_BILL";
}

public static class TelcoCheckConst
{
    public const string Viettel = "VT";
    public const string VinaPhone = "VN";
    public const string MobiPhone = "MB";
    public const string VietnamMobile = "VM";
    public const string Gmobile = "GM";
}

public static class ProviderConst
{
    public const string VTT = "VIETTEL";
    public const string VTT_TEST = "VIETTEL_TEST";
    public const string VTT2 = "VIETTEL2";
    public const string VTT2_TEST = "VIETTEL2_TEST";
    public const string ZOTA = "ZOTA";
    public const string ZOTA_TEST = "ZOTA_TEST";
    public const string OCTA = "OCTA";
    public const string OCTA_TEST = "OCTA_TEST";
    public const string IOMEDIA = "IOMEDIA";
    public const string IOMEDIA_TEST = "IOMEDIA_TEST";
    public const string IMEDIA = "IMEDIA";
    public const string IMEDIA_TEST = "IMEDIA_TEST";
    public const string CARD = "123CARD";
    public const string NHATTRAN = "NHATTRAN";
    public const string VIMO = "VIMO";
    public const string NHATTRANSTOCK = "NHATTRANSTOCK";
    public const string APPOTA = "APPOTA";
    public const string MTC = "MTC";
    public const string FAKE = "FAKE";
    public const string CG2022 = "CG2022";
    public const string SHT = "SHT";
    public const string WPAY = "WPAY";
    public const string IRIS = "IRIS";
    //public const string IRIS_PINCODE = "IRIS_PINCODE";
    public const string PAYOO = "PAYOO";    
    public const string MOBIFONE = "MOBIFONE";
    public const string MOBIFONE_TS = "MOBIFONE-TS";
    public const string IMEDIA2 = "IMEDIA2";
    public const string PAYTECH = "PAYTECH";
    public const string ESALE = "ESALE";
    public const string HLS = "HLS";
    public const string VDS = "VDS";
    public const string PAYPOO = "PAYPOO";
    public const string VMG = "VMG";
    public const string VMG2 = "VMG2";
    public const string WHYPAY = "WHYPAY";
    public const string ADVANCE = "ADVANCE";
    public const string VTC365 = "VTC365";
    public const string GATE = "GATE";
    public const string SHOPEEPAY = "SHOPEEPAY";
    public const string VINNET = "VINNET";
    public const string FINVIET = "FINVIET";
    public const string VNPTPAY = "VNPTPAY";
}

public enum ViettelValuesConst
{
    DK5 = 5000,
    DK10 = 10000,
    DK15 = 15000,
    DK20 = 20000,
    DK30 = 30000,
    DK40 = 40000,
    DK50 = 50000,
    DK100 = 100000,
    DK200 = 200000,
    DK500 = 500000
}

public static class ServiceCodes
{
    public const string TOPUP = "TOPUP";
    public const string PIN_CODE = "PIN_CODE";
    public const string PIN_DATA = "PIN_DATA";
    public const string PAY_BILL = "PAY_BILL";
    public const string TOPUP_DATA = "TOPUP_DATA";
    public const string PIN_GAME = "PIN_GAME";
    public const string QUERY_BILL = "QUERY_BILL";
    public const string CHECK_TRANS = "CHECK_TRANS";
}

public static class StockCodeConst
{
    public static string STOCK_SALE = "STOCK_SALE";
    public static string STOCK_TEMP = "STOCK_TEMP";
    public static string STOCK_ACTIVE = "STOCK_ACTIVE";
}

public static class ResponseCode
{
    public const string Success = ResponseCodeConst.Success; //Thành công
    public const string Error = ResponseCodeConst.Error; //Lỗi
    public const string ResponseReceived = "004";
    public const string InvalidPartner = "006";
    public const string InvalidSignature = "011";
    public const string RequestAlreadyExists = "012";
    public const string CardNumberInvalid = "010";
    public const string WaitForResult = "90"; //Chưa có kết quả
    public const string CardBeginInit = "02"; //trả thẻ về kho
    public const string Undefined = "99";
    public const string TimeOut = "100";
}

public enum ServiceStatus : byte
{
    Init = 0,
    Active = 1,
    Lock = 2
}

[EnumAsInt]
public enum PartnerStatus
{
    Init = 0, //Khởi tạo
    Active = 1, //Hoạt động
    Lock = 2 //Khóa
}

[EnumAsInt]
public enum ProviderStatus
{
    Init = 0, //Khởi tạo
    Active = 1, //Hoạt động
    Lock = 2 //Khóa
}

[EnumAsInt]
public enum CategoryStatus
{
    Init = 0,
    Active = 1,
    Lock = 2
}

[EnumAsInt]
public enum ProductStatus
{
    Init = 0,
    Active = 1,
    Lock = 2
}

[EnumAsInt]
public enum ProviderType
{
    Vendor = 1,
    Game = 2
}

[EnumAsInt]
public enum GameStatus
{
    Init = 0, //Khởi tạo
    Active = 1, //Hoạt động
    Lock = 2 //Khóa
}

[EnumAsInt]
public enum SimAppType : byte
{
    Modem = 1,
    MyViettel = 2,
    ViettelPay = 3,
    ViettelPayPro = 4
}

[EnumAsInt]
public enum SimMappingType : byte
{
    Topup = 1,
    TopupTKC = 2,
    DepositSim = 3
}

[EnumAsInt]
public enum LevelDiscountStatus : byte
{
    Default = 99,
    Init = 0,
    Payment = 1,
    Cancel = 3,
    Fail = 4
}

public enum CurrencyCode : byte
{
    VND = 1,
    DEBT = 2
}

public static class TopupItemTypeConst
{
    public const string PIN_CODE = "PIN_CODE";
    public const string MAPPING = "MAPPING";
    public const string TKC = "TKC";
    public const string MVT = "MVT";
}

[EnumAsInt]
public enum TransactionType
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
    CancelPayment = 17,
    CardCharges = 18,
    AdjustmentIncrease = 19,
    AdjustmentDecrease = 20,
    ClearDebt = 21,
    SaleDeposit = 22,
    Received = 25,
    PayBatch = 26,
    SystemTransfer = 27,
    PayCommission = 28
}

public static class CardStockNotificationType
{
    public const string MinimumInventoryLimit = "MinimumInventoryLimit";
}

public enum SystemAccountType : byte
{
    Company = 0,
    System = 1,
    MasterAgent = 2, //Đại lý cấp 1
    Agent = 4 //Agent
}

public enum ReportStatus : byte
{
    Process = 0, //Đang xử lý
    Error = 3, //Lỗi
    Success = 1, //Thành công
    TimeOut = 2 //Chưa xác định
}

public static class ReportServiceCode
{
    public const string TOPUP = "TOPUP";
    public const string PIN_CODE = "PIN_CODE";
    public const string PIN_DATA = "PIN_DATA";
    public const string PIN_GAME = "PIN_GAME";
    public const string PAY_BILL = "PAY_BILL";
    public const string TOPUP_DATA = "TOPUP_DATA";
    public const string DEPOSIT = "DEPOSIT";
    public const string TRANSFER = "TRANSFER";
    public const string PAYBATCH = "PAYBATCH";
    public const string PAYCOMMISSION = "PAYCOMMISSION";
    public const string CORRECTUP = "CORRECT_UP";
    public const string CORRECTDOWN = "CORRECT_DOWN";
    public const string REFUND = "REFUND";
    public const string RECEIVEMONEY = "RECEIVE_MONEY";
}

public static class ChannelRequest
{
    public static string App = "APP";
    public static string Web = "WEB";
    public static string LandingPage = "LandingPage";
}

public enum Channel : byte
{
    WEB = 1,
    APP = 2,
    API = 3
}

public enum PayBillCustomerStatus : byte
{
    Default = 99,
    Unpaid = 0,
    Paid = 1
}

public enum AgentType : byte
{
    Agent = 1, //Đại lý
    AgentApi = 2, //Đại lý bán hàng qua api
    AgentCampany = 3, //Đại lý có hệ thống riêng
    AgentGeneral = 4, //Đại lý tổng
    SubAgent = 5, //Đại lý con
    WholesaleAgent = 6, //Đại sỉ
    Default = 99
}

public enum AccountActivityType
{
    Default = 99,
    AssignSale = 1,
    ConvertSale = 2,
    UpdateSale = 3,
    Lock = 4,
    UnLock = 5,
    ChangeUserName = 6
}

public enum SaleRequestStatus : byte
{
    Init = 0,
    Success = 1,
    Canceled = 2,
    Failed = 3,
    TimeOver = 4,
    InProcessing = 6,
    ProcessTimeout = 7,
    WaitForResult = 8,
    Paid = 9, //Đã thanh toán
    WaitForConfirm = 10, //Trạng thái gd chậm. chờ nạp bù và kết luận bằng tay
    Undefined = 99
}

public enum CommissionTransactionStatus : byte
{
    Init = 0,
    Success = 1,
    Canceled = 2,
    Failed = 3
}

public enum SaleRequestType : byte
{
    Topup = 1,
    TopupPartner = 2,
    PayBill = 3,
    PinCode = 4,
    TopupList = 5,
    PayBillList = 6
}

public enum BatchLotRequestStatus : byte
{
    Init = 0,
    Completed = 1,
    Process = 2,
    Stop = 3
}

public enum SaleType : byte
{
    Normal = 0,
    Slow = 1,
    Default = 99
}

public enum CardBatchRequestStatus : byte
{
    Init = 0,
    Success = 1,
    Failed = 3,
    InProcessing = 6,
    WaitForResult = 8
}

public static class ReportRegisterType
{
    public const string TRANSACTION_FILE = "TRANSACTION_FILE";
    public const string BATCH_FILE = "BATCH_FILE";
    public const string CARD_NXT = "CARD_NXT";
    public const string TOTAL_REVENUE = "TOTAL_REVENUE";
    public const string BALANCE_SUPPLIER = "BALANCE_SUPPLIER";
    public const string SMS_MONTH = "SMS_MONTH";
    public const string BU_DATA = "BU_DATA";
    public const string ComparePartner = "ComparePartner";
    public const string AccountSystem = "AccountSystem";
    public const string CompareNotComplete = "CompareNotComplete";
    public const string SysInfoRefund = "SysInfoRefund";
    public const string AgentBalance = "AgentBalance";
    public const string WarningBalance = "WarningBalance";
    public const string WarningCard = "WarningCard";
    public const string CHECK_REVENUE = "CHECK_REVENUE";
}

public static class ReceiverType
{
    public const string PrePaid = "PREPAID";
    public const string PostPaid = "POSTPAID";
    public const string Default = "DEFAULT";
}

public static class ReportConst
{
    public const string Batch = "Batch";
    public const string NXT = "NXT";
    public const string COMPARE = "COMPARE";
    public const string ZIP = "ZIP";
    public const string SMS = "SMS";
    public const string Revenue = "Revenue";
    public const string Provider = "Provider";
    public const string Balance = "Balance";
    public const string Auto = "Auto";
    public const string Agent = "Agent";
}

public static class AccountWith
{
    public const string StartWith = "GMB";
}