using System.Collections.Generic;
using GMB.Topup.Gw.Model.Dtos;
using GMB.Topup.Shared;
using ServiceStack;

namespace GMB.Topup.Discovery.Requests.Backends;

[Route("/api/v1/partner/create", "POST")]
public class CreatePartnerRequest : IPost, IReturn<NewMessageResponseBase<object>>
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
    public int LastTransTimeConfig { get; set; }
    public List<string> ServiceConfigs { get; set; }
    public List<string> CategoryConfigs { get; set; }
    public List<string> ProductConfigsNotAllow { get; set; }
    public string Ips { get; set; }
    public bool IsCheckReceiverType { get; set; }
    public bool IsNoneDiscount { get; set; }
    public int MaxTotalTrans { get; set; }
    public bool IsCheckPhone { get; set; }
    public bool IsCheckAllowTopupReceiverType { get; set; }
    public string DefaultReceiverType { get; set; }
}

[Route("/api/v1/partner/update", "PUT")]
public class UpdatePartnerRequest : IPut, IReturn<NewMessageResponseBase<object>>
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
    public List<string> ServiceConfigs { get; set; }
    public List<string> CategoryConfigs { get; set; }
    public List<string> ProductConfigsNotAllow { get; set; }
    public string Ips { get; set; }
    public int LastTransTimeConfig { get; set; }
    public bool IsCheckReceiverType { get; set; }
    public bool IsCheckPhone { get; set; }
    public bool IsNoneDiscount { get; set; }
    public int MaxTotalTrans { get; set; }
    public bool IsCheckAllowTopupReceiverType { get; set; }
    public string DefaultReceiverType { get; set; }
}

[Route("/api/v1/partner", "GET")]
public class GetPartnerRequest : IGet, IReturn<NewMessageResponseBase<PartnerConfigDto>>
{
    public string PartnerCode { get; set; }
}

[Route("/api/v1/partner/create-update", "POST")]
public class CreateOrUpdatePartnerRequest : IPost, IReturn<NewMessageResponseBase<object>>
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
    public List<string> ServiceConfigs { get; set; }
    public List<string> CategoryConfigs { get; set; }
    public List<string> ProductConfigsNotAllow { get; set; }
    public string Ips { get; set; }
    public int LastTransTimeConfig { get; set; }
    public bool IsCheckReceiverType { get; set; }
    public bool IsNoneDiscount { get; set; }
    public int MaxTotalTrans { get; set; }
    public bool IsCheckPhone { get; set; }
    public bool IsCheckAllowTopupReceiverType { get; set; }
    public string DefaultReceiverType { get; set; }
    
}