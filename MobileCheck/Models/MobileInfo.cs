using System;
using MongoDB.Entities;

namespace MobileCheck.Models;

public class MobileInfo : Entity
{
    public string Mobile { get; set; }
    public string Telco { get; set; }
    public string MobileType { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastCheckDate { get; set; }
    public string CheckerProvider { get; set; }
    public string Index { get; set; }
}