using System.Collections.Generic;
using System.Runtime.Serialization;

namespace HLS.Paygate.TopupGw.Components.Connectors.Payoo;

public class PayooResponse
{
    public int ReturnCode { get; set; }
    public string SystemTrace { get; set; }
    public string DescriptionCode { get; set; }
    public List<PayCodeInfo> PayCodes { get; set; }
    public string OrderNo { get; set; }
    public string SubReturnCode { get; set; }
    public string Status { get; set; }
}

public class PayCodeInfo
{
    public string CardCode { get; set; }
    public string Expired { get; set; }
    public string SeriNumber { get; set; }
    public string TypeCard { get; set; }
}