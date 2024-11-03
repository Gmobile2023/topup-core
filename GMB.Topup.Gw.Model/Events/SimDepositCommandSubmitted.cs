using System;

namespace HLS.Paygate.Gw.Model.Events
{
    public interface SimDepositCommandSubmitted
    {
        Guid Id { get; }
        DateTime TimeStamp { get; }
        string TransCode { get; }
    }
}