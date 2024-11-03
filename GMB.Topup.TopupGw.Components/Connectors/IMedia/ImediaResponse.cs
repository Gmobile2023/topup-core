using System.Collections.Generic;

namespace GMB.Topup.TopupGw.Components.Connectors.Imedia;

public class ImediaResponse
{
    public decimal dataValue { get; set; }
    public string errorMessage { get; set; }
    public int errorCode { get; set; }
    public decimal merchantBalance { get; set; }
    public string requestID { get; set; }
    public string sysTransId { get; set; }
    public string token { get; set; }
    public int? accRealType { get; set; }

    public List<product> products { get; set; }
}

public class softpin
{
    public string softpinSerial { get; set; }

    public string softpinPinCode { get; set; }

    public string expiryDate { get; set; }
}

public class product
{
    public int productId { get; set; }

    public string productValue { get; set; }

    public List<softpin> softpins { get; set; }
}