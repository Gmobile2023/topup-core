﻿using System;
using HLS.Paygate.TopupGw.Contacts.Enums;
using MongoDbGenericRepository.Models;

namespace HLS.Paygate.TopupGw.Domains.Entities;

public class CardRequestLog : Document
{
    public string TransCode { get; set; }
    public string TransRef { get; set; }
    public decimal TransAmount { get; set; }
    public string ReceiverInfo { get; set; }
    public TransRequestStatus Status { get; set; }
    public DateTime RequestDate { get; set; }
    public DateTime ModifiedDate { get; set; }
    public int Quantity { get; set; }
    public string Vendor { get; set; }
    public string CategoryCode { get; set; }
    public string ProductCode { get; set; }
    public string ProviderCode { get; set; }
    public string ResponseInfo { get; set; }
    public string ServiceCode { get; set; }
    public string PartnerCode { get; set; }
    public string ReferenceCode { get; set; }
    public string TransIndex { get; set; }
}