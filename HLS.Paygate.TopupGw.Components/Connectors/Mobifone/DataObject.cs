using System.Runtime.Serialization;

namespace HLS.Paygate.TopupGw.Components.Connectors.Mobifone;

public class DataObject
{
    [DataMember(Name = "fee")] public string Fee { get; set; }

    [DataMember(Name = "result")] public string Result { get; set; }

    [DataMember(Name = "result_namespace")] public string ResultNamespace { get; set; }

    [DataMember(Name = "schedule_id")] public string ScheduleId { get; set; }

    [DataMember(Name = "transid")] public string TransId { get; set; }
    [DataMember(Name = "avail")] public string Avail { get; set; }
    [DataMember(Name = "avail_1")] public string Avail1 { get; set; }
    [DataMember(Name = "avail_2")] public string Avail2 { get; set; }
    [DataMember(Name = "avail_3")] public string Avail3 { get; set; }
    [DataMember(Name = "current")] public string Current { get; set; }
    [DataMember(Name = "current_1")] public string Current1 { get; set; }
    [DataMember(Name = "current_2")] public string Current2 { get; set; }
    [DataMember(Name = "current_3")] public string Current3 { get; set; }
    [DataMember(Name = "pending")] public string Pending { get; set; }
    [DataMember(Name = "pending_1")] public string Pending1 { get; set; }
    [DataMember(Name = "pending_2")] public string Pending2 { get; set; }
    [DataMember(Name = "pending_3")] public string Pending3 { get; set; }
}