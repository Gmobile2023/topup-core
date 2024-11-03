using System;
using MongoDbGenericRepository.Models;

namespace GMB.Topup.Balance.Domain.Entities;

public class Currency : Document
{
    public string CurrencyCode { get; set; }
    public DateTime? ModifiedDate { get; set; }
}