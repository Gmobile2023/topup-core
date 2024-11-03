

using GMB.Topup.TopupGw.Contacts.Dtos;

namespace GMB.Topup.TopupGw.Components.Connectors.PayTech;

public class DataInput
{
    public string Function { get; set; }
    /// <summary>
    /// Quy định loại dịch vụ
    /// </summary>
    public string ServiceCode { get; set; }

    /// <summary>
    /// 1:Thanh toán ngay
    /// 0:Truy vấn
    /// </summary>
    public int Status { get; set; }

    public string TransCode { get; set; }

    public string TransCodeConfirm { get; set; }

    public TopupRequestLogDto Topup { get; set; }

    public PayBillRequestLogDto PayBill { get; set; }

    public CardRequestLogDto Card { get; set; }

}
public class DataRequest
{
    public string Function { get; set; }

    public HeaderRequest HeaderDto { get; set; }

    public OrderRequest OrderDto { get; set; }

    public ConfirmRequest ConfirmDto { get; set; }

    public QueryRequest QueryTransDto { get; set; }

    public DownloadRequest DownloadDto { get; set; }
}

public class OrderRequest
{
    public string function { get; set; }
    public string orderId { get; set; }
    public string refer { get; set; }
    public string fullname { get; set; }
    public string email { get; set; }
    public string phoneNumber { get; set; }
    //public string promoCode { get; set; }
    //public string methodId { get; set; }
    //public string paymentOption { get; set; }
    //public string cardToken { get; set; }        


    //public string callbackUrl { get; set; }
    public decimal autoCharge { get; set; }
    public string transactionId { get; set; }

    public string type { get; set; }

    public string issuerId { get; set; }

    public string target { get; set; }

    public decimal amount { get; set; }

    public int quantity { get; set; }

    //public string areaId { get; set; }

    public string productId { get; set; }

    public string productName { get; set; }

    public string autoRetry { get; set; }

    //public string postBack { get; set; }
}

public class MemberInfo
{
    public string ProductId { get; set; }
    public string ProductName { get; set; }
    public string Service { get; set; }
    public string TelCo { get; set; }
    public int AutoCharge { get; set; }
    public string FullName { get; set; }
    public string Email { get; set; }
    public string PhoneNumber { get; set; }
}

public class ConfirmRequest
{
    public string function { get; set; }
    public string orderId { get; set; }
    public string status { get; set; }
    public string reason { get; set; }
    public string bills { get; set; }
    public string packs { get; set; }
}

public class HeaderRequest
{
    public string partnerId { get; set; }

    public string accountId { get; set; }

    public string requestId { get; set; }

    public string signature { get; set; }

    public string requestTime { get; set; }
}

public class DownloadRequest
{
    public string function { get; set; }
    public string orderId { get; set; }
    public string transactionId { get; set; }
}

public class QueryRequest
{
    public string function { get; set; }

    public string orderId { get; set; }

    public string transactionId { get; set; }
}

public class Function_PayTech
{
    public const string Balance = "000101";
    public const string Order = "000001";
    public const string Confirm = "000010";
    public const string Query = "000011";
    public const string Download = "000100";
}
