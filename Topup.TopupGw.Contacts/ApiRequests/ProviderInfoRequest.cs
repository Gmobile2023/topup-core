using System.Collections.Generic;
using Topup.Shared;
using Topup.Shared.Dtos;
using ServiceStack;
using Topup.TopupGw.Contacts.Dtos;

namespace Topup.TopupGw.Contacts.ApiRequests;

[Route("/api/v1/provider_info", "POST")]
public class ProviderInfoRequest : IPost, IReturn<NewMessageResponseBase<string>>
{
    public string ProviderCode { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public string ApiUrl { get; set; }
    public string ApiUser { get; set; }
    public string ApiPassword { get; set; }
    public string ExtraInfo { get; set; }
    public List<ProviderServiceDto> ProviderServices { get; set; }
    public int Timeout { get; set; }
    public string PrivateKeyFile { get; set; }
    public string PublicKeyFile { get; set; }
    public string PublicKey { get; set; }
    public int TimeoutProvider { get; set; }
    public int TotalTransError { get; set; }
    public int TimeClose { get; set; }
    public bool IsAutoCloseFail { get; set; }
    public string IgnoreCode { get; set; }
    public int TimeScan { get; set; }
    public int TotalTransScan { get; set; }
    public int TotalTransDubious { get; set; }
    public int TotalTransErrorScan { get; set; }
    public string ParentProvider { get; set; }
    public bool IsAlarm { get; set; }//Bật cảnh báo
    public string ErrorCodeNotAlarm { get; set; }//Bỏ qua các mã lỗi không cảnh báo
    public string AlarmChannel { get; set; }
    public string MessageNotAlarm { get; set; }//Bỏ qua các message lỗi không cảnh báo
    public int ProcessTimeAlarm { get; set; }//Cảnh báo thời gian xử lý giao dịch
    public string AlarmTeleChatId { get; set; }
}

[Route("/api/v1/provider_info", "PATCH")]
public class ProviderInfoUpdateRequest : IPatch, IReturn<NewMessageResponseBase<string>>
{
    public string ProviderCode { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public string ApiUrl { get; set; }
    public string ApiUser { get; set; }
    public string ApiPassword { get; set; }
    public string ExtraInfo { get; set; }
    public List<ProviderServiceDto> ProviderServices { get; set; }
    public int Timeout { get; set; }
    public string PrivateKeyFile { get; set; }
    public string PublicKeyFile { get; set; }
    public string PublicKey { get; set; }
    public int TimeoutProvider { get; set; }
    public int TotalTransError { get; set; }
    public int TimeClose { get; set; }
    public bool IsAutoCloseFail { get; set; }
    public string IgnoreCode { get; set; }
    public int TimeScan { get; set; }
    public int TotalTransScan { get; set; }
    public int TotalTransDubious { get; set; }
    public int TotalTransErrorScan { get; set; }
    public string ParentProvider { get; set; }
    public bool IsAlarm { get; set; }//Bật cảnh báo
    public string ErrorCodeNotAlarm { get; set; }//Bỏ qua các mã lỗi không cảnh báo
    public string AlarmChannel { get; set; }
    public string MessageNotAlarm { get; set; }//Bỏ qua các message lỗi không cảnh báo
    public string AlarmTeleChatId { get; set; }
    public int ProcessTimeAlarm { get; set; }//Cảnh báo thời gian xử lý giao dịch
    
}

[Route("/api/v1/provider_info", "GET")]
public class ProviderInfoGet : IGet, IReturn<NewMessageResponseBase<string>>
{
    public string ProviderCode { get; set; }
}

[Route("/api/v1/provider_response", "POST")]
public class CreateProviderReponse : IPost, IReturn<NewMessageResponseBase<string>>
{
    public string Provider { get; set; }
    public string ResponseCode { get; set; }
    public string ResponseName { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
}

[Route("/api/v1/provider_response", "PUT")]
public class UpdateProviderReponse : IPut, IReturn<NewMessageResponseBase<string>>
{
    public string Provider { get; set; }
    public string ReponseCode { get; set; }
    public string ReponseName { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
}
[Route("/api/v1/provider_response", "DELETE")]
public class DeleteProviderReponse : IDelete, IReturn<NewMessageResponseBase<string>>
{
    public string Provider { get; set; }
    public string Code { get; set; }
}
[Route("/api/v1/provider_response/list", "GET")]
public class GetListProviderResponse :  PagedAndSortedRequest
{
    public string ResponseCode { get; set; }
    public string ResponseName { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public string Provider { get; set; }
}

[Route("/api/v1/provider_response/import", "POST")]
public class ImportListProviderResponse
{
    public List<ProviderReponseDto> ListProviderResponse { get; set; }
}

[Route("/api/v1/provider_response", "GET")]
public class GetProviderReponse : IGet, IReturn<NewMessageResponseBase<ProviderReponseDto>>
{
    public string Provider { get; set; }
    public string Code { get; set; }
}

[Route("/api/v1/callBack", "POST")]
public class CallBackRequest
{
    public string TransCode { get; set; }

    public int ResponseCode { get; set; }

    public string ResponseMessage { get; set; }

    public int TotalTopupAmount { get; set; }

    public string Signature { get; set; }
}

[Route("/api/v1/callBackZoTa", "GET")]
public class CallBackZotaRequest
{
    public string request_id { get; set; }

    public string service_type { get; set; }

    public decimal txn_amount { get; set; }

    public string txn_id { get; set; }

    public string txn_info { get; set; }

    public string txn_status { get; set; }
}

[Route("/api/v1/callBackCG2022", "POST")]
public class CallBackCG2022Request
{
    public string TxnId { get; set; }
    public string ResponseCode { get; set; }
    public string ResponseMessage { get; set; }
    public decimal RequestAmount { get; set; }
    public decimal ActualAmount { get; set; }
}
[Route("/api/v1/callBackCG2022/{ProviderCode}", "POST")]
public class CallBackCG2022V2Request
{
    public string TxnId { get; set; }
    public string ResponseCode { get; set; }
    public string ResponseMessage { get; set; }
    public string ProviderCode { get; set; }
    public decimal RequestAmount { get; set; }
    public decimal ActualAmount { get; set; }
}
[Route("/api/v1/callBackAdvance", "POST")]
public class CallBackAdvanceRequest
{
    public string trans_id { get; set; }

    public string request_id { get; set; }

    public string amount { get; set; }

    public string phone { get; set; }

    public int status { get; set; }

    public string sign { get; set; }
}


[Route("/api/v1/callBackCardGate", "POST")]
public class CallBackCardGateRequest
{
    public string status { get; set; }
    public string message { get; set; }
    public string telco { get; set; }
    public string simtype { get; set; }
    public string mobile { get; set; }
    public int amount { get; set; }
    public int amoutran { get; set; }
    public string refcode { get; set; }
    public string id_tran { get; set; }
    public string creadate { get;set; }
    public string lastdate { get; set; }
    public string sign { get; set; }
}

[Route("/api/v1/provider_SalePriceInfo", "POST")]
public class ProviderSalePriceInfoRequest : IPost, IReturn<NewMessageResponseBase<string>>
{
    public string ProviderCode { get; set; }
    public string ServiceCode { get; set; }
    public string ProviderType { get; set; }
    public string TopupType { get; set; }    
    public string Account { get; set; }
}

[Route("/api/v1/provider_SalePriceInfo", "GET")]
public class ProviderSalePriceInfoGet : IGet, IReturn<NewMessageResponseBase<string>>
{
    public string ProviderCode { get; set; }
    public string ProviderType { get; set; }
    public string TopupType { get; set; }
    
}

[Route("/api/v1/provider_product_info", "GET")]
public class GetProviderProductInfo : IGet
{
    public string ProviderCode { get; set; }
    public string ProviderType { get; set; }
    public string TopupType { get; set; }
    public string AccountNo { get; set; }

}

