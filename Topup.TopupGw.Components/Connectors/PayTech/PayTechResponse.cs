using System.Collections.Generic;

namespace Topup.TopupGw.Components.Connectors.PayTech;

public class PayTechResponse
{
    public string code { get; set; }
    public string message { get; set; }
    public string reason { get; set; }
    public BalanceInfo balance { get; set; }
    public OrderInfo order { get; set; }
    public List<TransactionInfo> transactions { get; set; }

    public List<bills> bills { get; set; }

    public List<costs> costs { get; set; }
}
public class BalanceInfo
{
    public decimal balance { get; set; }

    public decimal debit { get; set; }
}

public class bills
{
    public string target { get; set; }


    public decimal amount { get; set; }

    public string customerName { get; set; }

    public string customerAddress { get; set; }

    public string paymentCycle { get; set; }

    public decimal queryAmount { get; set; }

    public decimal collectionFee { get; set; }
}

public class costs
{
    public string code { get; set; }
    public string type { get; set; }
}
public class OrderInfo
{
    public string orderId { get; set; }

    public string refer { get; set; }

    public decimal totalCount { get; set; }

    public decimal queryCount { get; set; }

    public int remainCount { get; set; }

    public int autoCharge { get; set; }

    public decimal totalAmount { get; set; }

    public decimal queryAmount { get; set; }

    public decimal paymentAmount { get; set; }

    public decimal paymentFee { get; set; }

    public decimal successAmount { get; set; }

    public decimal reverseAmount { get; set; }

    public decimal discountAmount { get; set; }

    public decimal commissionAmount { get; set; }

    public string message { get; set; }

    public string status { get; set; }

    public string reason { get; set; }

    public int priority { get; set; }
}
public class TransactionInfo
{
    public string transactionId { get; set; }

    public string orderId { get; set; }

    public string refer { get; set; }

    public string issuerId { get; set; }

    public string type { get; set; }
    public string target { get; set; }

    public decimal amount { get; set; }

    public int quantity { get; set; }

    public string productId { get; set; }

    public string productName { get; set; }

    public string status { get; set; }

    public string message { get; set; }

    public string reason { get; set; }

}
