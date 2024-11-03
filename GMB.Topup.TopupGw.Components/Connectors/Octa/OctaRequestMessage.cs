using System.Runtime.Serialization;

namespace GMB.Topup.TopupGw.Components.Connectors.Octa;

public class OctaRequestMessage
{
    [DataMember(Name = "Request")] public Request Request { get; set; }
}

public class RequestData
{
    [DataMember(Name = "ReceiptNumber")] public string ReceiptNumber { get; set; }

    [DataMember(Name = "ServiceCode")] public string ServiceCode { get; set; }

    [DataMember(Name = "Price")] public int ? Price { get; set; }

    [DataMember(Name = "PhoneNumber")] public string PhoneNumber { get; set; }

    [DataMember(Name = "Amount")] public int ? Amount { get; set; }

    [DataMember(Name = "RequestDate")] public string RequestDate { get; set; }
}

public class Request
{
    [DataMember(Name = "Data")] public RequestData Data { get; set; }

    [DataMember(Name = "RequestDate")] public string RequestDate { get; set; }
}