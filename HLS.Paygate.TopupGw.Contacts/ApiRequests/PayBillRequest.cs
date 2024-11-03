﻿using HLS.Paygate.Shared;
using ServiceStack;

namespace HLS.Paygate.TopupGw.Contacts.ApiRequests;

[Route("/api/v1/bill", "POST")]
public class PayBillRequest : IPost, IReturn<NewMessageReponseBase<object>>
{
    public string ReceiverInfo { get; set; }
    public bool IsInvoice { get; set; }
    public decimal Amount { get; set; }
    public string Vendor { get; set; }
    public string TransRef { get; set; }
    public string ProviderCode { get; set; }
    public string ProductCode { get; set; }
}