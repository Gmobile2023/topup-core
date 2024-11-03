namespace GMB.Topup.TopupGw.Components.Connectors.Imedia;

public class ImediaRequest
{
    public int operation { get; set; }

    public string username { get; set; }

    public string merchantPass { get; set; }

    public string requestID { get; set; }

    public string keyBirthdayTime { get; set; }

    public string targetAccount { get; set; }

    public string providerCode { get; set; }

    public int topupAmount { get; set; }

    public string accountType { get; set; }

    public string signature { get; set; }

    public string token { get; set; }

    public buyItems[] buyItems { get; set; }
}

public class buyItems
{
    public int productId { get; set; }

    public int quantity { get; set; }
}