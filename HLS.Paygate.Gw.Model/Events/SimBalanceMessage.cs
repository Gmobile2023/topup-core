﻿using System;

namespace HLS.Paygate.Gw.Model.Events
{
    public class SimBalanceMessage
    {
        public string SimNumber { get; set; }
        public decimal BalanceBefore { get; set; }
        public decimal BalanceAfter { get; set; }
        public decimal Increase { get; set; }
        public decimal Decrease { get; set; }
        public string TransRef { get; set; }
        public string TransCode { get; set; }
        public string ModifiedBy { get; set; }
        public string CreatedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public string Serial { get; set; }
        public string CardCode { get; set; }
        public DateTime CreatedDate { get; set; }
        public string Description { get; set; }
        public string TransNote { get; set; }
        public string TransactionType { get; set; }
    }
}