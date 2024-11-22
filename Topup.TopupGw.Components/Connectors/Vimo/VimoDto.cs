using System.Collections.Generic;

namespace Topup.TopupGw.Components.Connectors.Vimo;

public class VimoRequest
{
    public string fnc { get; set; }

    public string Merchantcode { get; set; }

    public string data { get; set; }

    public string Checksum { get; set; }
}

public class VimoReponse
{
    public string error_code { get; set; }

    public string error_message { get; set; }

    public string merchant_code { get; set; }

    public string checksum { get; set; }

    public object data { get; set; }
}

public class VimoBalanceDto
{
    public string merchant_code { get; set; }

    public string mc_request_id { get; set; }
}

public class TopupDto
{
    public string mc_request_id { get; set; }
    public string service_code { get; set; }
    public string publisher { get; set; }
    public string receiver { get; set; }
    public int amount { get; set; }
}

public class VpinDto
{
    public string mc_request_id { get; set; }
    public string service_code { get; set; }
    public int quantity { get; set; }
    public int amount { get; set; }
    public string publisher { get; set; }
}

public class cardItem
{
    public string cardCode { get; set; }

    public string cardSerial { get; set; }

    public string expiryDate { get; set; }

    public string cardValue { get; set; }
}

public class PinCodeInfoDto
{
    public string transaction_id { get; set; }

    public List<cardItem> cards { get; set; }
}

public class PaybillDto
{
    public string mc_request_id { get; set; }
    public string service_code { get; set; }
    public string publisher { get; set; }
    public string customer_code { get; set; }
    public bill_payment[] bill_payment { get; set; }
}

public class bill_payment
{
    public string billNumber { get; set; }

    public string period { get; set; }

    public int amount { get; set; }

    public string billType { get; set; }

    public string otherInfo { get; set; }
}

public class BalanceDto
{
    public decimal balance { get; set; }

    public decimal balance_holding { get; set; }
}

public class VbillInfo
{
    public string transaction_id { get; set; }

    public List<billDetailDto> billDetail { get; set; }

    public customerInfoDto customerInfo { get; set; }
}

public class billDetailDto
{
    public string billNumber { get; set; }

    public string period { get; set; }

    public int amount { get; set; }

    public string billType { get; set; }

    public string otherInfo { get; set; }
}

public class otherInfoDto
{
    public string creationDate { get; set; }

    public string expirationDate { get; set; }

    public string payDate { get; set; }

    public string addFee { get; set; }

    public string partial { get; set; }

    public string minAmount { get; set; }
}

public class customerInfoDto
{
    public string customerCode { get; set; }

    public string customerName { get; set; }

    public string customerAddress { get; set; }

    public string customerOtherInfo { get; set; }
}