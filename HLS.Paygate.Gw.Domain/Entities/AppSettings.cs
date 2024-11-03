using System;
using MongoDbGenericRepository.Models;

namespace HLS.Paygate.Gw.Domain.Entities;

public class AppSettings : Document
{
    public DateTime CreatedDate { get; set; }
    public string Name { get; set; }
    public string Key { get; set; }
    public string Value { get; set; }
    public DateTime? LastTransTime { get; set; }
}