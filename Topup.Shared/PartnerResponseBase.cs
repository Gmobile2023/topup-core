using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Topup.Shared;

[DataContract]
public class PartnerResponseBase<T>
{
    public PartnerResponseBase()
    {
    }

    public PartnerResponseBase(string status, string message)
    {
        Status = status;
        Message = message;
    }

    [DataMember(Order = 1)] public T Data { get; set; }
    [DataMember(Order = 2)] public string Status { get; set; }
    [DataMember(Order = 3)] public string Message { get; set; }
    [DataMember(Order = 4)] public string Sig { get; set; }
}