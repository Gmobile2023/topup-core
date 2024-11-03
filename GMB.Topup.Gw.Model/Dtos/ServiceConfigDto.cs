using System;

namespace GMB.Topup.Gw.Model.Dtos;

public class ServiceConfigDto
{
    public Guid Id { get; set; }
    public string ServiceName { get; set; }
    public string ServiceCode { get; set; }
    public string Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime? ModifiedDate { get; set; }
}