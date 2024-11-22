using Topup.Shared.Dtos;

namespace Topup.TopupGw.Components.Connectors.ZoTa;

public class ZoTaResponse
{
    public Status Status { get; set; }
    public string ConversationId { get; set; }
    public decimal Balance { get; set; }
    public string TxnId { get; set; }
    public string RefNumber { get; set; }
    public string EncryptedCards { get; set; }
    public InvoiceResponseDto Invoice { get; set; }
    public Transaction Transaction { get; set; }
}

public class Status
{
    public string Value { get; set; }
    public int Code { get; set; }
}

public class Transaction
{
    public string Id { get; set; }


    public object TraceNo { get; set; }


    public object RefTxnId { get; set; }


    public string ServiceType { get; set; }


    public string ServiceId { get; set; }


    public string ServiceName { get; set; }


    public string ServiceShortName { get; set; }


    public string TerminalId { get; set; }


    public string SourceOfFundId { get; set; }


    public string OrderChannel { get; set; }


    public string OrderId { get; set; }


    public string OrderInfo { get; set; }


    public string Cif { get; set; }


    public string Username { get; set; }


    public object PhoneNumber { get; set; }


    public string PayerId { get; set; }


    public string PayerUsername { get; set; }


    public string PayerFullname { get; set; }


    public string PayerMsisdn { get; set; }


    public object PayeeId { get; set; }


    public object PayeeUsername { get; set; }


    public object PayeeFullname { get; set; }


    public object PayeeMsisdn { get; set; }


    public string IdOwner { get; set; }


    public string IdOwnerCustomerType { get; set; }


    public string Amount { get; set; }


    public string Currency { get; set; }


    public int Fee { get; set; }


    public int Commision { get; set; }


    public int Discount { get; set; }


    public int Cashback { get; set; }


    public object CapitalValue { get; set; }


    public string RealAmount { get; set; }


    public string PreBalance { get; set; }


    public string PostBalance { get; set; }


    public object Test { get; set; }


    public object Locked { get; set; }


    public object AutoCapture { get; set; }


    public object Expiration { get; set; }


    public object Text { get; set; }


    public object ProviderCode { get; set; }


    public int ErrorCode { get; set; }


    public string ErrorMessage { get; set; }


    public string TransactionStatus { get; set; }


    public string CreationDate { get; set; }


    public object Serials { get; set; }
}

public class Card
{
    public string Serial { get; set; }

    public string Pin { get; set; }

    public string Price { get; set; }

    public string CardType { get; set; }

    public string ExpiredDate { get; set; }
}