﻿using System.Collections.Generic;
using System.Runtime.Serialization;

namespace GMB.Topup.Shared;

public class MessageResponseBase
{
    public MessageResponseBase()
    {
        ResponseCode = ResponseCodeConst.Error;
        ResponseMessage = "";
    }

    public MessageResponseBase(string code, string message)
    {
        ResponseCode = code;
        ResponseMessage = message;
    }

    [DataMember(Order = 1)] public string ResponseCode { get; set; }
    [DataMember(Order = 2)] public string ResponseMessage { get; set; }
    [DataMember(Order = 3)] public object Payload { get; set; }
    [DataMember(Order = 4)] public object SumData { get; set; }
    [DataMember(Order = 5)] public string ExtraInfo { get; set; }
    [DataMember(Order = 6)] public string TransCodeProvider { get; set; }
    [DataMember(Order = 7)] public string ProviderCode { get; set; }
    [DataMember(Order = 8)] public decimal PaymentAmount { get; set; }
    [DataMember(Order = 9)] public decimal RequestAmount { get; set; }
    [DataMember(Order = 10)] public string TransCode { get; set; }
    [DataMember(Order = 11)] public string TransRef { get; set; }
    [DataMember(Order = 12)] public string ProviderResponseCode { get; set; }
    [DataMember(Order = 13)] public string ProviderResponseMessage { get; set; }
    [DataMember(Order = 14)] public string ProviderResponseTransCode { get; set; }
    [DataMember(Order = 15)] public string ReceiverType { get; set; }
    [DataMember(Order = 16)] public string Exception { get; set; }

    public static MessageResponseBase Error()
    {
        return new MessageResponseBase("00", "Error");
    }

    public static MessageResponseBase Error(string message)
    {
        return new MessageResponseBase("00", message);
    }

    //public static MessageResponseBase Error(object data)
    //{
    //    var rs = new MessageResponseBase("00", "Error") { Payload = data };
    //    return rs;
    //}

    public static MessageResponseBase Success()
    {
        return new MessageResponseBase("01", "Success");
    }

    public static MessageResponseBase Success(string message)
    {
        return new MessageResponseBase("01", message);
    }

    public static MessageResponseBase Success(object data)
    {
        var rs = new MessageResponseBase("01", "Success") { Payload = data };
        return rs;
    }
}
[DataContract]
public class BalanceResponse
{
    [DataMember(Order = 1)] public decimal SrcBalance { get; set; }
    [DataMember(Order = 2)] public decimal DesBalance { get; set; }
    [DataMember(Order = 3)] public string TransactionCode { get; set; }
    [DataMember(Order = 4)] public List<BalanceAfterTransDto> BalanceAfterTrans { get; set; }
}

[DataContract]
public class BalanceAfterTransDto
{
    [DataMember(Order = 1)] public string SrcAccount { get; set; }
    [DataMember(Order = 2)] public decimal SrcBalance { get; set; }
    [DataMember(Order = 3)] public decimal SrcBeforeBalance { get; set; }
    [DataMember(Order = 4)] public string DesAccount { get; set; }
    [DataMember(Order = 5)] public decimal DesBalance { get; set; }
    [DataMember(Order = 6)] public decimal DesBeforeBalance { get; set; }
    [DataMember(Order = 7)] public decimal Amount { get; set; }
    [DataMember(Order = 8)] public string TransCode { get; set; }
    [DataMember(Order = 9)] public string CurrencyCode { get; set; }
}

[DataContract]
public class ResponseMessageApi<T>
{
    public ResponseMessageApi()
    {
        Error = new ErrorMessage();
    }

    [DataMember(Order = 1, Name = "result")] public T Result { get; set; }
    [DataMember(Order = 2, Name = "success")] public bool Success { get; set; }
    [DataMember(Order = 3, Name = "error")] public ErrorMessage Error { get; set; }
}
[DataContract]
public class ErrorMessage
{
    public ErrorMessage()
    {
        Code = 0;
    }

    [DataMember(Name = "code", Order = 1)] public int Code { get; set; }
    [DataMember(Name = "message", Order = 2)] public string Message { get; set; }
    [DataMember(Name = "details", Order = 3)] public string Details { get; set; }

    [DataMember(Name = "validationErrors", Order = 4)]
    public string ValidationErrors { get; set; }
}

public class MessagePagedResponseBase : MessageResponseBase
{
    [DataMember(Order = 1)] public Pagination Pagination { get; set; }
    [DataMember(Order = 2)] public int Total { get; set; }
}
[DataContract]
public class Pagination
{
    [DataMember(Order = 1)] public int Limit { get; set; }
    [DataMember(Order = 2)] public int Offset { get; set; }
}
[DataContract]
public class CardResponseMesssage : MessageResponseBase
{
    [DataMember(Order = 1)] public string ServerTransCode { get; set; }
    [DataMember(Order = 2)] public int CardRealValue { get; set; }
}
[DataContract]
public class ResponseMesssageObject<T>
{
    [DataMember(Order = 1)] public T Payload { get; set; }
    [DataMember(Order = 2)] public string ResponseCode { get; set; }
    [DataMember(Order = 3)] public string ResponseMessage { get; set; }
    [DataMember(Order = 4)] public string ExtraInfo { get; set; }
    [DataMember(Order = 5)] public int Total { get; set; }
}

[DataContract]
public class MessageResponseTopup : MessageResponseBase
{
    [DataMember(Order = 1)] public decimal ChargeAmount { get; set; }
}
[DataContract]
public class NewMessageResponseBase<T>
{
    [DataMember(Order = 1)] public T Results { get; set; }
    [DataMember(Order = 2)] public ResponseStatusApi ResponseStatus { get; set; }
    [DataMember(Order = 3)] public string Signature { get; set; }
}

[DataContract]
public class ResponseObject<T>
{
    [DataMember(Name = "result", Order = 1)] public T Result { get; set; }
    [DataMember(Name = "success", Order = 2)] public bool Success { get; set; }
}
[DataContract]
public class ResultObject<T>
{
    [DataMember(Order = 1)] public string ResponseCode { get; set; }
    [DataMember(Order = 2)] public string ResponseMessage { get; set; }
    [DataMember(Order = 3)] public string ExtraInfo { get; set; }
    [DataMember(Order = 4)] public int Total { get; set; }
    [DataMember(Order = 5)] public T Payload { get; set; }
}
[DataContract]
public class ResponsePagingMessage<T>
{
    [DataMember(Order = 1)] public T Items { get; set; }
    [DataMember(Order = 2)] public int TotalPage { get; set; }
    [DataMember(Order = 3)] public int Total { get; set; }
}
[DataContract]
public class ResponseCallBack
{
    [DataMember(Name = "TransCode", Order = 1)] public string TransCode { get; set; }

    [DataMember(Name = "ResponseCode", Order = 2)] public int ResponseCode { get; set; }
}
[DataContract]
public class ResponseCallBackReponse : ResponseCallBack
{
    [DataMember(Order = 1)] public decimal RequestAmount { get; set; }

    [DataMember(Order = 2)] public string TransRef { get; set; }

    [DataMember(Order = 3)] public string ReceiverInfo { get; set; }

    [DataMember(Order = 4)] public bool IsRefund { get; set; }

    [DataMember(Order = 5)] public string TopupGateTimeOut { get; set; }
}

[DataContract]
public class ResponseStatusApi
{
    public ResponseStatusApi()
    {

    }

    public ResponseStatusApi(string errorCode)
    {
        ErrorCode = errorCode;
    }

    public ResponseStatusApi(string errorCode, string message)
        : this(errorCode)
    {
        Message = message;
    }

    [DataMember(Order = 1)] public string ErrorCode { get; set; }
    [DataMember(Order = 2)] public string Message { get; set; }
    [DataMember(Order = 3)] public string TransCode { get; set; }
}
[DataContract]
public class ResponseProvider
{
    [DataMember(Order = 1)] public string Code { get; set; }
    [DataMember(Order = 2)] public string Message { get; set; }
    [DataMember(Order = 3)] public bool Responsed { get; set; }
    [DataMember(Order = 4)] public decimal? PaymentAmount { get; set; }
    [DataMember(Order = 5)] public string TransCode { get; set; }
    [DataMember(Order = 6)] public string TransRef { get; set; }
    [DataMember(Order = 7)] public string ProviderResponseTransCode { get; set; }
    [DataMember(Order = 8)] public string ReceiverType { get; set; }
    [DataMember(Order = 9)] public string PayLoad { get; set; }
    [DataMember(Order = 10)] public string ProviderCode { get; set; }

}

public class ResponseCallBackCardGate
{
    [DataMember(Name = "status", Order = 1)] public string Status { get; set; }

    [DataMember(Name = "message", Order = 2)] public string Message { get; set; }
}

public class ResponseCallBackAdvance
{
    [DataMember(Name = "status", Order = 1)] public int Status { get; set; }
    [DataMember(Name = "message", Order = 2)] public string Message { get; set; }
    [DataMember(Name = "request_id", Order = 3)] public string RequestId { get; set; }
    [DataMember(Name = "trans_id", Order = 4)] public string TransId { get; set; }
}
