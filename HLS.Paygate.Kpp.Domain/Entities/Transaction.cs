using System;

namespace HLS.Paygate.Kpp.Domain.Entities
{
    public class Transaction
    {
        public int Id { get; set; }

        public string trans_code { get; set; }

        public string account_code { get; set; }

        public string type { get; set; }

        public decimal amount { get; set; }

        public string receiver { get; set; }

        public string telco { get; set; }

        public DateTime created_date { get; set; }

        public string kpp_trans_id { get; set; }

        public string kpp_response { get; set; }

        public int status { get; set; }

        public decimal balance { get; set; }

        public decimal? trans_amount { get; set; }

        public decimal? discount_rate { get; set; }

        public DateTime? ended_date { get; set; }
    }

    public class TransactionView : Transaction
    {
        public DateTime transDate => ended_date == null ? created_date : ended_date.Value;
    }

    public class TransKppInfo
    {
        public string TransCode { get; set; }

        public string AccountCode { get; set; }

        public decimal Balance { get; set; }
    }

}