using System;
using MongoDbGenericRepository.Models;

namespace HLS.Paygate.Balance.Domain.Entities;

public class Currency : Document
{
    public string CurrencyCode { get; set; }
    public DateTime? ModifiedDate { get; set; }
}