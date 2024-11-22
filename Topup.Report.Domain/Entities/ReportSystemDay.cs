using System;
using System.Collections.Generic;
using Topup.Report.Model.Dtos;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDbGenericRepository.Models;
namespace Topup.Report.Domain.Entities
{
    public class ReportSystemDay : Document
    {               
        public double BalanceBefore { get; set; }
        public double BalanceAfter { get; set; }       
        public string AccountCode { get; set; }
        public DateTime UpdateDate { get; set; }
        public string CurrencyCode { get; set; }
        public string TextDay { get; set; }
    }
}
