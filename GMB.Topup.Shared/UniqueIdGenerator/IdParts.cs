using System;
using System.Linq;

namespace GMB.Topup.Shared.UniqueIdGenerator; 

public struct IdParts
{
    public IdParts(string id)
    {
        var bytes = Convert.FromBase64String(id);
        Bits = string.Concat(bytes.Select(y => Convert.ToString(y, 2).PadLeft(8, '0')));
        GeneratorId = Convert.ToInt16(Bits.Substring(42, 10), 2);
        Sequence = Convert.ToInt16(Bits.Substring(52, 12), 2);
        Time = TimeSpan.FromMilliseconds(Convert.ToInt64(Bits.Substring(0, 42), 2));
    }

    public string Bits { get; }

    public TimeSpan Time { get; }

    public short GeneratorId { get; }

    public long Sequence { get; }
}