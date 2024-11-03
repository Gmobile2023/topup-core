using System;
using System.Collections.Generic;
using HLS.Paygate.Shared;

namespace HLS.Paygate.Gw.Model.Events;

public interface TopupItemCommand : IEvent
{
    string TransCode { get; set; }
    SaleRequestStatus SaleRequestStatus { get; set; }
    List<CardItemCommand> CardItems { get; set; }
    string TopupItemType { get; set; }
}

public class CardItemCommand
{
    public string CardCode { get; set; }
    public int CardValue { get; set; }
    public string Vendor { get; set; }
    public string Serial { get; set; }
    public DateTime ExpireDate { get; set; }
}