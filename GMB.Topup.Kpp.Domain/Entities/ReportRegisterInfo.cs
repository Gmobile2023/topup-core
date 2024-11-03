using System;
using MongoDbGenericRepository.Models;

namespace GMB.Topup.Kpp.Domain.Entities
{
    public class ReportRegisterInfo : Document
    {
        public string Name { get; set; }
        public string Code { get; set; }
        public string Content { get; set; }
        public string EmailSend { get; set; }
        public string EmailCC { get; set; }
        public bool IsAuto { get; set; }
        public string AccountList { get; set; }
        public string Providers { get; set; }
    }

    public class AccountKppInfo : Document
    {
        public string AccountCode { get; set; }
        public DateTime CreatedDate { get; set; }
        public string DateText { get; set; }
        public decimal Balance { get; set; }
    }

    public class AccountDiscountInfo : Document
    {
        public string AccountCode { get; set; }
        public string AccountType { get; set; }      
        public decimal DiscountRate { get; set; }
    }
}