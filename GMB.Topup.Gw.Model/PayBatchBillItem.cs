namespace GMB.Topup.Gw.Model;

public class PayBatchBillItem
{
    public string AgentCode { get; set; }

    public string Mobile { get; set; }

    public string FullName { get; set; }

    public int Quantity { get; set; }

    public decimal PayAmount { get; set; }

    public decimal PayBatchMoney { get; set; }
}