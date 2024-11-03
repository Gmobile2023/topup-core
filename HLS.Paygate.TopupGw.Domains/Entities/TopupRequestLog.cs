using System;
using HLS.Paygate.TopupGw.Contacts.Enums;
using MongoDbGenericRepository.Models;

namespace HLS.Paygate.TopupGw.Domains.Entities;

public class TopupRequestLog : Document
{
    public string TransCode { get; set; }
    public string TransRef { get; set; }
    public decimal TransAmount { get; set; }
    public string ReceiverInfo { get; set; }
    public TransRequestStatus Status { get; set; }
    public DateTime RequestDate { get; set; }
    public DateTime ModifiedDate { get; set; }
    public string Vendor { get; set; }
    public string CategoryCode { get; set; }
    public string ProductCode { get; set; }
    public string ProviderCode { get; set; }
    public string ResponseInfo { get; set; }
    public string ServiceCode { get; set; }
    public string PartnerCode { get; set; }
    public string ReferenceCode { get; set; }
    public string TransIndex { get; set; }
    public decimal AmountProvider { get; set; }
    public string TopupGateTimeOut { get; set; }

    //nhannv: 
    //Thoi gian xu ly giao dich
    public int? ProviderSetTransactionTimeout { get; set; }
    //Thoi gian check ket qua
    public int? ProviderMaxWaitingTimeout { get; set; }
    /// <summary>
    /// Trả kết quả ngay khi tiếp nhận thành công giao dịch
    /// </summary>
    public bool? IsEnableResponseWhenJustReceived { get; set; }
    /// <summary>
    /// Trạng thái trả ra tiếp nhận giao dịch
    /// </summary>
    public string StatusResponseWhenJustReceived { get; set; }
    public int? WaitingTimeResponseWhenJustReceived { get; set; }
    public double ProcessedTime { get; set; }
}