using System.Runtime.Serialization;

namespace HLS.Paygate.Shared.Dtos;
[DataContract]
public class AccountInfoDto
{
    [DataMember(Order = 1)] public int UserId { get; set; }
    [DataMember(Order = 2)] public string FullName { get; set; }
    [DataMember(Order = 3)] public string Email { get; set; }
    [DataMember(Order = 4)] public string Mobile { get; set; }
    [DataMember(Order = 5)] public string UserName { get; set; }
    [DataMember(Order = 6)] public string AccountCode { get; set; }
    [DataMember(Order = 7)] public string ParentCode { get; set; }
}