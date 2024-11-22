namespace Topup.Gw.Model.Dtos;

public class AccountTransLimitConfigDto
{
    public decimal LimitAmount { get; set; }
    public decimal LimitConfig { get; set; }
    public string ServiceCode { get; set; }
    public string CateroryCode { get; set; }
    public string ProductCode { get; set; }
    public string AccountCode { get; set; }
}

public class AccountProductLimitDto
{
    public int TotalQuantity { get; set; }
    public decimal TotalAmount { get; set; }
}