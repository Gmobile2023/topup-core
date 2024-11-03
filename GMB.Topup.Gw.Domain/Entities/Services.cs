using System;
using MongoDbGenericRepository.Models;

namespace GMB.Topup.Gw.Domain.Entities;

public class ServiceConfig : Document
{
    public string ServiceName { get; set; }
    public string ServiceCode { get; set; }
    public string Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime? ModifiedDate { get; set; }
}