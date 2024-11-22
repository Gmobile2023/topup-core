namespace Topup.Kpp.Domain.Entities
{

    public class kpp_account
    {
        public int id { get; set; }

        public string account_code { get; set; }

        public string account_type { get; set; }

        public string mobile { get; set; }

        public decimal airtime_discount_rate { get; set; }
    }
}