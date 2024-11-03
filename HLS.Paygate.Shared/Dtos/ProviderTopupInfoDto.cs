using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace HLS.Paygate.Shared.Dtos
{
    [DataContract]
    public class ProviderTopupInfoDto
    {
        [DataMember(Order = 1)] public string ProviderCode { get; set; }
        [DataMember(Order = 2)] public string Username { get; set; }
        [DataMember(Order = 3)] public string Password { get; set; }
        [DataMember(Order = 4)] public string ApiUrl { get; set; }
        [DataMember(Order = 5)] public string ApiUser { get; set; }
        [DataMember(Order = 6)] public string ApiPassword { get; set; }
        [DataMember(Order = 7)] public string ExtraInfo { get; set; }
        [DataMember(Order = 8)] public string PublicKey { get; set; }
        [DataMember(Order = 9)] public bool IsAlarm { get; set; }
        [DataMember(Order = 10)] public string AlarmTeleChatId { get; set; }
    }
}
