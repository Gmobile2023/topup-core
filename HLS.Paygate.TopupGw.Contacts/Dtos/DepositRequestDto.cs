﻿namespace HLS.Paygate.TopupGw.Contacts.Dtos;

public class DepositRequestDto
{
    public string ProviderCode { get; set; }
    public string TransCode { get; set; }
    public decimal Amount { get; set; }
}