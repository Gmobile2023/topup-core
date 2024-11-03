using System;

namespace HLS.Paygate.Kpp.Domain.Entities
{
    public class Transfer
    {
        public int id { get; set; }

        public string sender { get; set; }

        public string receiver { get; set; }

        public decimal amount { get; set; }

        public string trans_id { get; set; }

        public DateTime created_date { get; set; }

        public DateTime? ended_date { get; set; }

        public int status { get; set; }

        public string kpp_trans_id { get; set; }

        public string kpp_response { get; set; }

        public string is_deposit { get; set; }
    }

    public class TransferView : Transfer
    {
        public DateTime TransDate => ended_date == null ? created_date : ended_date.Value;
    }
}