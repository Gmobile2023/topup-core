using HLS.Paygate.Shared;

namespace HLS.Paygate.Gw.Model.Dtos;

public interface IUserInfoRequest
{
    string PartnerCode { get; set; }
    string StaffAccount { get; set; }
    SystemAccountType AccountType { get; set; }
    AgentType AgentType { get; set; }
    string ParentCode { get; set; }
}