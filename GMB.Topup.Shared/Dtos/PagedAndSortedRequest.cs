using System.Runtime.Serialization;

namespace GMB.Topup.Shared.Dtos;
[DataContract]
public class PagedAndSortedRequest
{
    [DataMember(Order = 1)]
    public virtual string Sorting { get; set; }

    [DataMember(Order = 2)]
    public virtual string Filter { get; set; }

    [DataMember(Order = 3)]
    public virtual int SkipCount { get; set; }

    [DataMember(Order = 4)]
    public virtual int MaxResultCount { get; set; } = 10;
}