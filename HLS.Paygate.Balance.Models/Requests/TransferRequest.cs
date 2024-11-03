using System.Collections.Generic;
using System.Runtime.Serialization;
using HLS.Paygate.Balance.Models.Enums;
using HLS.Paygate.Shared;
using ServiceStack;
using ServiceStack.DataAnnotations;

namespace HLS.Paygate.Balance.Models.Requests;

[DataContract]
[Route("/api/v1/balance/deposit", "POST")]
[Route("/api/v1/balance/deposit/{AccountCode}/{CurrencyCode}/{Amount}/{TransRef}", "POST")]
[Route("/api/v1/balance/deposit/{AccountCode}/{CurrencyCode}/{Amount}/{TransRef}/{Description}", "POST")]
public class DepositRequest : IPost, IReturn<object>
{
    [DataMember(Order = 1)] public string AccountCode { get; set; }
    [DataMember(Order = 2)] public string CurrencyCode { get; set; }
    [DataMember(Order = 3)] public decimal Amount { get; set; }
    [DataMember(Order = 4)] public string TransRef { get; set; }
    [DataMember(Order = 5)] public string Description { get; set; }
    [DataMember(Order = 6)] public string TransNote { get; set; }
    [DataMember(Order = 7)] public string ExtraInfo { get; set; }
}


[DataContract]
[Route("/api/v1/balance/cashout", "POST")]
[Route("/api/v1/balance/cashout/{AccountCode}/{CurrencyCode}/{Amount}/{TransRef}", "POST")]
[Route("/api/v1/balance/cashout/{AccountCode}/{CurrencyCode}/{Amount}/{TransRef}/{Description}", "POST")]
public class CashOutRequest : IPost, IReturn<object>
{
    [DataMember(Order = 1)] public string AccountCode { get; set; }
    [DataMember(Order = 2)] public string CurrencyCode { get; set; }
    [DataMember(Order = 3)] public decimal Amount { get; set; }
    [DataMember(Order = 4)] public string TransRef { get; set; }
    [DataMember(Order = 5)] public string Description { get; set; }
    [DataMember(Order = 6)] public string TransNote { get; set; }
}


[DataContract]
[Route("/api/v1/balance/payment", "POST")]
[Route("/api/v1/balance/payment/{AccountCode}/{CurrencyCode}/{PaymentAmount}", "POST")]
[Route("/api/v1/balance/payment/{AccountCode}/{CurrencyCode}/{PaymentAmount}/{Description}", "POST")]
public class PaymentRequest : IPost, IReturn<object>
{
    [DataMember(Order = 1)][Required] public string AccountCode { get; set; }
    [DataMember(Order = 2)] public decimal PaymentAmount { get; set; }
    [DataMember(Order = 3)] public string CurrencyCode { get; set; }
    [DataMember(Order = 4)] public string TransRef { get; set; }
    [DataMember(Order = 5)] public string Description { get; set; }
    [DataMember(Order = 6)] public string MerchantCode { get; set; }
    [DataMember(Order = 7)] public string TransNote { get; set; }
}

[DataContract]
[Route("/api/v1/balance/revert", "POST")]
[Route("/api/v1/balance/revert/{TransactionCode}/{TransRef}", "POST")]
[Route("/api/v1/balance/revert/{TransactionCode}/{Reason}/{TransRef}", "POST")]
public class RevertRequest : IPost, IReturn<object>
{
    [DataMember(Order = 1)] public string TransactionCode { get; set; }
    [DataMember(Order = 2)] public string Reason { get; set; }
    [DataMember(Order = 3)] public decimal RevertAmount { get; set; }
    [DataMember(Order = 4)] public string TransRef { get; set; }
    [DataMember(Order = 5)] public string TransNote { get; set; }
}

[DataContract]
[Route("/api/v1/balance/priorityPayment", "POST")]
public class PriorityPaymentRequest : IPost, IReturn<object>
{
    [DataMember(Order = 1)] public string TransactionCode { get; set; }
    [DataMember(Order = 2)] public string TransRef { get; set; }
    [DataMember(Order = 3)] public decimal Amount { get; set; }
    [DataMember(Order = 4)] public string TransNote { get; set; }
    [DataMember(Order = 5)] public string Description { get; set; }
    [DataMember(Order = 6)]  public string AccountCode { get; set; }
    [DataMember(Order = 7)] public string CurrencyCode { get; set; }
}

[DataContract]
[Route("/api/v1/balance/correct", "POST")]
[Route("/api/v1/balance/correct/{TransactionCode}/{Amount}/{TransRef}", "POST")]
[Route("/api/v1/balance/correct/{TransactionCode}/{Amount}/{Reason}/{TransRef}", "POST")]
public class CorrectRequest : IPost, IReturn<object>
{
    [DataMember(Order = 1)] public string TransactionCode { get; set; }
    [DataMember(Order = 2)] public string Reason { get; set; }
    [DataMember(Order = 3)] public decimal Amount { get; set; }
    [DataMember(Order = 4)] public string TransRef { get; set; }
    [DataMember(Order = 5)] public string TransNote { get; set; }
    [DataMember(Order = 6)] public string AccountCode { get; set; }
}

[DataContract]
[Route("/api/v1/balance/collectDiscount", "POST")]
public class CollectDiscountRequest : IPost, IReturn<object>
{
    [DataMember(Order = 1)] public string Reason { get; set; }
    [DataMember(Order = 2)] public decimal Amount { get; set; }
    [DataMember(Order = 3)] public string TransRef { get; set; }
    [DataMember(Order = 4)] public string TransNote { get; set; }
    [DataMember(Order = 5)] public string AccountCode { get; set; }
}

[DataContract]
[Route("/api/v1/balance/transfer", "POST")]
[Route("/api/v1/balance/transfer/{SrcAccount}/{DesAccount}/{CurrencyCode}/{Amount}", "POST")]
[Route("/api/v1/balance/transfer/{SrcAccount}/{DesAccount}/{CurrencyCode}/{Amount}/{Description}", "POST")]
public class TransferRequest : IPost, IReturn<object>
{
    [DataMember(Order = 1)] public decimal Amount { get; set; }
    [DataMember(Order = 2)] public string SrcAccount { get; set; }
    [DataMember(Order = 3)] public string CurrencyCode { get; set; }
    [DataMember(Order = 4)] public string DesAccount { get; set; }
    [DataMember(Order = 5)] public string TransRef { get; set; }
    [DataMember(Order = 6)] public string Description { get; set; }
    [DataMember(Order = 7)] public string TransNote { get; set; }
}

[DataContract]
[Route("/api/v1/balance/transfermoney", "POST")]
public class TransferMoneyRequest : IPost, IReturn<object>
{
    [DataMember(Order = 1)] public decimal Amount { get; set; }
    [DataMember(Order = 2)] public string Account { get; set; }
    [DataMember(Order = 3)] public string SrcAccount { get; set; }
    [DataMember(Order = 4)] public string TransRef { get; set; }
    [DataMember(Order = 5)] public string Description { get; set; }
    [DataMember(Order = 6)] public string TransNote { get; set; }
}

[DataContract]
[Route("/api/v1/balance/mastertopup", "POST")]
[Route("/api/v1/balance/mastertopup/{CurrencyCode}/{Amount}/{TransRef}/{BankCode}", "POST")]
public class MasterTopupRequest : IPost, IReturn<NewMessageReponseBase<BalanceResponse>>
{
    [DataMember(Order = 1)] public string CurrencyCode { get; set; }
    [DataMember(Order = 2)] public decimal Amount { get; set; }
    [DataMember(Order = 3)] public string TransRef { get; set; }
    [DataMember(Order = 4)] public string BankCode { get; set; }
    [DataMember(Order = 5)] public string TransNote { get; set; }
}

[DataContract]
[Route("/api/v1/balance/mastertopdown", "POST")]
[Route("/api/v1/balance/mastertopdown/{CurrencyCode}/{Amount}/{TransRef}/{BankCode}", "POST")]
public class MasterTopdownRequest : IPost, IReturn<object>
{
    [DataMember(Order = 1)] public string CurrencyCode { get; set; }
    [DataMember(Order = 2)] public decimal Amount { get; set; }
    [DataMember(Order = 3)] public string TransRef { get; set; }
    [DataMember(Order = 4)] public string BankCode { get; set; }
    [DataMember(Order = 5)] public string TransNote { get; set; }
}

[DataContract]
[Route("/api/v1/balance/charging", "POST")]
public class ChargingRequest : IPost, IReturn<object>
{
    [DataMember(Order = 1)] public string TransactionCode { get; set; }
    [DataMember(Order = 2)] public decimal Amount { get; set; }
    [DataMember(Order = 3)] public string TransRef { get; set; }
    [DataMember(Order = 4)] public string TransNote { get; set; }
    [DataMember(Order = 5)] public string AccountCode { get; set; }
    [DataMember(Order = 6)] public string CurrencyCode { get; set; }
}

[DataContract]
[Route("/api/v1/balance/adjustment", "POST")]
public class AdjustmentRequest : IPost, IReturn<object>
{
    [DataMember(Order = 1)] public string AccountCode { get; set; }
    [DataMember(Order = 2)] public string CurrencyCode { get; set; }
    [DataMember(Order = 3)] public decimal Amount { get; set; }
    [DataMember(Order = 4)] public string TransRef { get; set; }
    [DataMember(Order = 5)] public string TransNote { get; set; }
    [DataMember(Order = 6)] public AdjustmentType AdjustmentType { get; set; }
}

[DataContract]
[Route("/api/v1/balance/clear-debt", "POST")]
public class ClearDebtRequest : IPost, IReturn<object>
{
    [DataMember(Order = 1)] public string AccountCode { get; set; }
    [DataMember(Order = 2)] public decimal Amount { get; set; }
    [DataMember(Order = 3)] public string TransRef { get; set; }
    [DataMember(Order = 4)] public string TransNote { get; set; }
}

[DataContract]
[Route("/api/v1/balance/sale-deposit", "POST")]
public class SaleDepositRequest : IPost, IReturn<object>
{
    [DataMember(Order = 1)][Required] public string AccountCode { get; set; }
    [DataMember(Order = 2)][Required] public string SaleCode { get; set; }   
    [DataMember(Order = 3)][Required] public decimal Amount { get; set; }
    [DataMember(Order = 4)][Required] public string TransRef { get; set; }
    [DataMember(Order = 5)][Required] public string TransNote { get; set; }
}

[DataContract]
[Route("/api/v1/balance/block", "POST")]
public class BlockBalanceRequest : IPost, IReturn<object>
{
    [DataMember(Order = 1)][Required] public string AccountCode { get; set; }

    [DataMember(Order = 2)][Required] public string CurrencyCode { get; set; }

    [DataMember(Order = 3)][Required] public decimal BlockAmount { get; set; }
    [DataMember(Order = 4)][Required] public string TransRef { get; set; }
    [DataMember(Order = 5)][Required] public string TransNote { get; set; }
}

[DataContract]
[Route("/api/v1/balance/unblock", "POST")]
public class UnBlockBalanceRequest : IPost, IReturn<object>
{
    [DataMember(Order = 1)][Required] public string AccountCode { get; set; }

    [DataMember(Order = 2)][Required] public string CurrencyCode { get; set; }

    [DataMember(Order = 3)][Required] public decimal UnBlockAmount { get; set; }
    [DataMember(Order = 4)][Required] public string TransRef { get; set; }
    [DataMember(Order = 5)][Required] public string TransNote { get; set; }
}

[DataContract]
[Route("/api/v1/balance/paybatch", "POST")]
public class PaybatchRequest : IPost, IReturn<object>
{
    [DataMember(Order = 1)] public List<PaybatchAccount> Accounts { get; set; }
    [DataMember(Order = 2)] public string CurrencyCode { get; set; }
    [DataMember(Order = 3)] public string TransRef { get; set; }
    [DataMember(Order = 4)] public string TransNote { get; set; }
}

[DataContract]
public class PaybatchAccount
{
    [DataMember(Order = 1)] public string AccountCode { get; set; }
    [DataMember(Order = 2)] public decimal Amount { get; set; }
    [DataMember(Order = 3)] public bool Success { get; set; }
    [DataMember(Order = 4)] public string TransRef { get; set; }
    [DataMember(Order = 5)] public string TransNote { get; set; }
}

[DataContract]
[Route("/api/v1/balance/transfer-system", "POST")]
public class TransferSystemRequest : IPost
{
    [DataMember(Order = 1)] public decimal Amount { get; set; }
    [DataMember(Order = 2)] public string SrcAccount { get; set; }
    [DataMember(Order = 3)] public string CurrencyCode { get; set; }
    [DataMember(Order = 4)] public string DesAccount { get; set; }
    [DataMember(Order = 5)] public string TransRef { get; set; }
    [DataMember(Order = 6)] public string TransNote { get; set; }
}