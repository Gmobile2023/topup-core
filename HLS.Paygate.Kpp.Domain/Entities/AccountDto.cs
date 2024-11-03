using System;

namespace HLS.Paygate.Kpp.Domain.Entities
{
    public class AccountDto
    {
        public string AccountCode { get; set; }
        public string AccountType { get; set; }
        public decimal Before { get; set; }
        public decimal Input { get; set; }
        public decimal Transfer { get; set; }
        public decimal Payment { get; set; }
        public decimal After { get; set; }
        public decimal Deviation { get; set; }
        public string Status { get; set; }
        public DateTime MinDate { get; set; }
        public DateTime MaxDate { get; set; }
    }
}