﻿using System;
using MongoDbGenericRepository.Models;

namespace Topup.Gw.Domain.Entities;

public class PartnerConfig : Document
{
    public string PartnerCode { get; set; }
    public string PartnerName { get; set; }
    public string PublicKeyFile { get; set; }
    public string PrivateKeyFile { get; set; }
    public string SecretKey { get; set; }
    public string ClientId { get; set; }
    public string UserName { get; set; }
    public string Password { get; set; }
    public bool EnableSig { get; set; }
    public bool IsActive { get; set; }
    public bool IsCheckReceiverType { get; set; }
    public bool IsNoneDiscount { get; set; }
    public string ServiceConfig { get; set; }
    public string CategoryConfigs { get; set; }
    public string ProductConfigsNotAllow { get; set; }
    public string Ips { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public int LastTransTimeConfig { get; set; }
    public int MaxTotalTrans { get; set; }
    public bool IsCheckPhone { get; set; }
    public bool IsCheckAllowTopupReceiverType { get; set; }
    public string DefaultReceiverType { get; set; }
}