using System;
using MongoDbGenericRepository.Models;

namespace HLS.Paygate.Report.Domain.Entities;

public class ReportAccountDto : Document
{
    public int UserId { get; set; }
    public string FullName { get; set; }
    public string Email { get; set; }
    public string Mobile { get; set; }

    public string UserName { get; set; }
    public string AccountCode { get; set; }
    public string ParentCode { get; set; }
    public string TreePath { get; set; }
    public int AccountType { get; set; }
    public int AgentType { get; set; }
    public string AgentName { get; set; }
    public string Gender { get; set; }
    public int NetworkLevel { get; set; }

    public int CityId { get; set; }
    public string CityName { get; set; }
    public int DistrictId { get; set; }
    public string DistrictName { get; set; }
    public int WardId { get; set; }
    public string WardName { get; set; }
    public int? UserSaleLeadId { get; set; }
    public string IdIdentity { get; set; }
    public string SaleCode { get; set; }
    public string LeaderCode { get; set; }
    public string ChatId { get; set; }
    public DateTime? CreationTime { get; set; }
}